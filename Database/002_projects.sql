-- 002: Projekte
-- user_id ist der Eigentümer. Zugriffs-Policies basieren auf der Mitgliedschaft
-- und werden in 003_project_members.sql definiert.
-- Idempotent: läuft auch auf einer Datenbank, in der die alte projects.sql bereits ausgeführt
-- oder die Tabelle von Hand (z. B. im Supabase Table Editor) angelegt wurde.

create table if not exists public.projects (
    id uuid primary key,
    user_id uuid not null references auth.users(id) on delete cascade,
    name text not null check (char_length(trim(name)) > 0),
    description text,
    status text not null default 'Aktiv',
    color text not null default '#0f766e',
    created_at timestamptz not null default now()
);

-- ── Bestehende Tabelle auf den erwarteten Stand bringen ─────────────────────

-- Alle bisherigen Policies entfernen: Sie können Typänderungen blockieren und werden
-- in 003_project_members.sql durch die Policies auf Basis der Mitgliedschaft ersetzt
do $$
declare
    v_policy record;
begin
    for v_policy in select policyname from pg_policies where schemaname = 'public' and tablename = 'projects' loop
        execute format('drop policy %I on public.projects', v_policy.policyname);
    end loop;
end;
$$;

alter table public.projects add column if not exists name text;
alter table public.projects add column if not exists description text;
alter table public.projects add column if not exists status text not null default 'Aktiv';
alter table public.projects add column if not exists color text not null default '#0f766e';
alter table public.projects add column if not exists created_at timestamptz not null default now();
alter table public.projects add column if not exists user_id uuid;

update public.projects set name = 'Projekt' where name is null or trim(name) = '';
alter table public.projects alter column name set not null;

do $$
declare
    v_id_type text;
    v_user_id_type text;
begin
    select data_type into v_id_type
    from information_schema.columns
    where table_schema = 'public' and table_name = 'projects' and column_name = 'id';

    if v_id_type is distinct from 'uuid' then
        raise exception using
            message = format('projects.id hat den Typ %s, Syncro erwartet uuid.', coalesce(v_id_type, 'keinen (Spalte fehlt)')),
            hint = 'Ist die Tabelle leer oder entbehrlich: "drop table public.projects cascade;" ausführen und das Skript erneut starten.';
    end if;

    -- user_id wurde im Table Editor evtl. als text angelegt
    select data_type into v_user_id_type
    from information_schema.columns
    where table_schema = 'public' and table_name = 'projects' and column_name = 'user_id';

    if v_user_id_type <> 'uuid' then
        alter table public.projects alter column user_id type uuid using nullif(trim(user_id::text), '')::uuid;
    end if;

    if not exists (
        select 1 from pg_constraint
        where conrelid = 'public.projects'::regclass and contype = 'f' and conname = 'projects_user_id_fkey'
    ) then
        alter table public.projects add constraint projects_user_id_fkey
            foreign key (user_id) references auth.users(id) on delete cascade;
    end if;

    -- Projekte ohne Eigentümer sind über RLS nicht erreichbar; NOT NULL nur, wenn es keine gibt
    if exists (select 1 from public.projects where user_id is null) then
        raise notice 'Es gibt Projekte ohne user_id. Diese sind erst sichtbar, wenn ihnen ein Eigentümer zugewiesen wird.';
    else
        alter table public.projects alter column user_id set not null;
    end if;
end;
$$;

alter table public.projects alter column id set default gen_random_uuid();

alter table public.projects add column if not exists key text;
alter table public.projects add column if not exists issue_counter integer not null default 0;
alter table public.projects add column if not exists updated_at timestamptz not null default now();

-- Kürzel für Issue-Nummern (z. B. SYN-12), aus dem Namen abgeleitet falls nicht gesetzt.
-- Liefert immer ein gültiges Kürzel: beginnt mit einem Buchstaben, 2–10 Zeichen.
--   "Syncro App" → SYN, "123 Test" → P123, "X" → XP, "Ölwechsel" → LWE, "" → PRJ
create or replace function public.project_key_from_name(p_name text)
returns text
language plpgsql
immutable
as $$
declare
    v_key text = upper(left(regexp_replace(coalesce(p_name, ''), '[^A-Za-z0-9]', '', 'g'), 3));
begin
    if v_key = '' then
        return 'PRJ';
    end if;
    if v_key !~ '^[A-Z]' then
        v_key = 'P' || v_key;
    end if;
    if length(v_key) < 2 then
        v_key = v_key || 'P';
    end if;
    return v_key;
end;
$$;

-- Bestandsdaten bereinigen, bevor die Prüfregeln greifen
-- (der frühere Editor erlaubte freie Eingaben für Status und Farbe)
update public.projects
set key = public.project_key_from_name(name)
where key is null or key !~ '^[A-Z][A-Z0-9]{1,9}$';

update public.projects
set status = 'Aktiv'
where status is null or status not in ('Aktiv', 'Pausiert', 'Abgeschlossen', 'Archiviert');

update public.projects
set color = '#0f766e'
where color is null or color !~ '^#[0-9A-Fa-f]{6}$';

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

-- Policies folgen in 003_project_members.sql
alter table public.projects enable row level security;
