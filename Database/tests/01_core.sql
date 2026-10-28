-- Kernregeln: Rollen, RLS, Nummerierung, Kommentare, Termine, Activity-Log, Löschen
-- Testnutzer: Anna (Eigentümerin), Ben (member), Cara (viewer), Dora (außenstehend)

\set ON_ERROR_STOP 1
\set QUIET 1
insert into auth.users values
 ('aaaaaaaa-0000-0000-0000-000000000000','anna@example.test'),
 ('bbbbbbbb-0000-0000-0000-000000000000','ben@example.test'),
 ('cccccccc-0000-0000-0000-000000000000','cara@example.test'),
 ('dddddddd-0000-0000-0000-000000000000','dora@example.test');
create function public.t_assert(cond boolean, msg text) returns void language plpgsql as $$
begin if cond is not true then raise exception 'FAILED: %', msg; end if; raise notice 'ok: %', msg; end $$;
create function public.t_fails(stmt text, msg text) returns void language plpgsql as $$
begin execute stmt; raise exception 'FAILED (no error): %', msg;
exception when others then
  if sqlerrm like 'FAILED%' then raise; end if;
  raise notice 'ok: % (%)', msg, sqlerrm; end $$;
grant execute on function public.t_assert(boolean,text), public.t_fails(text,text) to authenticated;
create table public.t_ids (name text primary key, id uuid);
grant all on public.t_ids to authenticated;

select t_assert((select count(*) from profiles where email in ('anna@example.test','ben@example.test','cara@example.test','dora@example.test')) = 4, 'Sign-up legt Profile an');

set role authenticated;
-- Anna (Owner)
select set_config('request.jwt.claim.sub','aaaaaaaa-0000-0000-0000-000000000000',false);
with p as (insert into projects(id,user_id,name,description) values (gen_random_uuid(),'aaaaaaaa-0000-0000-0000-000000000000','Board Test','x') returning id, key)
insert into t_ids select 'proj', id from p;
select t_assert((select key from projects) = 'BOA' and (select count(*) from projects)=1, 'Owner sieht nur eigenes Projekt, Key BOA');
select t_assert((select role from project_members where user_id = auth.uid()) = 'admin', 'Owner ist admin-Mitglied');
insert into project_members(project_id,user_id,role) select id,'bbbbbbbb-0000-0000-0000-000000000000','member' from t_ids where name='proj';
insert into project_members(project_id,user_id,role) select id,'cccccccc-0000-0000-0000-000000000000','viewer' from t_ids where name='proj';
update projects set name='Board Test 2', user_id='dddddddd-0000-0000-0000-000000000000', issue_counter=99;
select t_assert((select user_id from projects)='aaaaaaaa-0000-0000-0000-000000000000' and (select issue_counter from projects)=0 and (select name from projects)='Board Test 2', 'Owner/Zaehler bei Update geschuetzt');

-- Dora (aussenstehend)
select set_config('request.jwt.claim.sub','dddddddd-0000-0000-0000-000000000000',false);
select t_assert((select count(*) from projects)=0, 'Aussenstehende sehen kein Projekt');
select t_fails($q$insert into project_members(project_id,user_id) select id,'dddddddd-0000-0000-0000-000000000000' from t_ids where name='proj'$q$, 'Aussenstehende koennen sich nicht selbst hinzufuegen');
select t_assert((select count(*) from profiles)=1, 'Aussenstehende sehen nur eigenes Profil');
select t_assert((select count(*) from find_profile_by_email(' ANNA@example.test'))=1, 'find_profile_by_email findet Profil');

