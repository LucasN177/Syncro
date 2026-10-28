-- 000: Schema syncro anlegen und vorab prüfen
--
-- Syncro liegt komplett im eigenen Schema "syncro", damit es sich die Datenbank mit anderen
-- Apps (z. B. Monexa im Schema public) teilen kann, ohne Tabellen oder Funktionen zu überschneiden.
-- Damit die App über die Supabase-API zugreifen kann, muss "syncro" im Dashboard unter
-- Project Settings → Data API → Exposed schemas eingetragen sein.

create schema if not exists syncro;

grant usage on schema syncro to anon, authenticated, service_role;

-- Rechte für alle Objekte, die die folgenden Migrationen anlegen; RLS regelt den Zugriff im Detail
alter default privileges in schema syncro grant all on tables to anon, authenticated, service_role;
alter default privileges in schema syncro grant all on sequences to anon, authenticated, service_role;

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
            where p.pronamespace = 'syncro'::regnamespace
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
            join information_schema.tables t on t.table_schema = 'syncro' and t.table_name = e.table_name
        )
        -- Spalten, die fehlen oder einen anderen Typ haben
        select e.table_name,
               format('%s.%s: erwartet %s, vorhanden %s', e.table_name, e.column_name, e.data_type,
                      coalesce(c.data_type, 'fehlt')) as message
        from expected e
        join existing_tables using (table_name)
        left join information_schema.columns c
            on c.table_schema = 'syncro' and c.table_name = e.table_name and c.column_name = e.column_name
        where c.data_type is distinct from e.data_type
          and not (e.added_by_migration and c.data_type is null)
        union all
        -- Zusätzliche Pflichtspalten ohne Standardwert verhindern jedes Insert der App
        select c.table_name::text,
               format('%s.%s: zusätzliche Pflichtspalte ohne Standardwert', c.table_name, c.column_name)
        from information_schema.columns c
        join existing_tables et on et.table_name = c.table_name
        where c.table_schema = 'syncro'
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
                          (select string_agg('syncro.' || quote_ident(t), ', ') from unnest(v_tables) t));
    end if;
end;
$$;
