-- Anhänge, Storage-Policies, Benachrichtigungen und Realtime-Publikation

\set ON_ERROR_STOP 1
\set QUIET 1
insert into auth.users values
 ('aaaaaaaa-0000-0000-0000-000000000000','anna@example.test'),
 ('bbbbbbbb-0000-0000-0000-000000000000','ben.k+dev@example.test'),
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

select t_assert((select count(*) from pg_publication_tables where pubname='supabase_realtime' and tablename in ('issues','notifications')) = 2, 'Realtime-Publikation enthaelt issues und notifications');
select t_assert((select public from storage.buckets where id='attachments') = false, 'Privater Bucket attachments angelegt');

set role authenticated;
-- Anna legt Projekt an, Ben member, Cara viewer
select set_config('request.jwt.claim.sub','aaaaaaaa-0000-0000-0000-000000000000',false);
with p as (insert into projects(id,user_id,name) values (gen_random_uuid(),'aaaaaaaa-0000-0000-0000-000000000000','Test') returning id) insert into t_ids select 'proj', id from p;
insert into project_members(project_id,user_id,role) select id,'bbbbbbbb-0000-0000-0000-000000000000','member' from t_ids where name='proj';
insert into project_members(project_id,user_id,role) select id,'cccccccc-0000-0000-0000-000000000000','viewer' from t_ids where name='proj';
-- Anna legt Issue an und weist es Ben zu
with i as (insert into issues(project_id,title,assignee_id) select id,'Login','bbbbbbbb-0000-0000-0000-000000000000' from t_ids where name='proj' returning id) insert into t_ids select 'issue', id from i;
select t_assert((select count(*) from notifications) = 0, 'Anna sieht keine fremden Benachrichtigungen');

select set_config('request.jwt.claim.sub','bbbbbbbb-0000-0000-0000-000000000000',false);
select t_assert((select string_agg(type, ',' order by created_at, type) from notifications) = 'member_added,assigned', 'Ben: Einladung und Zuweisung');
select t_assert((select message from notifications where type='assigned') like 'anna hat dir TES-1%', 'Zuweisungstext mit Akteur und Nummer');
-- Ben kommentiert und erwaehnt Cara (Username cara) -> Cara mentioned, Anna (Erstellerin) commented
insert into issue_comments(issue_id, body) select id, 'Schau mal @Cara, und @ben.k+dev bin ich selbst' from t_ids where name='issue';
-- Ben markiert als gelesen, versucht Text zu aendern
update notifications set read_at = now(), message = 'hack';
select t_assert((select bool_and(read_at is not null) and bool_and(message <> 'hack') from notifications), 'Nur read_at aenderbar');

select set_config('request.jwt.claim.sub','cccccccc-0000-0000-0000-000000000000',false);
select t_assert((select string_agg(type, ',' order by created_at, type) from notifications) = 'member_added,mentioned', 'Cara: Einladung und Erwaehnung');
select t_fails($q$insert into notifications(user_id,type,message) values ('cccccccc-0000-0000-0000-000000000000','assigned','x')$q$, 'Client kann keine Benachrichtigungen anlegen');
-- Viewer darf keine Anhaenge
select t_fails($q$insert into issue_attachments(issue_id,file_name,storage_path) select i.id,'a.txt',p.id||'/'||i.id||'/a.txt' from t_ids i, t_ids p where i.name='issue' and p.name='proj'$q$, 'Viewer kann keinen Anhang anlegen');
select t_fails($q$insert into storage.objects(bucket_id,name) select 'attachments', id||'/x/a.txt' from t_ids where name='proj'$q$, 'Viewer kann nicht hochladen');

select set_config('request.jwt.claim.sub','aaaaaaaa-0000-0000-0000-000000000000',false);
select t_assert((select string_agg(type, ',') from notifications) = 'commented', 'Anna: Kommentar zu ihrem Issue');
select t_fails($q$insert into issue_attachments(issue_id,file_name,storage_path) select id,'a.txt','falsch/a.txt' from t_ids where name='issue'$q$, 'Falscher Pfad wird abgelehnt');
insert into storage.objects(bucket_id,name) select 'attachments', p.id||'/'||i.id||'/plan.pdf' from t_ids i, t_ids p where i.name='issue' and p.name='proj';
insert into issue_attachments(issue_id,file_name,storage_path,size_bytes,project_id) select i.id,'plan.pdf',p.id||'/'||i.id||'/plan.pdf',1234,'dddddddd-0000-0000-0000-000000000000' from t_ids i, t_ids p where i.name='issue' and p.name='proj';
select t_assert((select project_id from issue_attachments) = (select id from t_ids where name='proj') and (select uploaded_by from issue_attachments) = auth.uid(), 'project_id und uploaded_by erzwungen');
select t_assert((select attachment_count from issues) = 1, 'attachment_count per Trigger');
update issues set attachment_count = 9;
select t_assert((select attachment_count from issues) = 1, 'attachment_count nicht vom Client aenderbar');

select set_config('request.jwt.claim.sub','dddddddd-0000-0000-0000-000000000000',false);
select t_assert((select count(*) from storage.objects) = 0 and (select count(*) from issue_attachments) = 0, 'Aussenstehende sehen keine Anhaenge');
select t_fails($q$insert into storage.objects(bucket_id,name) values ('attachments','kein-uuid/a.txt')$q$, 'Ungueltiger Pfad im Storage abgelehnt');

select set_config('request.jwt.claim.sub','cccccccc-0000-0000-0000-000000000000',false);
select t_assert((select count(*) from storage.objects) = 1 and (select count(*) from issue_attachments) = 1, 'Viewer kann Anhaenge sehen');

select set_config('request.jwt.claim.sub','aaaaaaaa-0000-0000-0000-000000000000',false);
delete from issue_attachments;
select t_assert((select attachment_count from issues) = 0, 'attachment_count nach Loeschen 0');
delete from projects;
reset role;
select t_assert((select count(*) from notifications) = 0 and (select count(*) from issue_attachments) = 0, 'Projekt-Loeschung raeumt Benachrichtigungen und Anhaenge');
