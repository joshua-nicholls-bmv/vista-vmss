-- VISTA only. Apply once to an empty, dedicated Supabase project.
begin;
create schema vista_private;
revoke all on schema vista_private from public;

create table public.bases (
  id uuid primary key default gen_random_uuid(),
  icao text not null unique check (icao ~ '^[A-Z]{4}$'),
  name text not null check (length(trim(name)) > 0),
  category text not null default 'secondary' check (category in ('primary','secondary')),
  active boolean not null default true,
  created_at timestamptz not null default now()
);
create table public.aircraft_types (
  id uuid primary key default gen_random_uuid(),
  code text not null unique,
  name text not null,
  simbrief_aircraft_code text,
  simbrief_profile_id text,
  active boolean not null default true
);
create table public.squadrons (
  id uuid primary key default gen_random_uuid(),
  code text not null unique,
  name text not null,
  base_id uuid not null references public.bases(id),
  aircraft_type_id uuid references public.aircraft_types(id),
  role text not null,
  active boolean not null default true,
  created_at timestamptz not null default now()
);
create table public.aircraft (
  id uuid primary key default gen_random_uuid(),
  serial text not null unique,
  aircraft_type_id uuid not null references public.aircraft_types(id),
  squadron_id uuid references public.squadrons(id),
  home_base_id uuid not null references public.bases(id),
  current_base_id uuid references public.bases(id),
  status text not null default 'available' check (status in ('available','maintenance','retired')),
  created_at timestamptz not null default now()
);
create table public.pilots (
  id uuid primary key default gen_random_uuid(),
  auth_user_id uuid not null unique references auth.users(id),
  pilot_number text not null unique,
  callsign text not null unique,
  display_name text not null,
  simbrief_user_id text,
  home_base_id uuid references public.bases(id),
  squadron_id uuid references public.squadrons(id),
  role text not null default 'pilot' check (role in ('pilot','operations','admin')),
  status text not null default 'active' check (status in ('active','suspended','inactive')),
  created_at timestamptz not null default now()
);
create table public.missions (
  id uuid primary key default gen_random_uuid(),
  code text not null unique,
  title text not null,
  description text not null default '',
  mission_type text not null,
  departure_base_id uuid not null references public.bases(id),
  arrival_base_id uuid not null references public.bases(id),
  squadron_id uuid references public.squadrons(id),
  aircraft_type_id uuid references public.aircraft_types(id),
  estimated_minutes integer check (estimated_minutes > 0),
  revision integer not null default 1 check (revision > 0),
  active boolean not null default true,
  created_at timestamptz not null default now()
);
create table public.mission_elements (
  id uuid primary key default gen_random_uuid(),
  code text not null unique,
  title text not null,
  description text not null default '',
  element_type text not null,
  revision integer not null default 1 check (revision > 0),
  active boolean not null default true,
  created_at timestamptz not null default now()
);
create table public.mission_waypoints (
  id uuid primary key default gen_random_uuid(),
  mission_id uuid not null references public.missions(id) on delete cascade,
  position integer not null check (position > 0),
  identifier text not null,
  latitude numeric(9,6) not null check (latitude between -90 and 90),
  longitude numeric(10,6) not null check (longitude between -180 and 180),
  altitude_ft integer check (altitude_ft between -2000 and 100000),
  speed_kts integer check (speed_kts between 0 and 3000),
  instructions text not null default '',
  unique (mission_id, position)
);
create table public.mission_element_waypoints (
  id uuid primary key default gen_random_uuid(),
  mission_element_id uuid not null references public.mission_elements(id) on delete cascade,
  position integer not null check (position > 0),
  identifier text not null,
  latitude numeric(9,6) not null check (latitude between -90 and 90),
  longitude numeric(10,6) not null check (longitude between -180 and 180),
  altitude_ft integer check (altitude_ft between -2000 and 100000),
  speed_kts integer check (speed_kts between 0 and 3000),
  instructions text not null default '',
  unique (mission_element_id, position)
);
create table public.sorties (
  id uuid primary key default gen_random_uuid(),
  pilot_id uuid not null references public.pilots(id),
  aircraft_id uuid not null references public.aircraft(id),
  source text not null check (source in ('predefined','custom')),
  mission_id uuid references public.missions(id),
  mission_revision integer check (mission_revision > 0),
  title text not null,
  departure_base_id uuid not null references public.bases(id),
  arrival_base_id uuid not null references public.bases(id),
  alternate_base_id uuid references public.bases(id),
  callsign text not null,
  status text not null default 'planned' check (status in ('planned','briefed','airborne','completed','cancelled')),
  route_text text not null default '',
  plan_snapshot jsonb not null default '{}'::jsonb check (jsonb_typeof(plan_snapshot) = 'object'),
  planned_departure_at timestamptz,
  started_at timestamptz,
  ended_at timestamptz,
  created_at timestamptz not null default now(),
  check ((source = 'predefined' and mission_id is not null and mission_revision is not null)
      or (source = 'custom' and mission_id is null and mission_revision is null)),
  check (ended_at is null or (started_at is not null and ended_at >= started_at)),
  check (status not in ('airborne','completed') or started_at is not null),
  check (status <> 'completed' or ended_at is not null)
);
create unique index sorties_one_live_pilot on public.sorties(pilot_id) where status in ('briefed','airborne');
create unique index sorties_one_live_aircraft on public.sorties(aircraft_id) where status in ('briefed','airborne');
create index sorties_pilot_created on public.sorties(pilot_id,created_at desc);
create table public.sortie_waypoints (
  id uuid primary key default gen_random_uuid(),
  sortie_id uuid not null references public.sorties(id) on delete cascade,
  position integer not null check (position > 0),
  identifier text not null,
  latitude numeric(9,6) not null check (latitude between -90 and 90),
  longitude numeric(10,6) not null check (longitude between -180 and 180),
  altitude_ft integer check (altitude_ft between -2000 and 100000),
  speed_kts integer check (speed_kts between 0 and 3000),
  instructions text not null default '',
  source_mission_waypoint_id uuid references public.mission_waypoints(id),
  source_element_waypoint_id uuid references public.mission_element_waypoints(id),
  element_instance uuid,
  unique (sortie_id, position),
  check (num_nonnulls(source_mission_waypoint_id, source_element_waypoint_id) <= 1)
);
create table public.simbrief_exports (
  id uuid primary key default gen_random_uuid(),
  sortie_id uuid not null references public.sorties(id),
  request_key uuid not null unique,
  status text not null default 'pending' check (status in ('pending','succeeded','failed')),
  route_text text not null,
  request_payload jsonb not null check (jsonb_typeof(request_payload) = 'object'),
  response_payload jsonb,
  simbrief_flight_id text,
  ofp_url text,
  error_message text,
  created_at timestamptz not null default now(),
  completed_at timestamptz,
  check (status = 'pending' or completed_at is not null)
);
create index simbrief_exports_sortie on public.simbrief_exports(sortie_id,created_at desc);
create table public.track_points (
  sortie_id uuid not null references public.sorties(id),
  sequence bigint not null check (sequence >= 0),
  measured_at timestamptz not null,
  received_at timestamptz not null default now(),
  latitude numeric(9,6) not null check (latitude between -90 and 90),
  longitude numeric(10,6) not null check (longitude between -180 and 180),
  altitude_ft numeric not null check (altitude_ft between -2000 and 100000),
  groundspeed_kts numeric not null check (groundspeed_kts between 0 and 3000),
  heading_deg numeric check (heading_deg >= 0 and heading_deg < 360),
  on_ground boolean not null,
  fuel_kg numeric check (fuel_kg >= 0),
  primary key (sortie_id,sequence)
);
create index track_points_time on public.track_points(sortie_id,measured_at);
create table public.sortie_debriefs (
  sortie_id uuid primary key references public.sorties(id),
  flight_seconds bigint not null check (flight_seconds >= 0),
  distance_nm numeric(12,3) not null check (distance_nm >= 0),
  landing_rate_fpm numeric,
  fuel_used_kg numeric check (fuel_used_kg >= 0),
  outcome text not null check (outcome in ('successful','partial','unsuccessful')),
  pilot_notes text not null default '',
  events jsonb not null default '[]'::jsonb check (jsonb_typeof(events) = 'array'),
  submitted_at timestamptz not null default now()
);

