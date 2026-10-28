-- 008: Dateianhänge an Issues über Supabase Storage
-- Dateien liegen im privaten Bucket "attachments" unter {project_id}/{issue_id}/{datei}.
-- Voraussetzung: 004_issues.sql in der aktuellen Version (Spalte attachment_count).

alter table public.issues add column if not exists attachment_count integer not null default 0;

create table if not exists public.issue_attachments (
    id uuid primary key default gen_random_uuid(),
    issue_id uuid not null references public.issues(id) on delete cascade,
    project_id uuid not null references public.projects(id) on delete cascade,
    file_name text not null check (char_length(file_name) between 1 and 255),
    storage_path text not null unique,
    content_type text,
    size_bytes bigint not null default 0 check (size_bytes between 0 and 10485760),
    uploaded_by uuid references public.profiles(id) on delete set null,
    created_at timestamptz not null default now()
);

create index if not exists issue_attachments_issue_id_idx on public.issue_attachments (issue_id, created_at);

-- Projekt und Uploader setzen, Pfad muss zum Issue passen
create or replace function public.issue_attachments_before_insert()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    select project_id into new.project_id from public.issues where id = new.issue_id;
    new.uploaded_by = auth.uid();
    new.created_at = now();

    if new.storage_path not like new.project_id::text || '/' || new.issue_id::text || '/%' then
        raise exception 'storage_path muss mit {project_id}/{issue_id}/ beginnen';
    end if;
    return new;
end;
$$;

drop trigger if exists issue_attachments_before_insert on public.issue_attachments;
create trigger issue_attachments_before_insert
    before insert on public.issue_attachments
    for each row execute function public.issue_attachments_before_insert();

create or replace function public.update_issue_attachment_count()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_issue_id uuid = coalesce(new.issue_id, old.issue_id);
begin
    perform set_config('syncro.allow_attachment_count_update', 'on', true);
    update public.issues
    set attachment_count = (select count(*) from public.issue_attachments a where a.issue_id = v_issue_id)
    where id = v_issue_id;
    perform set_config('syncro.allow_attachment_count_update', 'off', true);
    return null;
end;
$$;

drop trigger if exists issue_attachments_update_count on public.issue_attachments;
create trigger issue_attachments_update_count
    after insert or delete on public.issue_attachments
    for each row execute function public.update_issue_attachment_count();

alter table public.issue_attachments enable row level security;

drop policy if exists "Members can read attachments" on public.issue_attachments;
create policy "Members can read attachments"
    on public.issue_attachments for select
    to authenticated
    using (public.is_project_member(project_id));

drop policy if exists "Editors can add attachments" on public.issue_attachments;
create policy "Editors can add attachments"
    on public.issue_attachments for insert
    to authenticated
    with check (public.can_edit_project(project_id));

drop policy if exists "Editors can delete attachments" on public.issue_attachments;
create policy "Editors can delete attachments"
    on public.issue_attachments for delete
    to authenticated
    using (public.can_edit_project(project_id));

-- ── Storage ──────────────────────────────────────────────────────────────────

insert into storage.buckets (id, name, public, file_size_limit)
values ('attachments', 'attachments', false, 10485760)
on conflict (id) do update set public = false, file_size_limit = 10485760;

-- Projekt-Id aus dem ersten Pfadsegment; null bei ungültigen Pfaden
create or replace function public.storage_project_id(p_name text)
returns uuid
language sql
immutable
as $$
    select case
        when split_part(p_name, '/', 1) ~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
            then split_part(p_name, '/', 1)::uuid
    end;
$$;

drop policy if exists "Project members can read attachments" on storage.objects;
create policy "Project members can read attachments"
    on storage.objects for select
    to authenticated
    using (bucket_id = 'attachments' and public.is_project_member(public.storage_project_id(name)));

drop policy if exists "Project editors can upload attachments" on storage.objects;
create policy "Project editors can upload attachments"
    on storage.objects for insert
    to authenticated
    with check (bucket_id = 'attachments' and public.can_edit_project(public.storage_project_id(name)));

drop policy if exists "Project editors can delete attachments" on storage.objects;
create policy "Project editors can delete attachments"
    on storage.objects for delete
    to authenticated
    using (bucket_id = 'attachments' and public.can_edit_project(public.storage_project_id(name)));
