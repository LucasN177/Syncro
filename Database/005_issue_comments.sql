-- 005: Kommentare zu Issues
-- issues.comment_count wird per Trigger aktuell gehalten (CommentCount auf den Board-Karten).

create table if not exists syncro.issue_comments (
    id uuid primary key default gen_random_uuid(),
    issue_id uuid not null references syncro.issues(id) on delete cascade,
    user_id uuid references syncro.profiles(id) on delete set null,
    body text not null check (char_length(trim(body)) > 0),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index if not exists issue_comments_issue_id_idx on syncro.issue_comments (issue_id, created_at);

create or replace function syncro.issue_project_id(p_issue_id uuid)
returns uuid
language sql
stable
security definer
set search_path = ''
as $$
    select project_id from syncro.issues where id = p_issue_id;
$$;

revoke execute on function syncro.issue_project_id(uuid) from public, anon;
grant execute on function syncro.issue_project_id(uuid) to authenticated;

create or replace function syncro.issue_comments_before_write()
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

drop trigger if exists issue_comments_before_write on syncro.issue_comments;
create trigger issue_comments_before_write
    before insert or update on syncro.issue_comments
    for each row execute function syncro.issue_comments_before_write();

drop trigger if exists issue_comments_set_updated_at on syncro.issue_comments;
create trigger issue_comments_set_updated_at
    before update on syncro.issue_comments
    for each row execute function syncro.set_updated_at();

create or replace function syncro.update_issue_comment_count()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_issue_id uuid = coalesce(new.issue_id, old.issue_id);
begin
    perform set_config('syncro.allow_comment_count_update', 'on', true);
    update syncro.issues
    set comment_count = (select count(*) from syncro.issue_comments c where c.issue_id = v_issue_id)
    where id = v_issue_id;
    perform set_config('syncro.allow_comment_count_update', 'off', true);
    return null;
end;
$$;

drop trigger if exists issue_comments_update_count on syncro.issue_comments;
create trigger issue_comments_update_count
    after insert or delete on syncro.issue_comments
    for each row execute function syncro.update_issue_comment_count();

alter table syncro.issue_comments enable row level security;

drop policy if exists "Members can read comments" on syncro.issue_comments;
create policy "Members can read comments"
    on syncro.issue_comments for select
    to authenticated
    using (syncro.is_project_member(syncro.issue_project_id(issue_id)));

drop policy if exists "Editors can write comments" on syncro.issue_comments;
create policy "Editors can write comments"
    on syncro.issue_comments for insert
    to authenticated
    with check (syncro.can_edit_project(syncro.issue_project_id(issue_id)));

drop policy if exists "Authors can edit their comments" on syncro.issue_comments;
create policy "Authors can edit their comments"
    on syncro.issue_comments for update
    to authenticated
    using (user_id = (select auth.uid()))
    with check (user_id = (select auth.uid()));

drop policy if exists "Authors and admins can delete comments" on syncro.issue_comments;
create policy "Authors and admins can delete comments"
    on syncro.issue_comments for delete
    to authenticated
    using (
        user_id = (select auth.uid())
        or syncro.is_project_admin(syncro.issue_project_id(issue_id))
    );