-- A lifecycle record is retained for both active and completed sorties.
create view public.active_sorties with (security_invoker = true) as
select * from public.sorties where status in ('briefed','airborne');
create view public.completed_sorties with (security_invoker = true) as
select s.*, d.flight_seconds, d.distance_nm, d.outcome, d.pilot_notes, d.events
from public.sorties s join public.sortie_debriefs d on d.sortie_id = s.id where s.status = 'completed';
-- Live aggregation avoids drift and double counting from retried completions.
create view public.pilot_statistics with (security_invoker = true) as
select p.id as pilot_id, count(d.sortie_id) as completed_sorties,
coalesce(sum(d.flight_seconds),0) as flight_seconds,
round(coalesce(sum(d.flight_seconds),0)::numeric / 3600, 2) as flight_hours,
coalesce(sum(d.distance_nm),0) as distance_nm,
max(s.ended_at) as last_completed_at
from public.pilots p left join public.sorties s on s.pilot_id = p.id and s.status = 'completed'
left join public.sortie_debriefs d on d.sortie_id = s.id group by p.id;

create function vista_private.can_operate() returns boolean language sql stable
security definer set search_path = '' as $$
select exists(select 1 from public.pilots where auth_user_id = (select auth.uid())
and status = 'active' and role in ('operations','admin'));
$$;
create function vista_private.owns_pilot(p_id uuid) returns boolean language sql stable
security definer set search_path = '' as $$
select exists(select 1 from public.pilots where id = p_id and auth_user_id = (select auth.uid()) and status = 'active');
$$;
create function vista_private.can_read_sortie(s_id uuid) returns boolean language sql stable
security definer set search_path = '' as $$
select vista_private.can_operate() or exists(select 1 from public.sorties s
where s.id = s_id and vista_private.owns_pilot(s.pilot_id));
$$;
revoke all on all functions in schema vista_private from public;
grant usage on schema vista_private to authenticated;
grant execute on all functions in schema vista_private to authenticated;

