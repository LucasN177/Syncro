-- ============================================================================
-- Syncro: komplettes Datenbank-Setup für Supabase
--
-- Generiert aus den Migrationen 000–010 mit build-setup-all.sh.
-- Nicht von Hand bearbeiten: Änderungen in den Einzeldateien machen und neu generieren.
--
-- Im Supabase SQL-Editor komplett ausführen. Das Skript ist idempotent und
-- übernimmt auch eine Datenbank, in der die frühere projects.sql lief.
-- ============================================================================


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 000_preflight.sql
-- └──────────────────────────────────────────────────────────────────────────

-- 000: Vorabprüfung für Datenbanken mit Resten eines früheren Setups
--
-- Teil 1: Gleichnamige Funktionen entfernen, die abweichen
-- "create or replace function" kann Rückgabetyp und Parameternamen einer bestehenden Funktion
-- nicht ändern (Fehler 42P13). Existiert eine Funktion mit demselben Namen und denselben
-- Parametertypen, aber anderer Definition, wird sie hier vorher gelöscht.
-- Funktionen, die bereits der erwarteten Definition entsprechen, bleiben unverändert.
--
-- Die Liste muss alle Funktionen aus 001–010 enthalten; Database/tests/run-tests.sh prüft das.

do $$
declare
    v_expected record;
    v_existing record;
begin
    for v_expected in
        select * from (values
            -- 001
            ('set_updated_at', '', '', 'trigger'),
            ('handle_new_user', '', '', 'trigger'),
            ('handle_user_email_changed', '', '', 'trigger'),
            -- 002
            ('project_key_from_name', 'text', 'p_name text', 'text'),
            ('projects_before_write', '', '', 'trigger'),
            -- 003
            ('project_role', 'uuid', 'p_project_id uuid', 'text'),
            ('is_project_member', 'uuid', 'p_project_id uuid', 'boolean'),
            ('can_edit_project', 'uuid', 'p_project_id uuid', 'boolean'),
            ('is_project_admin', 'uuid', 'p_project_id uuid', 'boolean'),
            ('is_user_in_project', 'uuid, uuid', 'p_project_id uuid, p_user_id uuid', 'boolean'),
            ('is_project_owner', 'uuid, uuid', 'p_project_id uuid, p_user_id uuid', 'boolean'),
            ('shares_project_with', 'uuid', 'p_user_id uuid', 'boolean'),
            ('find_profile_by_email', 'text', 'p_email text',
                'TABLE(id uuid, email text, username text, vorname text, nachname text, avatar_color text)'),
            ('add_project_owner_as_member', '', '', 'trigger'),
            -- 004
            ('issues_before_write', '', '', 'trigger'),
            ('unassign_issues_of_removed_member', '', '', 'trigger'),
            -- 005
            ('issue_project_id', 'uuid', 'p_issue_id uuid', 'uuid'),
            ('issue_comments_before_write', '', '', 'trigger'),
            ('update_issue_comment_count', '', '', 'trigger'),
            -- 006
            ('can_read_event', 'uuid', 'p_event_id uuid', 'boolean'),
            ('can_edit_event', 'uuid', 'p_event_id uuid', 'boolean'),
            ('events_before_write', '', '', 'trigger'),
            -- 007
            ('log_activity', 'uuid, text, uuid, text', 'p_project_id uuid, p_action text, p_entity_id uuid, p_message text', 'void'),
            ('issue_key', 'uuid, integer', 'p_project_id uuid, p_number integer', 'text'),
            ('log_project_activity', '', '', 'trigger'),
            ('log_member_activity', '', '', 'trigger'),
            ('log_issue_activity', '', '', 'trigger'),
            ('log_comment_activity', '', '', 'trigger'),
            -- 008
            ('issue_attachments_before_insert', '', '', 'trigger'),
            ('update_issue_attachment_count', '', '', 'trigger'),
            ('storage_project_id', 'text', 'p_name text', 'uuid'),
            -- 009
            ('notifications_before_update', '', '', 'trigger'),
            ('profile_display_name', 'uuid', 'p_user_id uuid', 'text'),
            ('notify_user', 'uuid, uuid, uuid, text, text',
                'p_user_id uuid, p_project_id uuid, p_issue_id uuid, p_type text, p_message text', 'void'),
            ('notify_issue_assigned', '', '', 'trigger'),
            ('notify_issue_comment', '', '', 'trigger'),
            ('notify_member_added', '', '', 'trigger')
        ) as expected (name, arg_types, args, result)
    loop
        for v_existing in
            select p.oid::regprocedure as signature,
                   pg_get_function_arguments(p.oid) as args,
                   pg_get_function_result(p.oid) as result
            from pg_proc p
            where p.pronamespace = 'public'::regnamespace
              and p.proname = v_expected.name
              and oidvectortypes(p.proargtypes) = v_expected.arg_types
        loop
            if v_existing.args is distinct from v_expected.args
               or v_existing.result is distinct from v_expected.result then
                raise notice 'Entferne abweichende Funktion % (bisher: (%) returns %)',
                    v_existing.signature, v_existing.args, v_existing.result;
                -- cascade: davon abhängige Policies/Trigger werden von den Migrationen neu angelegt
                execute format('drop function %s cascade', v_existing.signature);
            end if;
        end loop;
    end loop;
