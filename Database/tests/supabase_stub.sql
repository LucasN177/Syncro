-- Minimaler Nachbau der Supabase-Umgebung für Tests mit einem normalen Postgres.
-- Nicht in Supabase ausführen!

do $$
begin
    if not exists (select 1 from pg_roles where rolname = 'anon') then
        create role anon nologin;
    end if;
    if not exists (select 1 from pg_roles where rolname = 'authenticated') then
        create role authenticated nologin;
    end if;
end;
$$;

-- auth: Nutzer und auth.uid() aus der gesetzten JWT-Claim
create schema auth;
create table auth.users (id uuid primary key, email text);
create function auth.uid() returns uuid language sql stable as
$$ select nullif(current_setting('request.jwt.claim.sub', true), '')::uuid $$;
grant usage on schema auth to authenticated, anon;
grant execute on function auth.uid() to authenticated, anon;

-- In Supabase haben anon und authenticated Rechte auf public; RLS regelt den Rest
alter default privileges in schema public grant all on tables to anon, authenticated;

-- storage: nur die Tabellen, die 008_attachments.sql braucht
create schema storage;
create table storage.buckets (id text primary key, name text not null, public boolean default false, file_size_limit bigint);
create table storage.objects (id uuid primary key default gen_random_uuid(), bucket_id text references storage.buckets(id), name text not null);
alter table storage.objects enable row level security;
grant usage on schema storage to authenticated, anon;
grant all on storage.objects, storage.buckets to authenticated;

-- Realtime-Publikation (wal_level-Warnung beim Anlegen ist für Tests egal)
set client_min_messages = error;
create publication supabase_realtime;