do $$ declare t text; begin
  foreach t in array array['bases','aircraft_types','squadrons','aircraft','missions','mission_waypoints','mission_elements','mission_element_waypoints'] loop
    execute format('alter table public.%I enable row level security', t);
    execute format('create policy catalogue_read on public.%I for select to authenticated using (exists (select 1 from public.pilots where auth_user_id = (select auth.uid()) and status = ''active''))',t);
  end loop;
  foreach t in array array['pilots','sorties','sortie_waypoints','track_points','sortie_debriefs','simbrief_exports'] loop
    execute format('alter table public.%I enable row level security', t);
  end loop;
end $$;
create policy pilot_read on public.pilots for select to authenticated
using (vista_private.owns_pilot(id) or vista_private.can_operate());
create policy sortie_read on public.sorties for select to authenticated
using (vista_private.owns_pilot(pilot_id) or vista_private.can_operate());
do $$ declare t text; begin
  foreach t in array array['sortie_waypoints','track_points','sortie_debriefs','simbrief_exports'] loop
    execute format('create policy own_sortie_read on public.%I for select to authenticated using (vista_private.can_read_sortie(sortie_id))',t);
  end loop;
end $$;
-- Explicit grants: all writes go through the future trusted backend.
do $$ declare t text; begin
  foreach t in array array['bases','aircraft_types','squadrons','aircraft','pilots','missions','mission_waypoints','mission_elements','mission_element_waypoints','sorties','sortie_waypoints','simbrief_exports','track_points','sortie_debriefs'] loop
    execute format('revoke all on public.%I from anon, authenticated',t);
    execute format('grant select on public.%I to authenticated',t);
    execute format('grant all on public.%I to service_role',t);
  end loop;