end;
$$;

-- ── Teil 2: Tabellen aus früheren Setups prüfen ─────────────────────────────
-- Die Migrationen legen Tabellen mit "create table if not exists" an und übernehmen damit
-- bestehende Tabellen gleichen Namens. Weicht deren Aufbau ab (z. B. project_members.role als
-- Zahl statt Text), würden spätere Schritte mit schwer verständlichen Fehlern abbrechen.
-- Deshalb hier alle Abweichungen auf einmal melden. Daten werden nicht automatisch gelöscht.
-- projects fehlt in der Liste, weil 002_projects.sql diese Tabelle selbst angleicht.
-- Die Liste stammt aus einer frisch migrierten Datenbank; Database/tests/run-tests.sh prüft sie.

do $$
declare
    v_problem record;
    v_problems text = '';
    v_tables text[] = '{}';
begin
    for v_problem in
        with expected (table_name, column_name, data_type, added_by_migration) as (values
            ('profiles', 'id', 'uuid', false),
            ('profiles', 'email', 'text', false),
            ('profiles', 'username', 'text', false),
            ('profiles', 'vorname', 'text', false),
            ('profiles', 'nachname', 'text', false),
            ('profiles', 'avatar_color', 'text', false),
            ('profiles', 'created_at', 'timestamp with time zone', false),
            ('profiles', 'updated_at', 'timestamp with time zone', false),
            ('project_members', 'id', 'uuid', false),
            ('project_members', 'project_id', 'uuid', false),
            ('project_members', 'user_id', 'uuid', false),
            ('project_members', 'role', 'text', false),
            ('project_members', 'created_at', 'timestamp with time zone', false),
            ('issues', 'id', 'uuid', false),
            ('issues', 'project_id', 'uuid', false),
            ('issues', 'number', 'integer', false),
            ('issues', 'title', 'text', false),
            ('issues', 'description', 'text', false),
            ('issues', 'type', 'text', false),
            ('issues', 'priority', 'text', false),
            ('issues', 'status', 'text', false),
            ('issues', 'position', 'double precision', false),
            ('issues', 'assignee_id', 'uuid', false),
            ('issues', 'due_date', 'date', false),
            ('issues', 'progress', 'integer', false),
            ('issues', 'tags', 'ARRAY', false),
            ('issues', 'comment_count', 'integer', false),
            ('issues', 'created_by', 'uuid', false),
            ('issues', 'created_at', 'timestamp with time zone', false),
            ('issues', 'updated_at', 'timestamp with time zone', false),
            ('issues', 'attachment_count', 'integer', true),
            ('issue_comments', 'id', 'uuid', false),
            ('issue_comments', 'issue_id', 'uuid', false),
            ('issue_comments', 'user_id', 'uuid', false),
            ('issue_comments', 'body', 'text', false),
            ('issue_comments', 'created_at', 'timestamp with time zone', false),
            ('issue_comments', 'updated_at', 'timestamp with time zone', false),
            ('events', 'id', 'uuid', false),
            ('events', 'owner_id', 'uuid', false),
            ('events', 'project_id', 'uuid', false),
            ('events', 'title', 'text', false),
            ('events', 'description', 'text', false),
            ('events', 'location', 'text', false),
            ('events', 'category', 'text', false),
            ('events', 'start_at', 'timestamp with time zone', false),
            ('events', 'end_at', 'timestamp with time zone', false),
            ('events', 'created_at', 'timestamp with time zone', false),
            ('events', 'updated_at', 'timestamp with time zone', false),
            ('event_attendees', 'id', 'uuid', false),
            ('event_attendees', 'event_id', 'uuid', false),
            ('event_attendees', 'user_id', 'uuid', false),
            ('event_attendees', 'created_at', 'timestamp with time zone', false),
            ('activity_log', 'id', 'uuid', false),
            ('activity_log', 'project_id', 'uuid', false),
            ('activity_log', 'actor_id', 'uuid', false),
            ('activity_log', 'action', 'text', false),
            ('activity_log', 'entity_id', 'uuid', false),
            ('activity_log', 'message', 'text', false),
            ('activity_log', 'created_at', 'timestamp with time zone', false),
            ('issue_attachments', 'id', 'uuid', false),
            ('issue_attachments', 'issue_id', 'uuid', false),
            ('issue_attachments', 'project_id', 'uuid', false),
            ('issue_attachments', 'file_name', 'text', false),
            ('issue_attachments', 'storage_path', 'text', false),
            ('issue_attachments', 'content_type', 'text', false),
            ('issue_attachments', 'size_bytes', 'bigint', false),
            ('issue_attachments', 'uploaded_by', 'uuid', false),
            ('issue_attachments', 'created_at', 'timestamp with time zone', false),
            ('notifications', 'id', 'uuid', false),
            ('notifications', 'user_id', 'uuid', false),
            ('notifications', 'project_id', 'uuid', false),
            ('notifications', 'issue_id', 'uuid', false),
            ('notifications', 'actor_id', 'uuid', false),
            ('notifications', 'type', 'text', false),
            ('notifications', 'message', 'text', false),
            ('notifications', 'read_at', 'timestamp with time zone', false),
            ('notifications', 'created_at', 'timestamp with time zone', false)
        ),
        existing_tables as (
            select distinct e.table_name
            from expected e
            join information_schema.tables t on t.table_schema = 'public' and t.table_name = e.table_name
        )
        -- Spalten, die fehlen oder einen anderen Typ haben
        select e.table_name,
               format('%s.%s: erwartet %s, vorhanden %s', e.table_name, e.column_name, e.data_type,
                      coalesce(c.data_type, 'fehlt')) as message
        from expected e
        join existing_tables using (table_name)
        left join information_schema.columns c
            on c.table_schema = 'public' and c.table_name = e.table_name and c.column_name = e.column_name
        where c.data_type is distinct from e.data_type
          and not (e.added_by_migration and c.data_type is null)
        union all
        -- Zusätzliche Pflichtspalten ohne Standardwert verhindern jedes Insert der App
        select c.table_name::text,
               format('%s.%s: zusätzliche Pflichtspalte ohne Standardwert', c.table_name, c.column_name)
        from information_schema.columns c
        join existing_tables et on et.table_name = c.table_name
        where c.table_schema = 'public'
          and c.is_nullable = 'NO' and c.column_default is null
          and c.is_identity = 'NO' and c.is_generated = 'NEVER'
          and not exists (select 1 from expected e where e.table_name = c.table_name and e.column_name = c.column_name)
        order by 1, 2
    loop
        v_problems = v_problems || E'\n  - ' || v_problem.message;
        if not v_problem.table_name = any(v_tables) then
            v_tables = v_tables || v_problem.table_name;
        end if;
    end loop;

    if v_problems <> '' then
        raise exception using
            -- Abweichungen in der Meldung selbst, da der SQL-Editor DETAIL nicht immer anzeigt
            message = format(E'Folgende Tabellen stammen aus einem früheren Setup und passen nicht zu Syncro: %s\nAbweichungen:%s',
                             array_to_string(v_tables, ', '), v_problems),
            hint = format('Werden die Daten darin nicht mehr gebraucht, die Tabellen löschen und das Skript erneut ausführen: drop table if exists %s cascade;',
                          (select string_agg('public.' || quote_ident(t), ', ') from unnest(v_tables) t));
    end if;
