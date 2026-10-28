-- 003: Projektmitglieder, Rollen und Zugriffs-Policies für Projekte und Profile
-- Rollen: admin (alles inkl. Mitglieder verwalten), member (Inhalte bearbeiten), viewer (nur lesen)
-- Der Projekteigentümer ist immer admin und kann nicht entfernt werden.

create table if not exists public.project_members (
    id uuid primary key default gen_random_uuid(),
    project_id uuid not null references public.projects(id) on delete cascade,
    user_id uuid not null references public.profiles(id) on delete cascade,
    role text not null default 'member' check (role in ('admin', 'member', 'viewer')),
    created_at timestamptz not null default now(),
    unique (project_id, user_id)
);

create index if not exists project_members_user_id_idx on public.project_members (user_id);

-- ── Hilfsfunktionen (security definer, damit Policies nicht rekursiv werden) ──

create or replace function public.project_role(p_project_id uuid)
returns text
language sql
stable
security definer
set search_path = ''
as $$
    select case
        when exists (
            select 1 from public.projects p
            where p.id = p_project_id and p.user_id = auth.uid()
        ) then 'admin'
        else (
            select m.role from public.project_members m
            where m.project_id = p_project_id and m.user_id = auth.uid()
        )
    end;
$$;

create or replace function public.is_project_member(p_project_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select public.project_role(p_project_id) is not null;
$$;

create or replace function public.can_edit_project(p_project_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select coalesce(public.project_role(p_project_id) in ('admin', 'member'), false);
$$;

create or replace function public.is_project_admin(p_project_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select coalesce(public.project_role(p_project_id) = 'admin', false);
$$;

-- Prüft, ob ein beliebiger Nutzer Mitglied eines Projekts ist (z. B. für Assignees)
create or replace function public.is_user_in_project(p_project_id uuid, p_user_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.project_members m
        where m.project_id = p_project_id and m.user_id = p_user_id
    ) or exists (
        select 1 from public.projects p
        where p.id = p_project_id and p.user_id = p_user_id
    );
$$;

create or replace function public.is_project_owner(p_project_id uuid, p_user_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.projects p
        where p.id = p_project_id and p.user_id = p_user_id
    );
$$;

-- Teilen sich der aktuelle Nutzer und p_user_id mindestens ein Projekt?
create or replace function public.shares_project_with(p_user_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.project_members mine
        join public.project_members theirs on theirs.project_id = mine.project_id
        where mine.user_id = auth.uid() and theirs.user_id = p_user_id
    );
$$;

-- Profil per E-Mail finden, um Nutzer in ein Projekt einzuladen
create or replace function public.find_profile_by_email(p_email text)
returns table (id uuid, email text, username text, vorname text, nachname text, avatar_color text)
language sql
stable
security definer
set search_path = ''
as $$
    select p.id, p.email, p.username, p.vorname, p.nachname, p.avatar_color
    from public.profiles p
    where auth.uid() is not null
      and lower(p.email) = lower(trim(p_email));
$$;

revoke execute on function public.project_role(uuid) from public, anon;
revoke execute on function public.is_project_member(uuid) from public, anon;
revoke execute on function public.can_edit_project(uuid) from public, anon;
revoke execute on function public.is_project_admin(uuid) from public, anon;
revoke execute on function public.is_user_in_project(uuid, uuid) from public, anon;
revoke execute on function public.is_project_owner(uuid, uuid) from public, anon;
revoke execute on function public.shares_project_with(uuid) from public, anon;
revoke execute on function public.find_profile_by_email(text) from public, anon;

grant execute on function public.project_role(uuid) to authenticated;
grant execute on function public.is_project_member(uuid) to authenticated;
grant execute on function public.can_edit_project(uuid) to authenticated;
grant execute on function public.is_project_admin(uuid) to authenticated;
grant execute on function public.is_user_in_project(uuid, uuid) to authenticated;
grant execute on function public.is_project_owner(uuid, uuid) to authenticated;
grant execute on function public.shares_project_with(uuid) to authenticated;
grant execute on function public.find_profile_by_email(text) to authenticated;

-- ── Eigentümer automatisch als admin eintragen ───────────────────────────────

create or replace function public.add_project_owner_as_member()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    insert into public.project_members (project_id, user_id, role)
    values (new.id, new.user_id, 'admin')
    on conflict (project_id, user_id) do update set role = 'admin';
    return new;
end;
$$;

drop trigger if exists projects_add_owner_member on public.projects;
create trigger projects_add_owner_member
    after insert on public.projects
    for each row execute function public.add_project_owner_as_member();

-- Bestehende Projekte nachziehen
insert into public.project_members (project_id, user_id, role)
select p.id, p.user_id, 'admin'
from public.projects p
join public.profiles pr on pr.id = p.user_id
on conflict (project_id, user_id) do update set role = 'admin';

-- ── Policies: project_members ────────────────────────────────────────────────

alter table public.project_members enable row level security;

drop policy if exists "Members can read project members" on public.project_members;
create policy "Members can read project members"
    on public.project_members for select
    to authenticated
    using (public.is_project_member(project_id));

drop policy if exists "Admins can add project members" on public.project_members;
create policy "Admins can add project members"
    on public.project_members for insert
    to authenticated
    with check (public.is_project_admin(project_id));

drop policy if exists "Admins can change member roles" on public.project_members;
create policy "Admins can change member roles"
    on public.project_members for update
    to authenticated
    using (public.is_project_admin(project_id) and not public.is_project_owner(project_id, user_id))
    with check (public.is_project_admin(project_id) and not public.is_project_owner(project_id, user_id));

drop policy if exists "Admins can remove members, members can leave" on public.project_members;
create policy "Admins can remove members, members can leave"
    on public.project_members for delete
    to authenticated
    using (
        not public.is_project_owner(project_id, user_id)
        and (public.is_project_admin(project_id) or user_id = (select auth.uid()))
    );

-- ── Policies: projects ───────────────────────────────────────────────────────

drop policy if exists "Members can read projects" on public.projects;
create policy "Members can read projects"
    on public.projects for select
    to authenticated
    using (user_id = (select auth.uid()) or public.is_project_member(id));

drop policy if exists "Users can create their own projects" on public.projects;
create policy "Users can create their own projects"
    on public.projects for insert
    to authenticated
    with check (user_id = (select auth.uid()));

drop policy if exists "Admins can update projects" on public.projects;
create policy "Admins can update projects"
    on public.projects for update
    to authenticated
    using (public.is_project_admin(id))
    with check (public.is_project_admin(id));

drop policy if exists "Owners can delete projects" on public.projects;
create policy "Owners can delete projects"
    on public.projects for delete
    to authenticated
    using (user_id = (select auth.uid()));

-- ── Policies: profiles (ergänzt 001) ─────────────────────────────────────────

drop policy if exists "Users can read profiles of project co-members" on public.profiles;
create policy "Users can read profiles of project co-members"
    on public.profiles for select
    to authenticated
    using (public.shares_project_with(id));