end $$;
revoke all on public.active_sorties,public.completed_sorties,public.pilot_statistics from anon,authenticated;
grant select on public.active_sorties,public.completed_sorties,public.pilot_statistics to authenticated,service_role;

create function vista_private.guard_sortie() returns trigger language plpgsql set search_path = '' as $$
begin
  if TG_OP = 'INSERT' then
    if new.status <> 'planned' then raise exception 'Create sorties in planned state'; end if;
  else
    if old.status in ('completed','cancelled') then raise exception 'Terminal sorties are immutable'; end if;
    if new.status <> old.status and not (
      (old.status = 'planned' and new.status in ('briefed','cancelled')) or
      (old.status = 'briefed' and new.status in ('airborne','cancelled')) or
      (old.status = 'airborne' and new.status in ('completed','cancelled'))
    ) then raise exception 'Invalid sortie transition'; end if;
    if old.status <> 'planned' and
      (to_jsonb(new) - array['status','started_at','ended_at']) is distinct from
      (to_jsonb(old) - array['status','started_at','ended_at']) then
      raise exception 'Plan is frozen after briefing';
    end if;
    if old.status = 'airborne' and new.started_at is distinct from old.started_at then
      raise exception 'Takeoff time is frozen';
    end if;
  end if;
  if new.status in ('briefed','airborne') then
    if not exists(select 1 from public.pilots where id = new.pilot_id and status = 'active') or
       not exists(select 1 from public.aircraft where id = new.aircraft_id and status = 'available') then
      raise exception 'Pilot or aircraft unavailable';
    end if;
    if not exists(select 1 from public.sortie_waypoints where sortie_id = new.id) then
      raise exception 'Sortie requires a route before briefing';
    end if;
    if (select max(position) <> count(*) from public.sortie_waypoints where sortie_id = new.id) then
      raise exception 'Route positions must be consecutive starting at 1';
    end if;
  end if;
  return new;
end $$;
create trigger guard_sortie before insert or update on public.sorties for each row execute function vista_private.guard_sortie();
create function vista_private.guard_route() returns trigger language plpgsql set search_path = '' as $$
declare s_id uuid;
begin
  if TG_OP = 'UPDATE' and new.sortie_id <> old.sortie_id then raise exception 'Cannot move route points'; end if;
  if TG_OP = 'DELETE' then s_id := old.sortie_id; else s_id := new.sortie_id; end if;
  perform 1 from public.sorties where id = s_id and status = 'planned' for update;
  if not found then raise exception 'Route is editable only while planned'; end if;
  if TG_OP = 'DELETE' then return old; end if;
  return new;
end $$;
create trigger guard_route before insert or update or delete on public.sortie_waypoints for each row execute function vista_private.guard_route();

-- Deferred check permits atomic completion while preventing orphan debriefs.
create function vista_private.check_completion() returns trigger language plpgsql set search_path = '' as $$
declare s_id uuid; s_status text; has_debrief boolean;
begin
  if TG_TABLE_NAME = 'sorties' then
    if TG_OP = 'DELETE' then s_id := old.id; else s_id := new.id; end if;
  else
    if TG_OP = 'UPDATE' and old.sortie_id <> new.sortie_id then
      raise exception 'Cannot move a debrief to another sortie';
    end if;
    if TG_OP = 'DELETE' then s_id := old.sortie_id; else s_id := new.sortie_id; end if;
  end if;
  select status into s_status from public.sorties where id = s_id;
  select exists(select 1 from public.sortie_debriefs where sortie_id = s_id) into has_debrief;
  if (s_status = 'completed') is distinct from has_debrief and s_status is not null then
    raise exception 'Completed sortie must have exactly one debrief; other states must have none';
  end if;
  return null;
