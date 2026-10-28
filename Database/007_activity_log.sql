-- 007: Aktivitätsprotokoll für die Dashboard-Timeline
-- Wird ausschließlich per Trigger geschrieben; Clients haben nur Lesezugriff.

create table if not exists syncro.activity_log (
    id uuid primary key default gen_random_uuid(),
    project_id uuid not null references syncro.projects(id) on delete cascade,
    actor_id uuid references syncro.profiles(id) on delete set null,
    action text not null check (action in (
        'project_created', 'member_added', 'member_removed',
        'issue_created', 'issue_moved', 'issue_assigned', 'issue_deleted',
        'comment_added'
    )),
    entity_id uuid,
    message text not null,
    created_at timestamptz not null default now()
);

create index if not exists activity_log_project_created_idx on syncro.activity_log (project_id, created_at desc);

alter table syncro.activity_log enable row level security;

drop policy if exists "Members can read activity" on syncro.activity_log;
create policy "Members can read activity"
    on syncro.activity_log for select
    to authenticated
    using (syncro.is_project_member(project_id));

create or replace function syncro.log_activity(
    p_project_id uuid,
    p_action text,
    p_entity_id uuid,
    p_message text
)
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
    -- Beim Löschen eines Projekts (Cascade) ist das Projekt bereits weg
    if not exists (select 1 from syncro.projects where id = p_project_id) then
        return;
    end if;

    insert into syncro.activity_log (project_id, actor_id, action, entity_id, message)
    values (p_project_id, auth.uid(), p_action, p_entity_id, p_message);
end;
$$;

revoke execute on function syncro.log_activity(uuid, text, uuid, text) from public, anon, authenticated;

create or replace function syncro.issue_key(p_project_id uuid, p_number integer)
returns text
language sql
stable
security definer
set search_path = ''
as $$
    select coalesce((select key from syncro.projects where id = p_project_id), 'PRJ') || '-' || p_number;
$$;

revoke execute on function syncro.issue_key(uuid, integer) from public, anon;
grant execute on function syncro.issue_key(uuid, integer) to authenticated;

-- ── Trigger: projects ────────────────────────────────────────────────────────

create or replace function syncro.log_project_activity()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    perform syncro.log_activity(new.id, 'project_created', new.id,
        format('Projekt „%s“ wurde angelegt', new.name));
    return null;
end;
$$;

drop trigger if exists projects_log_activity on syncro.projects;
create trigger projects_log_activity
    after insert on syncro.projects
    for each row execute function syncro.log_project_activity();

-- ── Trigger: project_members ─────────────────────────────────────────────────

create or replace function syncro.log_member_activity()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_name text;
begin
    select coalesce(nullif(trim(p.vorname || ' ' || p.nachname), ''), nullif(p.username, ''), p.email)
    into v_name
    from syncro.profiles p
    where p.id = coalesce(new.user_id, old.user_id);

    if tg_op = 'INSERT' then
        -- Eigentümer beim Anlegen des Projekts nicht extra protokollieren
        if syncro.is_project_owner(new.project_id, new.user_id) then
            return null;
        end if;
        perform syncro.log_activity(new.project_id, 'member_added', new.user_id,
            format('%s wurde dem Projekt hinzugefügt', v_name));
    else
        perform syncro.log_activity(old.project_id, 'member_removed', old.user_id,
            format('%s wurde aus dem Projekt entfernt', v_name));
    end if;
    return null;
end;
$$;

drop trigger if exists project_members_log_activity on syncro.project_members;
create trigger project_members_log_activity
    after insert or delete on syncro.project_members
    for each row execute function syncro.log_member_activity();

-- ── Trigger: issues ──────────────────────────────────────────────────────────

create or replace function syncro.log_issue_activity()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_assignee text;
begin
    if tg_op = 'INSERT' then
        perform syncro.log_activity(new.project_id, 'issue_created', new.id,
            format('%s „%s“ wurde angelegt', syncro.issue_key(new.project_id, new.number), new.title));
    elsif tg_op = 'DELETE' then
        perform syncro.log_activity(old.project_id, 'issue_deleted', old.id,
            format('%s „%s“ wurde gelöscht', syncro.issue_key(old.project_id, old.number), old.title));
    else
        if new.status is distinct from old.status then
            perform syncro.log_activity(new.project_id, 'issue_moved', new.id,
                format('%s wurde nach „%s“ verschoben', syncro.issue_key(new.project_id, new.number),
                    case new.status
                        when 'Todo' then 'To do'
                        when 'InProgress' then 'In Arbeit'
                        when 'Done' then 'Erledigt'
                        else new.status
                    end));
        end if;

        if new.assignee_id is distinct from old.assignee_id and new.assignee_id is not null then
            select coalesce(nullif(trim(p.vorname || ' ' || p.nachname), ''), nullif(p.username, ''), p.email)
            into v_assignee
            from syncro.profiles p
            where p.id = new.assignee_id;

            perform syncro.log_activity(new.project_id, 'issue_assigned', new.id,
                format('%s wurde %s zugewiesen', syncro.issue_key(new.project_id, new.number), v_assignee));
        end if;
    end if;
    return null;
end;
$$;

drop trigger if exists issues_log_activity on syncro.issues;
create trigger issues_log_activity
    after insert or update or delete on syncro.issues
    for each row execute function syncro.log_issue_activity();

-- ── Trigger: issue_comments ──────────────────────────────────────────────────

create or replace function syncro.log_comment_activity()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_issue syncro.issues%rowtype;
begin
    select * into v_issue from syncro.issues where id = new.issue_id;
    perform syncro.log_activity(v_issue.project_id, 'comment_added', new.issue_id,
        format('Neuer Kommentar zu %s', syncro.issue_key(v_issue.project_id, v_issue.number)));
    return null;
end;
$$;

drop trigger if exists issue_comments_log_activity on syncro.issue_comments;
create trigger issue_comments_log_activity
    after insert on syncro.issue_comments
    for each row execute function syncro.log_comment_activity();
