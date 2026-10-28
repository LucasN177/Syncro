-- projects-Tabelle, wie sie im Supabase Table Editor entstehen kann:
-- ohne status/color/description, user_id als text, eigene Policy darauf
create schema if not exists syncro;

create table syncro.projects (
    id uuid primary key default gen_random_uuid(),
    created_at timestamptz default now(),
    name text,
    user_id text
);

alter table syncro.projects enable row level security;

create policy "Enable read access for own rows"
    on syncro.projects for select
    using (user_id = auth.uid()::text);

insert into auth.users values ('22222222-2222-2222-2222-222222222222', 'manuell@example.test');

insert into syncro.projects (name, user_id) values
    ('Syncro', '22222222-2222-2222-2222-222222222222'),
    (null, '22222222-2222-2222-2222-222222222222'),
    ('Ohne Eigentümer', null);
