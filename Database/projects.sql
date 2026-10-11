create table if not exists public.projects (
    id uuid primary key,
    user_id uuid not null references auth.users(id) on delete cascade,
    name text not null check (char_length(trim(name)) > 0),
    description text,
    status text not null default 'Aktiv',
    color text not null default '#0f766e',
    created_at timestamptz not null default now()
);

alter table public.projects enable row level security;

create policy "Users can read their own projects"
    on public.projects for select
    using (auth.uid() = user_id);

create policy "Users can create their own projects"
    on public.projects for insert
    with check (auth.uid() = user_id);

create policy "Users can update their own projects"
    on public.projects for update
    using (auth.uid() = user_id)
    with check (auth.uid() = user_id);
