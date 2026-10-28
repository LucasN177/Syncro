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
