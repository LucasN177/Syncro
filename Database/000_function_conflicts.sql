-- 000: Gleichnamige Funktionen aus früheren Setups entfernen
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
