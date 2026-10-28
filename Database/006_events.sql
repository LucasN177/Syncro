-- 006: Kalendertermine
-- Ohne project_id ist ein Termin privat (Eigentümer + eingeladene Teilnehmer),
-- mit project_id sehen ihn alle Projektmitglieder.

create table if not exists syncro.events (
    id uuid primary key default gen_random_uuid(),
    owner_id uuid references syncro.profiles(id) on delete cascade,
    project_id uuid references syncro.projects(id) on delete cascade,
    title text not null check (char_length(trim(title)) > 0),
    description text,
    location text,
    category text not null default 'Meeting'
        check (category in ('Meeting', 'Review', 'Release', 'Personal', 'Deadline')),
    start_at timestamptz not null,
    end_at timestamptz not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    check (end_at >= start_at)
);

create index if not exists events_owner_start_idx on syncro.events (owner_id, start_at);
create index if not exists events_project_start_idx on syncro.events (project_id, start_at) where project_id is not null;

create table if not exists syncro.event_attendees (
    id uuid primary key default gen_random_uuid(),
    event_id uuid not null references syncro.events(id) on delete cascade,
    user_id uuid not null references syncro.profiles(id) on delete cascade,
    created_at timestamptz not null default now(),
    unique (event_id, user_id)
);

create index if not exists event_attendees_user_id_idx on syncro.event_attendees (user_id);

-- ── Hilfsfunktionen ──────────────────────────────────────────────────────────

create or replace function syncro.can_read_event(p_event_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1 from syncro.events e
        where e.id = p_event_id
          and (
              e.owner_id = auth.uid()
              or (e.project_id is not null and syncro.is_project_member(e.project_id))
              or exists (
                  select 1 from syncro.event_attendees a
                  where a.event_id = e.id and a.user_id = auth.uid()
              )
          )
    );
$$;

create or replace function syncro.can_edit_event(p_event_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1 from syncro.events e
        where e.id = p_event_id
          and (
              e.owner_id = auth.uid()
              or (e.project_id is not null and syncro.can_edit_project(e.project_id))
          )
    );
$$;

revoke execute on function syncro.can_read_event(uuid) from public, anon;
revoke execute on function syncro.can_edit_event(uuid) from public, anon;
grant execute on function syncro.can_read_event(uuid) to authenticated;
grant execute on function syncro.can_edit_event(uuid) to authenticated;

-- ── Trigger ──────────────────────────────────────────────────────────────────

create or replace function syncro.events_before_write()
returns trigger
language plpgsql
as $$
begin
    if tg_op = 'INSERT' then
        new.owner_id = auth.uid();
        new.created_at = now();
    else
        new.owner_id = old.owner_id;
        new.created_at = old.created_at;
    end if;
    return new;
end;
$$;

drop trigger if exists events_before_write on syncro.events;
create trigger events_before_write
    before insert or update on syncro.events
    for each row execute function syncro.events_before_write();

drop trigger if exists events_set_updated_at on syncro.events;
create trigger events_set_updated_at
    before update on syncro.events
    for each row execute function syncro.set_updated_at();

-- ── Policies: events ─────────────────────────────────────────────────────────

alter table syncro.events enable row level security;

drop policy if exists "Users can read accessible events" on syncro.events;
create policy "Users can read accessible events"
    on syncro.events for select
    to authenticated
    using (
        owner_id = (select auth.uid())
        or (project_id is not null and syncro.is_project_member(project_id))
        or syncro.can_read_event(id)
    );

drop policy if exists "Users can create events" on syncro.events;
create policy "Users can create events"
    on syncro.events for insert
    to authenticated
    with check (project_id is null or syncro.can_edit_project(project_id));

drop policy if exists "Owners and editors can update events" on syncro.events;
create policy "Owners and editors can update events"
    on syncro.events for update
    to authenticated
    using (
        owner_id = (select auth.uid())
        or (project_id is not null and syncro.can_edit_project(project_id))
    )
    with check (project_id is null or syncro.can_edit_project(project_id) or owner_id = (select auth.uid()));

drop policy if exists "Owners and editors can delete events" on syncro.events;
create policy "Owners and editors can delete events"
    on syncro.events for delete
    to authenticated
    using (
        owner_id = (select auth.uid())
        or (project_id is not null and syncro.can_edit_project(project_id))
    );

-- ── Policies: event_attendees ────────────────────────────────────────────────

alter table syncro.event_attendees enable row level security;

drop policy if exists "Users can read attendees of visible events" on syncro.event_attendees;
create policy "Users can read attendees of visible events"
    on syncro.event_attendees for select
    to authenticated
    using (syncro.can_read_event(event_id));

drop policy if exists "Event editors can add attendees" on syncro.event_attendees;
create policy "Event editors can add attendees"
    on syncro.event_attendees for insert
    to authenticated
    with check (syncro.can_edit_event(event_id));

drop policy if exists "Event editors can remove attendees, attendees can leave" on syncro.event_attendees;
create policy "Event editors can remove attendees, attendees can leave"
    on syncro.event_attendees for delete
    to authenticated
    using (syncro.can_edit_event(event_id) or user_id = (select auth.uid()));
