-- 002: Projekte
-- user_id ist der Eigentümer. Zugriffs-Policies basieren auf der Mitgliedschaft
-- und werden in 003_project_members.sql definiert.
-- Idempotent: läuft auch auf einer Datenbank, in der die alte projects.sql bereits ausgeführt wurde.

create table if not exists public.projects (
    id uuid primary key,
    user_id uuid not null references auth.users(id) on delete cascade,
    name text not null check (char_length(trim(name)) > 0),
    description text,
    status text not null default 'Aktiv',
    color text not null default '#0f766e',
    created_at timestamptz not null default now()
);

alter table public.projects alter column id set default gen_random_uuid();

alter table public.projects add column if not exists key text;
alter table public.projects add column if not exists issue_counter integer not null default 0;
alter table public.projects add column if not exists updated_at timestamptz not null default now();

-- Kürzel für Issue-Nummern (z. B. SYN-12), aus dem Namen abgeleitet falls nicht gesetzt
create or replace function public.project_key_from_name(p_name text)
returns text
language sql
immutable
as $$
    select coalesce(
        nullif(upper(left(regexp_replace(p_name, '[^A-Za-z0-9]', '', 'g'), 3)), ''),
        'PRJ'
    );
$$;

update public.projects
set key = public.project_key_from_name(name)
where key is null or key !~ '^[A-Z][A-Z0-9]{1,9}$';

-- Namen, die mit einer Ziffer beginnen, bekommen ein P vorangestellt
update public.projects
set key = left('P' || key, 10)
where key !~ '^[A-Z]';

alter table public.projects alter column key set not null;

alter table public.projects drop constraint if exists projects_key_format;
alter table public.projects add constraint projects_key_format
    check (key ~ '^[A-Z][A-Z0-9]{1,9}$');

alter table public.projects drop constraint if exists projects_status_check;
alter table public.projects add constraint projects_status_check
    check (status in ('Aktiv', 'Pausiert', 'Abgeschlossen', 'Archiviert'));

alter table public.projects drop constraint if exists projects_color_format;
alter table public.projects add constraint projects_color_format
    check (color ~ '^#[0-9A-Fa-f]{6}$');

create index if not exists projects_user_id_idx on public.projects (user_id);

-- Schreibschutz für Eigentümer, Zähler und Erstellungsdatum; Key-Default beim Anlegen
create or replace function public.projects_before_write()
returns trigger
language plpgsql
as $$
begin
    if tg_op = 'INSERT' then
        new.issue_counter = 0;
        if new.key is null or new.key = '' then
            new.key = public.project_key_from_name(new.name);
            if new.key !~ '^[A-Z]' then
                new.key = left('P' || new.key, 10);
            end if;
        end if;
    else
        new.user_id = old.user_id;
        new.created_at = old.created_at;
        -- issue_counter wird nur vom Issue-Trigger hochgezählt
        if current_setting('syncro.allow_counter_update', true) is distinct from 'on' then
            new.issue_counter = old.issue_counter;
        end if;
    end if;
    return new;
end;
$$;

drop trigger if exists projects_before_write on public.projects;
create trigger projects_before_write
    before insert or update on public.projects
    for each row execute function public.projects_before_write();

drop trigger if exists projects_set_updated_at on public.projects;
create trigger projects_set_updated_at
    before update on public.projects
    for each row execute function public.set_updated_at();

alter table public.projects enable row level security;

-- Alte Eigentümer-Policies aus projects.sql entfernen (werden in 003 ersetzt)
drop policy if exists "Users can read their own projects" on public.projects;
drop policy if exists "Users can create their own projects" on public.projects;
drop policy if exists "Users can update their own projects" on public.projects;
