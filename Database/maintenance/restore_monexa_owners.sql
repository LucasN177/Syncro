-- ============================================================================
-- Monexa: Projekteigentümer wieder als Admin eintragen
--
-- Nach remove_syncro_from_public.sql und Monexas setup_all.sql ausführen.
-- Die ursprüngliche Monexa-Tabelle project_members wurde gelöscht. Die Eigentümer lassen sich
-- aus projects.owner_id wiederherstellen; weitere Mitglieder und offene Einladungen nur aus
-- einem Backup oder durch erneutes Einladen in Monexa.
-- Kann gefahrlos mehrfach ausgeführt werden.
-- ============================================================================

insert into public.project_members (project_id, user_id, email, role)
select p.id, p.owner_id, coalesce(u.email, p.owner_id::text), 2
from public.projects p
join auth.users u on u.id = p.owner_id
on conflict (project_id, email) do nothing;

select
    (select count(*) from public.projects) as projekte,
    (select count(*) from public.projects where owner_id is null) as projekte_ohne_eigentuemer,
    (select count(*) from public.project_members where role = 2) as admins;
