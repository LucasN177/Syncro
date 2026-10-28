-- 009: In-App-Benachrichtigungen
-- Werden per Trigger geschrieben bei: Zuweisung, Kommentar, @Erwähnung, Projekteinladung.
-- Nutzer können nur ihre eigenen lesen, als gelesen markieren und löschen.

create table if not exists public.notifications (
    id uuid primary key default gen_random_uuid(),
    user_id uuid not null references public.profiles(id) on delete cascade,
    project_id uuid references public.projects(id) on delete cascade,
    issue_id uuid references public.issues(id) on delete cascade,
    actor_id uuid references public.profiles(id) on delete set null,
    type text not null check (type in ('assigned', 'commented', 'mentioned', 'member_added')),
    message text not null,
    read_at timestamptz,
    created_at timestamptz not null default now()
);

create index if not exists notifications_user_created_idx on public.notifications (user_id, created_at desc);
create index if not exists notifications_user_unread_idx on public.notifications (user_id) where read_at is null;

alter table public.notifications enable row level security;

drop policy if exists "Users can read their notifications" on public.notifications;
create policy "Users can read their notifications"
    on public.notifications for select
    to authenticated
    using (user_id = (select auth.uid()));

drop policy if exists "Users can mark their notifications as read" on public.notifications;
create policy "Users can mark their notifications as read"
    on public.notifications for update
    to authenticated
    using (user_id = (select auth.uid()))
    with check (user_id = (select auth.uid()));

drop policy if exists "Users can delete their notifications" on public.notifications;
create policy "Users can delete their notifications"
    on public.notifications for delete
    to authenticated
    using (user_id = (select auth.uid()));

-- Clients dürfen nur read_at ändern
create or replace function public.notifications_before_update()
returns trigger
language plpgsql
as $$
begin
    new.user_id = old.user_id;
    new.project_id = old.project_id;
    new.issue_id = old.issue_id;
    new.actor_id = old.actor_id;
    new.type = old.type;
    new.message = old.message;
    new.created_at = old.created_at;
    return new;
end;
$$;

drop trigger if exists notifications_before_update on public.notifications;
create trigger notifications_before_update
    before update on public.notifications
    for each row execute function public.notifications_before_update();

-- ── Hilfsfunktionen ──────────────────────────────────────────────────────────

create or replace function public.profile_display_name(p_user_id uuid)
returns text
language sql
stable
security definer
set search_path = ''
as $$
    select coalesce(nullif(trim(p.vorname || ' ' || p.nachname), ''), nullif(p.username, ''), p.email, 'Jemand')
    from public.profiles p
    where p.id = p_user_id;
$$;

revoke execute on function public.profile_display_name(uuid) from public, anon;
grant execute on function public.profile_display_name(uuid) to authenticated;

-- Benachrichtigt einen Nutzer, außer er hat die Aktion selbst ausgelöst
create or replace function public.notify_user(
    p_user_id uuid,
    p_project_id uuid,
    p_issue_id uuid,
    p_type text,
    p_message text
)
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
    if p_user_id is null or p_user_id = auth.uid() then
        return;
    end if;

    if not exists (select 1 from public.profiles where id = p_user_id) then
        return;
    end if;

    insert into public.notifications (user_id, project_id, issue_id, actor_id, type, message)
    values (p_user_id, p_project_id, p_issue_id, auth.uid(), p_type, p_message);
end;
$$;

revoke execute on function public.notify_user(uuid, uuid, uuid, text, text) from public, anon, authenticated;

-- ── Trigger: Zuweisung ───────────────────────────────────────────────────────

create or replace function public.notify_issue_assigned()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    if new.assignee_id is not null
       and (tg_op = 'INSERT' or new.assignee_id is distinct from old.assignee_id) then
        perform public.notify_user(new.assignee_id, new.project_id, new.id, 'assigned',
            format('%s hat dir %s „%s“ zugewiesen',
                coalesce(public.profile_display_name(auth.uid()), 'Jemand'),
                public.issue_key(new.project_id, new.number), new.title));
    end if;
    return null;
end;
$$;

drop trigger if exists issues_notify_assigned on public.issues;
create trigger issues_notify_assigned
    after insert or update of assignee_id on public.issues
    for each row execute function public.notify_issue_assigned();

-- ── Trigger: Kommentar und @Erwähnung ────────────────────────────────────────

create or replace function public.notify_issue_comment()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_issue public.issues%rowtype;
    v_key text;
    v_actor text;
    v_mentioned uuid[] = '{}';
    v_member record;
    v_escaped text;
begin
    select * into v_issue from public.issues where id = new.issue_id;
    v_key = public.issue_key(v_issue.project_id, v_issue.number);
    v_actor = coalesce(public.profile_display_name(auth.uid()), 'Jemand');

    -- @username von Projektmitgliedern (Groß-/Kleinschreibung egal)
    for v_member in
        select p.id, p.username
        from public.project_members m
        join public.profiles p on p.id = m.user_id
        where m.project_id = v_issue.project_id and p.username <> ''
    loop
        v_escaped = regexp_replace(v_member.username, '([.+*?^$()\[\]{}|\\-])', '\\\1', 'g');
        if new.body ~* ('(^|[^[:alnum:]_])@' || v_escaped || '($|[^[:alnum:]_.-])') then
            v_mentioned = v_mentioned || v_member.id;
            perform public.notify_user(v_member.id, v_issue.project_id, v_issue.id, 'mentioned',
                format('%s hat dich in %s erwähnt', v_actor, v_key));
        end if;
    end loop;

    -- Assignee und Ersteller über neue Kommentare informieren (ohne bereits Erwähnte)
    if v_issue.assignee_id is not null and not v_issue.assignee_id = any(v_mentioned) then
        perform public.notify_user(v_issue.assignee_id, v_issue.project_id, v_issue.id, 'commented',
            format('%s hat %s „%s“ kommentiert', v_actor, v_key, v_issue.title));
    end if;

    if v_issue.created_by is not null
       and v_issue.created_by is distinct from v_issue.assignee_id
       and not v_issue.created_by = any(v_mentioned) then
        perform public.notify_user(v_issue.created_by, v_issue.project_id, v_issue.id, 'commented',
            format('%s hat %s „%s“ kommentiert', v_actor, v_key, v_issue.title));
    end if;

    return null;
end;
$$;

drop trigger if exists issue_comments_notify on public.issue_comments;
create trigger issue_comments_notify
    after insert on public.issue_comments
    for each row execute function public.notify_issue_comment();

-- ── Trigger: Projekteinladung ────────────────────────────────────────────────

create or replace function public.notify_member_added()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_project_name text;
begin
    select name into v_project_name from public.projects where id = new.project_id;
    perform public.notify_user(new.user_id, new.project_id, null, 'member_added',
        format('%s hat dich zum Projekt „%s“ hinzugefügt',
            coalesce(public.profile_display_name(auth.uid()), 'Jemand'), v_project_name));
    return null;
end;
$$;

drop trigger if exists project_members_notify on public.project_members;
create trigger project_members_notify
    after insert on public.project_members
    for each row execute function public.notify_member_added();
