-- 005: Kommentare zu Issues
-- issues.comment_count wird per Trigger aktuell gehalten (CommentCount auf den Board-Karten).

create table if not exists public.issue_comments (
    id uuid primary key default gen_random_uuid(),
    issue_id uuid not null references public.issues(id) on delete cascade,
    user_id uuid references public.profiles(id) on delete set null,
    body text not null check (char_length(trim(body)) > 0),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists issue_comments_issue_id_idx on public.issue_comments (issue_id, created_at);

create or replace function public.issue_project_id(p_issue_id uuid)
returns uuid
language sql
stable
security definer
set search_path = ''
as $$
    select project_id from public.issues where id = p_issue_id;
$$;

revoke execute on function public.issue_project_id(uuid) from public, anon;
grant execute on function public.issue_project_id(uuid) to authenticated;

create or replace function public.issue_comments_before_write()
returns trigger
language plpgsql
as $$
begin
    if tg_op = 'INSERT' then
        new.user_id = auth.uid();
        new.created_at = now();
    else
        new.issue_id = old.issue_id;
        new.user_id = old.user_id;
        new.created_at = old.created_at;
    end if;
    return new;
end;
$$;

drop trigger if exists issue_comments_before_write on public.issue_comments;
create trigger issue_comments_before_write
    before insert or update on public.issue_comments
    for each row execute function public.issue_comments_before_write();

drop trigger if exists issue_comments_set_updated_at on public.issue_comments;
create trigger issue_comments_set_updated_at
    before update on public.issue_comments
    for each row execute function public.set_updated_at();

create or replace function public.update_issue_comment_count()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_issue_id uuid = coalesce(new.issue_id, old.issue_id);
begin
    perform set_config('syncro.allow_comment_count_update', 'on', true);
    update public.issues
    set comment_count = (select count(*) from public.issue_comments c where c.issue_id = v_issue_id)
    where id = v_issue_id;
    perform set_config('syncro.allow_comment_count_update', 'off', true);
    return null;
end;
$$;

drop trigger if exists issue_comments_update_count on public.issue_comments;
create trigger issue_comments_update_count
    after insert or delete on public.issue_comments
    for each row execute function public.update_issue_comment_count();

alter table public.issue_comments enable row level security;

drop policy if exists "Members can read comments" on public.issue_comments;
create policy "Members can read comments"
    on public.issue_comments for select
    to authenticated
    using (public.is_project_member(public.issue_project_id(issue_id)));

drop policy if exists "Editors can write comments" on public.issue_comments;
create policy "Editors can write comments"
    on public.issue_comments for insert
    to authenticated
    with check (public.can_edit_project(public.issue_project_id(issue_id)));

drop policy if exists "Authors can edit their comments" on public.issue_comments;
create policy "Authors can edit their comments"
    on public.issue_comments for update
    to authenticated
    using (user_id = (select auth.uid()))
    with check (user_id = (select auth.uid()));

drop policy if exists "Authors and admins can delete comments" on public.issue_comments;
create policy "Authors and admins can delete comments"
    on public.issue_comments for delete
    to authenticated
    using (
        user_id = (select auth.uid())
        or public.is_project_admin(public.issue_project_id(issue_id))
    );