end;
$$;


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 001_profiles.sql
-- └──────────────────────────────────────────────────────────────────────────

-- 001: Profile je Supabase-Auth-Nutzer
-- Wird beim Sign-up automatisch per Trigger angelegt.

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

create table if not exists public.profiles (
    id uuid primary key references auth.users(id) on delete cascade,
    email text,
    username text not null default '',
    vorname text not null default '',
    nachname text not null default '',
    avatar_color text not null default '#0f766e',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists profiles_email_key on public.profiles (lower(email));

drop trigger if exists profiles_set_updated_at on public.profiles;
create trigger profiles_set_updated_at
    before update on public.profiles
    for each row execute function public.set_updated_at();

-- Profil beim Sign-up anlegen
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    insert into public.profiles (id, email, username)
    values (new.id, new.email, coalesce(split_part(new.email, '@', 1), ''))
    on conflict (id) do nothing;
    return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_user();

-- E-Mail-Änderungen in Auth ins Profil übernehmen
create or replace function public.handle_user_email_changed()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    update public.profiles set email = new.email where id = new.id;
    return new;
end;
$$;

drop trigger if exists on_auth_user_email_changed on auth.users;
create trigger on_auth_user_email_changed
    after update of email on auth.users
    for each row
    when (old.email is distinct from new.email)
    execute function public.handle_user_email_changed();

-- Bereits registrierte Nutzer nachziehen
insert into public.profiles (id, email, username)
select id, email, coalesce(split_part(email, '@', 1), '')
from auth.users
on conflict (id) do nothing;

alter table public.profiles enable row level security;

-- Lese-Policy für Profile von Projektmitgliedern folgt in 003_project_members.sql
drop policy if exists "Users can read their own profile" on public.profiles;
create policy "Users can read their own profile"
    on public.profiles for select
    to authenticated
    using ((select auth.uid()) = id);

drop policy if exists "Users can update their own profile" on public.profiles;
create policy "Users can update their own profile"
    on public.profiles for update
    to authenticated
    using ((select auth.uid()) = id)
    with check ((select auth.uid()) = id);


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 002_projects.sql
-- └──────────────────────────────────────────────────────────────────────────

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


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 003_project_members.sql
-- └──────────────────────────────────────────────────────────────────────────

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


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 004_issues.sql
-- └──────────────────────────────────────────────────────────────────────────

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


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 005_issue_comments.sql
-- └──────────────────────────────────────────────────────────────────────────

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


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 006_events.sql
-- └──────────────────────────────────────────────────────────────────────────

-- 006: Kalendertermine
-- Ohne project_id ist ein Termin privat (Eigentümer + eingeladene Teilnehmer),
-- mit project_id sehen ihn alle Projektmitglieder.

create table if not exists public.events (
    id uuid primary key default gen_random_uuid(),
    owner_id uuid references public.profiles(id) on delete cascade,
    project_id uuid references public.projects(id) on delete cascade,
    title text not null check (char_length(trim(title)) > 0),
    description text,
    location text,
    category text not null default 'Meeting'
        check (category in ('Meeting', 'Review', 'Release', 'Personal', 'Deadline')),
    start_at timestamptz not null,
    end_at timestamptz not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    check (end_at >= start_at)
);

create index if not exists events_owner_start_idx on public.events (owner_id, start_at);
create index if not exists events_project_start_idx on public.events (project_id, start_at) where project_id is not null;

create table if not exists public.event_attendees (
    id uuid primary key default gen_random_uuid(),
    event_id uuid not null references public.events(id) on delete cascade,
    user_id uuid not null references public.profiles(id) on delete cascade,
    created_at timestamptz not null default now(),
    unique (event_id, user_id)
);

create index if not exists event_attendees_user_id_idx on public.event_attendees (user_id);

-- ── Hilfsfunktionen ──────────────────────────────────────────────────────────

create or replace function public.can_read_event(p_event_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.events e
        where e.id = p_event_id
          and (
              e.owner_id = auth.uid()
              or (e.project_id is not null and public.is_project_member(e.project_id))
              or exists (
                  select 1 from public.event_attendees a
                  where a.event_id = e.id and a.user_id = auth.uid()
              )
          )
    );
$$;

create or replace function public.can_edit_event(p_event_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1 from public.events e
        where e.id = p_event_id
          and (
              e.owner_id = auth.uid()
              or (e.project_id is not null and public.can_edit_project(e.project_id))
          )
    );
$$;

revoke execute on function public.can_read_event(uuid) from public, anon;
revoke execute on function public.can_edit_event(uuid) from public, anon;
grant execute on function public.can_read_event(uuid) to authenticated;
grant execute on function public.can_edit_event(uuid) to authenticated;

-- ── Trigger ──────────────────────────────────────────────────────────────────

create or replace function public.events_before_write()
returns trigger
language plpgsql
as $$
begin
    if tg_op = 'INSERT' then
        new.owner_id = auth.uid();
        new.created_at = now();
    else
        new.owner_id = old.owner_id;
        new.created_at = old.created_at;
    end if;
    return new;
end;
$$;

drop trigger if exists events_before_write on public.events;
create trigger events_before_write
    before insert or update on public.events
    for each row execute function public.events_before_write();

drop trigger if exists events_set_updated_at on public.events;
create trigger events_set_updated_at
    before update on public.events
    for each row execute function public.set_updated_at();

-- ── Policies: events ─────────────────────────────────────────────────────────

alter table public.events enable row level security;

drop policy if exists "Users can read accessible events" on public.events;
create policy "Users can read accessible events"
    on public.events for select
    to authenticated
    using (
        owner_id = (select auth.uid())
        or (project_id is not null and public.is_project_member(project_id))
        or public.can_read_event(id)
    );

drop policy if exists "Users can create events" on public.events;
create policy "Users can create events"
    on public.events for insert
    to authenticated
    with check (project_id is null or public.can_edit_project(project_id));

drop policy if exists "Owners and editors can update events" on public.events;
create policy "Owners and editors can update events"
    on public.events for update
    to authenticated
    using (
        owner_id = (select auth.uid())
        or (project_id is not null and public.can_edit_project(project_id))
    )
    with check (project_id is null or public.can_edit_project(project_id) or owner_id = (select auth.uid()));

drop policy if exists "Owners and editors can delete events" on public.events;
create policy "Owners and editors can delete events"
    on public.events for delete
    to authenticated
    using (
        owner_id = (select auth.uid())
        or (project_id is not null and public.can_edit_project(project_id))
    );

-- ── Policies: event_attendees ────────────────────────────────────────────────

alter table public.event_attendees enable row level security;

drop policy if exists "Users can read attendees of visible events" on public.event_attendees;
create policy "Users can read attendees of visible events"
    on public.event_attendees for select
    to authenticated
    using (public.can_read_event(event_id));

drop policy if exists "Event editors can add attendees" on public.event_attendees;
create policy "Event editors can add attendees"
    on public.event_attendees for insert
    to authenticated
    with check (public.can_edit_event(event_id));

drop policy if exists "Event editors can remove attendees, attendees can leave" on public.event_attendees;
create policy "Event editors can remove attendees, attendees can leave"
    on public.event_attendees for delete
    to authenticated
    using (public.can_edit_event(event_id) or user_id = (select auth.uid()));


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 007_activity_log.sql
-- └──────────────────────────────────────────────────────────────────────────

-- 007: Aktivitätsprotokoll für die Dashboard-Timeline
-- Wird ausschließlich per Trigger geschrieben; Clients haben nur Lesezugriff.

create table if not exists public.activity_log (
    id uuid primary key default gen_random_uuid(),
    project_id uuid not null references public.projects(id) on delete cascade,
    actor_id uuid references public.profiles(id) on delete set null,
    action text not null check (action in (
        'project_created', 'member_added', 'member_removed',
        'issue_created', 'issue_moved', 'issue_assigned', 'issue_deleted',
        'comment_added'
    )),
    entity_id uuid,
    message text not null,
    created_at timestamptz not null default now()
);

create index if not exists activity_log_project_created_idx on public.activity_log (project_id, created_at desc);

alter table public.activity_log enable row level security;

drop policy if exists "Members can read activity" on public.activity_log;
create policy "Members can read activity"
    on public.activity_log for select
    to authenticated
    using (public.is_project_member(project_id));

create or replace function public.log_activity(
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
    if not exists (select 1 from public.projects where id = p_project_id) then
        return;
    end if;

    insert into public.activity_log (project_id, actor_id, action, entity_id, message)
    values (p_project_id, auth.uid(), p_action, p_entity_id, p_message);
end;
$$;

revoke execute on function public.log_activity(uuid, text, uuid, text) from public, anon, authenticated;

create or replace function public.issue_key(p_project_id uuid, p_number integer)
returns text
language sql
stable
security definer
set search_path = ''
as $$
    select coalesce((select key from public.projects where id = p_project_id), 'PRJ') || '-' || p_number;
$$;

revoke execute on function public.issue_key(uuid, integer) from public, anon;
grant execute on function public.issue_key(uuid, integer) to authenticated;

-- ── Trigger: projects ────────────────────────────────────────────────────────

create or replace function public.log_project_activity()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    perform public.log_activity(new.id, 'project_created', new.id,
        format('Projekt „%s“ wurde angelegt', new.name));
    return null;
end;
$$;

drop trigger if exists projects_log_activity on public.projects;
create trigger projects_log_activity
    after insert on public.projects
    for each row execute function public.log_project_activity();

-- ── Trigger: project_members ─────────────────────────────────────────────────

create or replace function public.log_member_activity()
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
    from public.profiles p
    where p.id = coalesce(new.user_id, old.user_id);

    if tg_op = 'INSERT' then
        -- Eigentümer beim Anlegen des Projekts nicht extra protokollieren
        if public.is_project_owner(new.project_id, new.user_id) then
            return null;
        end if;
        perform public.log_activity(new.project_id, 'member_added', new.user_id,
            format('%s wurde dem Projekt hinzugefügt', v_name));
    else
        perform public.log_activity(old.project_id, 'member_removed', old.user_id,
            format('%s wurde aus dem Projekt entfernt', v_name));
    end if;
    return null;
end;
$$;

drop trigger if exists project_members_log_activity on public.project_members;
create trigger project_members_log_activity
    after insert or delete on public.project_members
    for each row execute function public.log_member_activity();

-- ── Trigger: issues ──────────────────────────────────────────────────────────

create or replace function public.log_issue_activity()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_assignee text;
begin
    if tg_op = 'INSERT' then
        perform public.log_activity(new.project_id, 'issue_created', new.id,
            format('%s „%s“ wurde angelegt', public.issue_key(new.project_id, new.number), new.title));
    elsif tg_op = 'DELETE' then
        perform public.log_activity(old.project_id, 'issue_deleted', old.id,
            format('%s „%s“ wurde gelöscht', public.issue_key(old.project_id, old.number), old.title));
    else
        if new.status is distinct from old.status then
            perform public.log_activity(new.project_id, 'issue_moved', new.id,
                format('%s wurde nach „%s“ verschoben', public.issue_key(new.project_id, new.number),
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
            from public.profiles p
            where p.id = new.assignee_id;

            perform public.log_activity(new.project_id, 'issue_assigned', new.id,
                format('%s wurde %s zugewiesen', public.issue_key(new.project_id, new.number), v_assignee));
        end if;
    end if;
    return null;
end;
$$;

drop trigger if exists issues_log_activity on public.issues;
create trigger issues_log_activity
    after insert or update or delete on public.issues
    for each row execute function public.log_issue_activity();

-- ── Trigger: issue_comments ──────────────────────────────────────────────────

create or replace function public.log_comment_activity()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    v_issue public.issues%rowtype;
begin
    select * into v_issue from public.issues where id = new.issue_id;
    perform public.log_activity(v_issue.project_id, 'comment_added', new.issue_id,
        format('Neuer Kommentar zu %s', public.issue_key(v_issue.project_id, v_issue.number)));
    return null;
end;
$$;

drop trigger if exists issue_comments_log_activity on public.issue_comments;
create trigger issue_comments_log_activity
    after insert on public.issue_comments
    for each row execute function public.log_comment_activity();


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 008_attachments.sql
-- └──────────────────────────────────────────────────────────────────────────

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


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 009_notifications.sql
-- └──────────────────────────────────────────────────────────────────────────

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


-- ┌──────────────────────────────────────────────────────────────────────────
-- │ 010_realtime.sql
-- └──────────────────────────────────────────────────────────────────────────

-- 010: Realtime für Board und Benachrichtigungen aktivieren
-- Supabase verschickt Änderungen nur für Tabellen in der Publikation supabase_realtime.
-- RLS gilt auch für Realtime: Nutzer erhalten nur Zeilen, die sie lesen dürfen.

do $$
declare
    v_table text;
begin
    if not exists (select 1 from pg_publication where pubname = 'supabase_realtime') then
        raise notice 'Publikation supabase_realtime nicht gefunden – übersprungen';
        return;
    end if;

    foreach v_table in array array['issues', 'notifications'] loop
        if not exists (
            select 1 from pg_publication_tables
            where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = v_table
        ) then
            execute format('alter publication supabase_realtime add table public.%I', v_table);
        end if;
    end loop;
end;
$$;
