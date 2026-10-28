-- ============================================================================
-- Syncro aus dem Schema public entfernen
--
-- Frühere Syncro-Versionen haben ihre Tabellen und Funktionen im Schema public angelegt und
-- sich dort mit Monexa (projects, project_members, project_role) überschnitten. Dieses Skript
-- entfernt ausschließlich Syncro-Objekte aus public. Monexas Tabellen und Daten bleiben erhalten;
-- Objekte werden nur gelöscht, wenn sie eindeutig zu Syncro gehören (Spalten bzw. Signatur).
--
-- Reihenfolge im Supabase SQL-Editor:
--   1. dieses Skript
--   2. Monexas database/setup_all.sql (stellt project_members, project_role und alle Policies wieder her)
--   3. Database/maintenance/restore_monexa_owners.sql (Eigentümer wieder als Admin eintragen)
--   4. Database/setup_all.sql von Syncro (legt Syncro im eigenen Schema syncro an)
--
-- Gibt es in public Daten, die mit Syncro angelegt wurden, bricht das Skript ab und nennt sie.
-- Sollen sie trotzdem gelöscht werden, die folgende Zeile einkommentieren:
-- select set_config('syncro.confirm_delete', 'ja', false);
-- ============================================================================

-- Hilfsfunktion nur für dieses Skript (pg_temp verschwindet mit der Sitzung)
create or replace function pg_temp.has_column(p_table text, p_column text, p_type text default null)
returns boolean
language sql
as $$
    select exists (
        select 1 from information_schema.columns
        where table_schema = 'public' and table_name = p_table and column_name = p_column
          and (p_type is null or data_type = p_type)
    );
$$;

-- ── 0. Sicherheitsprüfung: Daten, die mit Syncro in public angelegt wurden ───

do $$
declare
    v_projects bigint = 0;
    v_issues bigint = 0;
    v_events bigint = 0;
begin
    if pg_temp.has_column('projects', 'user_id') then
        execute 'select count(*) from public.projects where user_id is not null' into v_projects;
    end if;
    if pg_temp.has_column('issues', 'comment_count') then
        execute 'select count(*) from public.issues' into v_issues;
    end if;
    if pg_temp.has_column('events', 'owner_id') and pg_temp.has_column('events', 'category') then
        execute 'select count(*) from public.events' into v_events;
    end if;

    if v_projects + v_issues + v_events > 0
       and coalesce(current_setting('syncro.confirm_delete', true), '') <> 'ja' then
        raise exception using
            message = format('Im Schema public gibt es Daten aus Syncro: %s Projekte, %s Issues, %s Termine. Sie würden gelöscht.',
                             v_projects, v_issues, v_events),
            hint = 'Werden sie nicht gebraucht, am Anfang des Skripts die Zeile mit syncro.confirm_delete einkommentieren und erneut ausführen.';
    end if;
end;
$$;

-- ── 1. Syncro-Trigger auf auth.users ─────────────────────────────────────────

do $$
declare
    v_trigger record;
begin
    for v_trigger in
        select t.tgname
        from pg_trigger t
        where t.tgrelid = 'auth.users'::regclass
          and not t.tgisinternal
          and t.tgfoid in (
              select oid from pg_proc
              where pronamespace = 'public'::regnamespace
                and proname in ('handle_new_user', 'handle_user_email_changed')
                and pg_get_function_arguments(oid) = ''
          )
    loop
        raise notice 'Entferne Trigger % auf auth.users', v_trigger.tgname;
        execute format('drop trigger %I on auth.users', v_trigger.tgname);
    end loop;
end;
$$;

-- ── 2. Syncro-Tabellen (nur wenn ihre Spalten eindeutig zu Syncro passen) ─────

do $$
declare
    v_table record;
begin
    for v_table in
        select * from (values
            ('notifications', 'actor_id', null),
            ('issue_attachments', 'storage_path', null),
            ('activity_log', 'entity_id', null),
            ('event_attendees', 'event_id', null),
            ('events', 'owner_id', null),
            ('issue_comments', 'body', null),
            ('issues', 'comment_count', null),
            -- Monexas project_members hat role als smallint und bleibt erhalten
            ('project_members', 'role', 'text'),
            ('profiles', 'avatar_color', null)
        ) as syncro_tables (name, marker_column, marker_type)
    loop
        if pg_temp.has_column(v_table.name, v_table.marker_column, v_table.marker_type) then
            raise notice 'Entferne Tabelle public.%', v_table.name;
            execute format('drop table public.%I cascade', v_table.name);
        end if;
    end loop;
end;
$$;

-- ── 3. projects: Syncro-Anteile entfernen, Monexas Tabelle behalten ─────────