end $$;
create constraint trigger sortie_completion_consistency after insert or update or delete on public.sorties
deferrable initially deferred for each row execute function vista_private.check_completion();
create constraint trigger debrief_completion_consistency after insert or update or delete on public.sortie_debriefs
deferrable initially deferred for each row execute function vista_private.check_completion();
revoke all on function vista_private.check_completion() from public,anon,authenticated;

create function public.complete_sortie(p_sortie_id uuid, p_ended_at timestamptz,
  p_flight_seconds bigint, p_distance_nm numeric, p_outcome text,
  p_pilot_notes text default '', p_events jsonb default '[]'::jsonb)
returns uuid language plpgsql security definer set search_path = '' as $$
declare s public.sorties; d public.sortie_debriefs;
begin
  select * into s from public.sorties where id = p_sortie_id for update;
  if not found then raise exception 'Unknown sortie'; end if;
  if s.status = 'completed' then
    select * into d from public.sortie_debriefs where sortie_id = s.id;
    if s.ended_at is not distinct from p_ended_at and
       d.flight_seconds is not distinct from p_flight_seconds and
       d.distance_nm is not distinct from round(p_distance_nm,3) and
       d.outcome is not distinct from p_outcome and
       d.pilot_notes is not distinct from p_pilot_notes and d.events is not distinct from p_events then
      return s.id;
    end if;
    raise exception 'Conflicting completion retry';
  end if;
  if s.status <> 'airborne' then raise exception 'Only airborne sorties can complete'; end if;
  if p_ended_at is null or p_ended_at < s.started_at or
     p_flight_seconds > extract(epoch from (p_ended_at - s.started_at)) then
    raise exception 'Invalid completion timing';
  end if;
  insert into public.sortie_debriefs(sortie_id,flight_seconds,distance_nm,outcome,pilot_notes,events)
  values(s.id,p_flight_seconds,p_distance_nm,p_outcome,p_pilot_notes,p_events);
  update public.sorties set status = 'completed', ended_at = p_ended_at where id = s.id;
  return s.id;
end $$;
revoke all on function public.complete_sortie(uuid,timestamptz,bigint,numeric,text,text,jsonb) from public,anon,authenticated;
grant execute on function public.complete_sortie(uuid,timestamptz,bigint,numeric,text,text,jsonb) to service_role;
revoke all on function vista_private.guard_sortie(),vista_private.guard_route() from public,anon,authenticated;

-- Leading columns of existing unique/composite indexes already cover route/track owners.
create index squadrons_base on public.squadrons(base_id);
create index squadrons_type on public.squadrons(aircraft_type_id);
create index aircraft_type on public.aircraft(aircraft_type_id);
create index aircraft_squadron on public.aircraft(squadron_id);
create index aircraft_home_base on public.aircraft(home_base_id);
create index aircraft_current_base on public.aircraft(current_base_id);
create index pilots_base on public.pilots(home_base_id);
create index pilots_squadron on public.pilots(squadron_id);
create index missions_departure on public.missions(departure_base_id);
create index missions_arrival on public.missions(arrival_base_id);
create index missions_squadron on public.missions(squadron_id);
create index missions_type on public.missions(aircraft_type_id);
create index sorties_aircraft on public.sorties(aircraft_id);
create index sorties_mission on public.sorties(mission_id);
create index sorties_departure on public.sorties(departure_base_id);
create index sorties_arrival on public.sorties(arrival_base_id);
create index sorties_alternate on public.sorties(alternate_base_id);
create index sortie_waypoints_mission_source on public.sortie_waypoints(source_mission_waypoint_id);
create index sortie_waypoints_element_source on public.sortie_waypoints(source_element_waypoint_id);

-- Only agreed primary bases are seeded; secondary airfields use the same table.
insert into public.bases(icao,name,category) values
('EGXC','RAF Coningsby','primary'),('EGQS','RAF Lossiemouth','primary'),('EGVN','RAF Brize Norton','primary');
commit;