-- Ben (member)
select set_config('request.jwt.claim.sub','bbbbbbbb-0000-0000-0000-000000000000',false);
select t_assert((select count(*) from profiles)=3, 'Member sieht Profile der Mitglieder (A,B,C)');
with i as (insert into issues(project_id,title,number,created_by) select id,'Erstes',42,'aaaaaaaa-0000-0000-0000-000000000000' from t_ids where name='proj' returning id) insert into t_ids select 'i1', id from i;
insert into issues(project_id,title,status) select id,'Zweites','Todo' from t_ids where name='proj';
select t_assert((select array_agg(number order by number) from issues) = '{1,2}' and (select bool_and(created_by=auth.uid()) from issues), 'Nummern fortlaufend, created_by erzwungen');
update projects set name='hack';
select t_assert((select name from projects)='Board Test 2', 'Member kann Projekt nicht aendern');
update issues set status='InProgress', position=1.5 where number=1;
select t_fails($q$update issues set assignee_id='dddddddd-0000-0000-0000-000000000000' where number=1$q$, 'Assignee muss Projektmitglied sein');
update issues set assignee_id='cccccccc-0000-0000-0000-000000000000' where number=1;
insert into issue_comments(issue_id, body) select id,'Hallo' from t_ids where name='i1';
select t_assert((select comment_count from issues where number=1)=1, 'comment_count per Trigger');
update issues set comment_count=50 where number=1;
select t_assert((select comment_count from issues where number=1)=1, 'comment_count nicht vom Client aenderbar');
insert into events(project_id,title,start_at,end_at) select id,'Planning',now(),now()+interval '1h' from t_ids where name='proj';
delete from project_members where user_id='aaaaaaaa-0000-0000-0000-000000000000';
select set_config('request.jwt.claim.sub','aaaaaaaa-0000-0000-0000-000000000000',false);
select t_assert((select count(*) from project_members where user_id=auth.uid())=1, 'Owner kann nicht entfernt werden');

-- Cara (viewer)
select set_config('request.jwt.claim.sub','cccccccc-0000-0000-0000-000000000000',false);
select t_assert((select count(*) from issues)=2, 'Viewer liest Issues');
select t_fails($q$insert into issues(project_id,title) select id,'x' from t_ids where name='proj'$q$, 'Viewer kann keine Issues anlegen');
select t_fails($q$insert into issue_comments(issue_id,body) select id,'x' from t_ids where name='i1'$q$, 'Viewer kann nicht kommentieren');
select t_assert((select count(*) from events)=1, 'Viewer sieht Projekttermin');
update issues set title='hack';
select t_assert((select count(*) from issues where title='hack')=0, 'Viewer kann Issues nicht aendern');

-- Dora: privater Termin, Anna wird eingeladen
select set_config('request.jwt.claim.sub','dddddddd-0000-0000-0000-000000000000',false);
with e as (insert into events(title,start_at,end_at,owner_id) values ('Privat',now(),now()+interval '1h','aaaaaaaa-0000-0000-0000-000000000000') returning id) insert into t_ids select 'e_priv', id from e;
select t_assert((select owner_id from events) = auth.uid(), 'owner_id erzwungen, nur eigener Termin sichtbar');
select t_fails($q$insert into events(project_id,title,start_at,end_at) select id,'x',now(),now() from t_ids where name='proj'$q$, 'Kein Termin in fremdem Projekt');
select set_config('request.jwt.claim.sub','aaaaaaaa-0000-0000-0000-000000000000',false);
select t_assert((select count(*) from events)=1, 'Anna sieht privaten Termin vor Einladung nicht');
select set_config('request.jwt.claim.sub','dddddddd-0000-0000-0000-000000000000',false);
insert into event_attendees(event_id,user_id) select id,'aaaaaaaa-0000-0000-0000-000000000000' from t_ids where name='e_priv';
select set_config('request.jwt.claim.sub','aaaaaaaa-0000-0000-0000-000000000000',false);
select t_assert((select count(*) from events)=2, 'Teilnehmerin sieht privaten Termin');
select t_assert((select count(*) from event_attendees)=1, 'Teilnehmerin sieht Teilnehmerliste');
select t_fails($q$insert into activity_log(project_id,action,message) select id,'issue_created','x' from t_ids where name='proj'$q$, 'Client kann nicht ins Activity-Log schreiben');

-- Anna entfernt Cara -> Issue wird freigegeben
delete from project_members where user_id='cccccccc-0000-0000-0000-000000000000';
select t_assert((select assignee_id from issues where number=1) is null, 'Issue nach Entfernen freigegeben');
delete from issues where number=2;
reset role;
select action, message from activity_log a join t_ids t on t.id = a.project_id order by created_at, action;
set role authenticated;
select set_config('request.jwt.claim.sub','bbbbbbbb-0000-0000-0000-000000000000',false);
delete from projects;
select t_assert((select count(*) from projects)=1, 'Member kann Projekt nicht loeschen');
select set_config('request.jwt.claim.sub','aaaaaaaa-0000-0000-0000-000000000000',false);
delete from projects;
reset role;
select t_assert((select count(*) from projects where name like 'Board%')=0 and (select count(*) from issues)=0 and (select count(*) from activity_log a join t_ids t on t.id=a.project_id)=0, 'Owner loescht Projekt inkl. Cascade');
