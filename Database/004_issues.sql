-- 004: Issues (Board-Karten)
-- status entspricht den Namen des C#-Enums Syncro.Core.Models.Enums.Column.
-- number wird pro Projekt fortlaufend vergeben (Anzeige z. B. SYN-12).

create table if not exists syncro.issues (
    id uuid primary key default gen_random_uuid(),
    project_id uuid not null references syncro.projects(id) on delete cascade,
    number integer not null default 0,
    title text not null check (char_length(trim(title)) > 0),
    description text,
    type text not null default 'Task'
        check (type in ('Feature', 'Bug', 'Task', 'Improvement', 'Documentation')),
    priority text not null default 'Medium'
        check (priority in ('Low', 'Medium', 'High', 'Critical')),
    status text not null default 'Backlog'
        check (status in ('Backlog', 'Todo', 'InProgress', 'Review', 'Done')),
    position double precision not null default 0,
    assignee_id uuid references syncro.profiles(id) on delete set null,
    due_date date,
    progress integer check (progress between 0 and 100),
    tags text[] not null default '{}',
    comment_count integer not null default 0,
    created_by uuid references syncro.profiles(id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (project_id, number)
);

-- Anzahl Anhänge, gepflegt von 008_attachments.sql
alter table syncro.issues add column if not exists attachment_count integer not null default 0;

create index if not exists issues_project_status_position_idx on syncro.issues (project_id, status, position);
create index if not exists issues_assignee_id_idx on syncro.issues (assignee_id);
create index if not exists issues_due_date_idx on syncro.issues (due_date) where due_date is not null;

-- Nummer vergeben, Ersteller setzen, unveränderliche Felder schützen
create or replace function syncro.issues_before_write()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    if tg_op = 'INSERT' then
        perform set_config('syncro.allow_counter_update', 'on', true);
        update syncro.projects
        set issue_counter = issue_counter + 1
        where id = new.project_id
        returning issue_counter into new.number;
        perform set_config('syncro.allow_counter_update', 'off', true);

        new.created_by = auth.uid();
        new.created_at = now();
        new.comment_count = 0;
        new.attachment_count = 0;
    else
        new.project_id = old.project_id;
        new.number = old.number;
        new.created_by = old.created_by;
        new.created_at = old.created_at;
        -- comment_count und attachment_count werden nur von Triggern gepflegt
        if current_setting('syncro.allow_comment_count_update', true) is distinct from 'on' then
            new.comment_count = old.comment_count;
        end if;
        if current_setting('syncro.allow_attachment_count_update', true) is distinct from 'on' then
            new.attachment_count = old.attachment_count;
        end if;
    end if;
    return new;
end;
$$;

drop trigger if exists issues_before_write on syncro.issues;
create trigger issues_before_write
    before insert or update on syncro.issues
    for each row execute function syncro.issues_before_write();

drop trigger if exists issues_set_updated_at on syncro.issues;
create trigger issues_set_updated_at
    before update on syncro.issues
    for each row execute function syncro.set_updated_at();

alter table syncro.issues enable row level security;

drop policy if exists "Members can read issues" on syncro.issues;
create policy "Members can read issues"
    on syncro.issues for select
    to authenticated
    using (syncro.is_project_member(project_id));

drop policy if exists "Editors can create issues" on syncro.issues;
create policy "Editors can create issues"
    on syncro.issues for insert
    to authenticated
    with check (
        syncro.can_edit_project(project_id)
        and (assignee_id is null or syncro.is_user_in_project(project_id, assignee_id))
    );

drop policy if exists "Editors can update issues" on syncro.issues;
create policy "Editors can update issues"
    on syncro.issues for update
    to authenticated
    using (syncro.can_edit_project(project_id))
    with check (
        syncro.can_edit_project(project_id)
        and (assignee_id is null or syncro.is_user_in_project(project_id, assignee_id))
    );

drop policy if exists "Editors can delete issues" on syncro.issues;
create policy "Editors can delete issues"
    on syncro.issues for delete
    to authenticated
    using (syncro.can_edit_project(project_id));

-- Verlässt jemand ein Projekt, werden seine Issues dort freigegeben
create or replace function syncro.unassign_issues_of_removed_member()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    update syncro.issues
    set assignee_id = null
    where project_id = old.project_id and assignee_id = old.user_id;
    return old;
end;
$$;

drop trigger if exists project_members_unassign_issues on syncro.project_members;
create trigger project_members_unassign_issues
    after delete on syncro.project_members
    for each row execute function syncro.unassign_issues_of_removed_member();