do $$
begin
    if to_regclass('public.projects') is null then
        return;
    end if;

    -- Trigger und Policies, die Syncro auf projects angelegt hat
    drop trigger if exists projects_before_write on public.projects;
    drop trigger if exists projects_set_updated_at on public.projects;
    drop trigger if exists projects_add_owner_member on public.projects;
    drop trigger if exists projects_log_activity on public.projects;
    drop policy if exists "Members can read projects" on public.projects;
    drop policy if exists "Users can create their own projects" on public.projects;
    drop policy if exists "Admins can update projects" on public.projects;
    drop policy if exists "Owners can delete projects" on public.projects;
    drop policy if exists "Users can read their own projects" on public.projects;
    drop policy if exists "Users can update their own projects" on public.projects;

    if pg_temp.has_column('projects', 'owner_id') then
        -- Monexas Tabelle: mit Syncro angelegte Projekte und Syncro-Spalten entfernen
        if pg_temp.has_column('projects', 'user_id') then
            execute 'delete from public.projects where user_id is not null';
        end if;
        alter table public.projects
            drop column if exists description,
            drop column if exists status,
            drop column if exists color,
            drop column if exists user_id,
            drop column if exists key,
            drop column if exists issue_counter,
            drop column if exists updated_at;
        raise notice 'Syncro-Spalten aus public.projects entfernt, Monexas Daten bleiben erhalten';
    elsif pg_temp.has_column('projects', 'issue_counter') then
        -- Tabelle gehört allein Syncro
        drop table public.projects cascade;
        raise notice 'Entferne Tabelle public.projects';
    end if;
end;
$$;

-- ── 4. Syncro-Funktionen (nur bei exakt passender Signatur) ───────────────────

do $$
declare
    v_expected record;
    v_existing record;
begin
    for v_expected in
        select * from (values
            ('set_updated_at', '', '', 'trigger'),
            ('handle_new_user', '', '', 'trigger'),
            ('handle_user_email_changed', '', '', 'trigger'),
            ('project_key_from_name', 'text', 'p_name text', 'text'),
            ('projects_before_write', '', '', 'trigger'),
            -- Monexas project_role(p_project uuid) returns smallint passt nicht und bleibt
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
            ('issues_before_write', '', '', 'trigger'),
            ('unassign_issues_of_removed_member', '', '', 'trigger'),
            ('issue_project_id', 'uuid', 'p_issue_id uuid', 'uuid'),
            ('issue_comments_before_write', '', '', 'trigger'),
            ('update_issue_comment_count', '', '', 'trigger'),
            ('can_read_event', 'uuid', 'p_event_id uuid', 'boolean'),
            ('can_edit_event', 'uuid', 'p_event_id uuid', 'boolean'),
            ('events_before_write', '', '', 'trigger'),
            ('log_activity', 'uuid, text, uuid, text', 'p_project_id uuid, p_action text, p_entity_id uuid, p_message text', 'void'),
            ('issue_key', 'uuid, integer', 'p_project_id uuid, p_number integer', 'text'),
            ('log_project_activity', '', '', 'trigger'),
            ('log_member_activity', '', '', 'trigger'),
            ('log_issue_activity', '', '', 'trigger'),
            ('log_comment_activity', '', '', 'trigger'),
            ('issue_attachments_before_insert', '', '', 'trigger'),
            ('update_issue_attachment_count', '', '', 'trigger'),
            ('storage_project_id', 'text', 'p_name text', 'uuid'),
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
            select p.oid::regprocedure as signature
            from pg_proc p
            where p.pronamespace = 'public'::regnamespace
              and p.proname = v_expected.name
              and oidvectortypes(p.proargtypes) = v_expected.arg_types
              and pg_get_function_arguments(p.oid) = v_expected.args
              and pg_get_function_result(p.oid) = v_expected.result
        loop
            raise notice 'Entferne Funktion %', v_existing.signature;
            execute format('drop function %s cascade', v_existing.signature);
        end loop;
    end loop;
end;
$$;

-- ── 5. Storage: Policies und leerer Bucket "attachments" ─────────────────────

drop policy if exists "Project members can read attachments" on storage.objects;
drop policy if exists "Project editors can upload attachments" on storage.objects;
drop policy if exists "Project editors can delete attachments" on storage.objects;

do $$
begin
    if exists (select 1 from storage.objects where bucket_id = 'attachments') then
        raise notice 'Bucket attachments enthält noch Dateien und bleibt bestehen. Bei Bedarf im Dashboard unter Storage leeren und löschen.';
        return;
    end if;
    delete from storage.buckets where id = 'attachments';
exception
    when others then
        raise notice 'Bucket attachments konnte nicht per SQL gelöscht werden (%). Bei Bedarf im Dashboard unter Storage löschen.', sqlerrm;
end;
$$;

-- ── Ergebnis ─────────────────────────────────────────────────────────────────

do $$
begin
    raise notice 'Fertig. Tabellen in public: %',
        (select string_agg(table_name, ', ' order by table_name)
         from information_schema.tables where table_schema = 'public' and table_type = 'BASE TABLE');
    raise notice 'Als Nächstes Monexas setup_all.sql ausführen, danach restore_monexa_owners.sql.';
end;
$$;
