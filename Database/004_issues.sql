-- 004: Issues (Board-Karten)
-- status entspricht den Namen des C#-Enums Syncro.Core.Models.Enums.Column.
-- number wird pro Projekt fortlaufend vergeben (Anzeige z. B. SYN-12).

create table if not exists public.issues (
    id uuid primary key default gen_random_uuid(),
    project_id uuid not null references public.projects(id) on delete cascade,
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
    assignee_id uuid references public.profiles(id) on delete set null,
    due_date date,
    progress integer check (progress between 0 and 100),
    tags text[] not null default '{}',
    comment_count integer not null default 0,
    created_by uuid references public.profiles(id) on delete set null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    unique (project_id, number)
);

-- Anzahl Anhänge, gepflegt von 008_attachments.sql
alter table public.issues add column if not exists attachment_count integer not null default 0;

create index if not exists issues_project_status_position_idx on public.issues (project_id, status, position);
create index if not exists issues_assignee_id_idx on public.issues (assignee_id);
create index if not exists issues_due_date_idx on public.issues (due_date) where due_date is not null;

-- Nummer vergeben, Ersteller setzen, unveränderliche Felder schützen
create or replace function public.issues_before_write()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    if tg_op = 'INSERT' then
        perform set_config('syncro.allow_counter_update', 'on', true);
        update public.projects
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

drop trigger if exists issues_before_write on public.issues;
create trigger issues_before_write
    before insert or update on public.issues
    for each row execute function public.issues_before_write();

drop trigger if exists issues_set_updated_at on public.issues;
create trigger issues_set_updated_at
    before update on public.issues
    for each row execute function public.set_updated_at();

alter table public.issues enable row level security;

drop policy if exists "Members can read issues" on public.issues;
create policy "Members can read issues"
    on public.issues for select
    to authenticated
    using (public.is_project_member(project_id));

drop policy if exists "Editors can create issues" on public.issues;
create policy "Editors can create issues"
    on public.issues for insert
    to authenticated
    with check (
        public.can_edit_project(project_id)
        and (assignee_id is null or public.is_user_in_project(project_id, assignee_id))
    );

drop policy if exists "Editors can update issues" on public.issues;
create policy "Editors can update issues"
    on public.issues for update
    to authenticated
    using (public.can_edit_project(project_id))
    with check (
        public.can_edit_project(project_id)
        and (assignee_id is null or public.is_user_in_project(project_id, assignee_id))
    );

drop policy if exists "Editors can delete issues" on public.issues;
create policy "Editors can delete issues"
    on public.issues for delete
    to authenticated
    using (public.can_edit_project(project_id));

-- Verlässt jemand ein Projekt, werden seine Issues dort freigegeben
create or replace function public.unassign_issues_of_removed_member()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    update public.issues
    set assignee_id = null
    where project_id = old.project_id and assignee_id = old.user_id;
    return old;
end;
$$;

drop trigger if exists project_members_unassign_issues on public.project_members;
create trigger project_members_unassign_issues
    after delete on public.project_members
    for each row execute function public.unassign_issues_of_removed_member();
