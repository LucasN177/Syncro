-- 001: Profile je Supabase-Auth-Nutzer
-- Wird beim Sign-up automatisch per Trigger angelegt.

create or replace function syncro.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

create table if not exists syncro.profiles (
    id uuid primary key references auth.users(id) on delete cascade,
    email text,
    username text not null default '',
    vorname text not null default '',
    nachname text not null default '',
    avatar_color text not null default '#0f766e',
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create unique index if not exists profiles_email_key on syncro.profiles (lower(email));

drop trigger if exists profiles_set_updated_at on syncro.profiles;
create trigger profiles_set_updated_at
    before update on syncro.profiles
    for each row execute function syncro.set_updated_at();

-- Profil beim Sign-up anlegen
create or replace function syncro.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    insert into syncro.profiles (id, email, username)
    values (new.id, new.email, coalesce(split_part(new.email, '@', 1), ''))
    on conflict (id) do nothing;
    return new;
end;
$$;

drop trigger if exists syncro_on_auth_user_created on auth.users;
create trigger syncro_on_auth_user_created
    after insert on auth.users
    for each row execute function syncro.handle_new_user();

-- E-Mail-Änderungen in Auth ins Profil übernehmen
create or replace function syncro.handle_user_email_changed()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
    update syncro.profiles set email = new.email where id = new.id;
    return new;
end;
$$;

drop trigger if exists syncro_on_auth_user_email_changed on auth.users;
create trigger syncro_on_auth_user_email_changed
    after update of email on auth.users
    for each row
    when (old.email is distinct from new.email)
    execute function syncro.handle_user_email_changed();

-- Bereits registrierte Nutzer nachziehen
insert into syncro.profiles (id, email, username)
select id, email, coalesce(split_part(email, '@', 1), '')
from auth.users
on conflict (id) do nothing;

alter table syncro.profiles enable row level security;

-- Lese-Policy für Profile von Projektmitgliedern folgt in 003_project_members.sql
drop policy if exists "Users can read their own profile" on syncro.profiles;
create policy "Users can read their own profile"
    on syncro.profiles for select
    to authenticated
    using ((select auth.uid()) = id);

drop policy if exists "Users can update their own profile" on syncro.profiles;
create policy "Users can update their own profile"
    on syncro.profiles for update
    to authenticated
    using ((select auth.uid()) = id)
    with check ((select auth.uid()) = id);
