-- 010: Realtime für Board und Benachrichtigungen aktivieren, Rechte nachziehen
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
            where pubname = 'supabase_realtime' and schemaname = 'syncro' and tablename = v_table
        ) then
            execute format('alter publication supabase_realtime add table syncro.%I', v_table);
        end if;
    end loop;
end;
$$;

-- Rechte auf bereits vorhandene Objekte nachziehen (die Default-Rechte aus 000 gelten nur für neue)
grant all on all tables in schema syncro to anon, authenticated, service_role;
grant all on all sequences in schema syncro to anon, authenticated, service_role;
