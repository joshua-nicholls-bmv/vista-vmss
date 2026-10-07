-- VISTA ONLY. Apply after migration 012. Shared snapshots and read-only fleet reservations.
begin;
create table if not exists public.shared_missions (
 id uuid primary key default gen_random_uuid(), owner_id uuid not null references public.pilots(id),
 plan_id uuid not null, title text not null, squadron text not null, author text not null,
 departure_icao text not null, arrival_icao text not null, revision integer not null default 1,
 active boolean not null default true, document jsonb not null, updated_at timestamptz not null default now(),
 unique(owner_id,plan_id));
alter table public.shared_missions enable row level security;
drop policy if exists shared_missions_read on public.shared_missions;
create policy shared_missions_read on public.shared_missions for select to authenticated using(
 exists(select 1 from public.pilots where auth_user_id=auth.uid() and status='active') and (active or vista_private.owns_pilot(owner_id)));
revoke all on public.shared_missions from public,anon,authenticated;
grant select on public.shared_missions to authenticated;
create or replace function public.publish_mission(p_sortie_id uuid) returns uuid language plpgsql security definer set search_path='' as $$
declare s public.sorties; p public.pilots; doc jsonb; share_id uuid; unit_name text;
begin
 select * into p from public.pilots where auth_user_id=auth.uid() and status='active' for update;
 if not found then raise exception 'Active pilot login required'; end if;
 select * into s from public.sorties where id=p_sortie_id and pilot_id=p.id for update;
 if not found then raise exception 'Choose your own saved mission'; end if;
 select q.name into unit_name from public.aircraft a left join public.squadrons q on q.id=a.squadron_id where a.id=s.aircraft_id;
 doc:=jsonb_build_object('aircraft_id',s.aircraft_id,'source',s.source,'mission_id',s.mission_id,'mission_revision',s.mission_revision,
 'title',s.title,'departure_base_id',s.departure_base_id,'arrival_base_id',s.arrival_base_id,'alternate_base_id',s.alternate_base_id,
 'mission_notes',coalesce((select mission_notes from public.sortie_briefings where sortie_id=s.id),''),
 'callsign',s.callsign,'route_text',s.route_text,'plan_snapshot',s.plan_snapshot-'pilot_number',
 'waypoints',coalesce((select jsonb_agg(to_jsonb(w)-'id'-'sortie_id' order by position) from public.sortie_waypoints w where sortie_id=s.id),'[]'::jsonb));
 insert into public.shared_missions(owner_id,plan_id,title,squadron,author,departure_icao,arrival_icao,document)
 values(p.id,coalesce(s.mission_plan_id,s.id),s.title,coalesce(unit_name,'Unassigned'),p.display_name||' - '||p.pilot_number,
 (select icao from public.bases where id=s.departure_base_id),(select icao from public.bases where id=s.arrival_base_id),doc)
 on conflict(owner_id,plan_id) do update set title=excluded.title,squadron=excluded.squadron,document=excluded.document,
 departure_icao=excluded.departure_icao,arrival_icao=excluded.arrival_icao,author=excluded.author,active=true,
 revision=public.shared_missions.revision+1,updated_at=now() returning id into share_id;
 return share_id;
end $$;
create or replace function public.withdraw_shared_mission(p_shared_id uuid) returns uuid language plpgsql security definer set search_path='' as $$
begin
 update public.shared_missions set active=false,updated_at=now() where id=p_shared_id and vista_private.owns_pilot(owner_id);
 if not found then raise exception 'Choose your own shared mission'; end if;return p_shared_id;
end $$;
create or replace function public.import_shared_mission(p_shared_id uuid,p_request_id uuid) returns uuid language plpgsql security definer set search_path='' as $$
declare p public.pilots; shared public.shared_missions; d jsonb; existing public.sorties; existing_id uuid;
begin
 select * into p from public.pilots where auth_user_id=auth.uid() and status='active' for update;
 if not found or p_request_id is null then raise exception 'Active pilot and import request required'; end if;
 select * into shared from public.shared_missions where id=p_shared_id and active for share;
 if not found then raise exception 'Shared mission is no longer available'; end if;
 select * into existing from public.sorties where id=p_request_id;
 if found then
  if existing.pilot_id=p.id and existing.plan_snapshot->>'shared_mission_id'=shared.id::text then return existing.id; end if;
  raise exception 'Import request already used';
 end if;
 select id into existing_id from public.sorties where pilot_id=p.id and plan_snapshot->>'shared_mission_id'=shared.id::text
 and plan_snapshot->>'shared_revision'=shared.revision::text order by created_at desc limit 1;
 if found then return existing_id; end if;
 d:=shared.document;
 insert into public.sorties(id,pilot_id,aircraft_id,source,mission_id,mission_revision,title,departure_base_id,arrival_base_id,alternate_base_id,callsign,route_text,plan_snapshot)
 values(p_request_id,p.id,(d->>'aircraft_id')::uuid,d->>'source',(d->>'mission_id')::uuid,(d->>'mission_revision')::integer,d->>'title',
 (d->>'departure_base_id')::uuid,(d->>'arrival_base_id')::uuid,(d->>'alternate_base_id')::uuid,d->>'callsign',d->>'route_text',
 (d->'plan_snapshot')||jsonb_build_object('pilot_number',p.pilot_number,'shared_mission_id',shared.id,'shared_revision',shared.revision));
 insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_mission_waypoint_id,source_element_waypoint_id,element_instance)
 select p_request_id,(w->>'position')::integer,w->>'identifier',(w->>'latitude')::numeric,(w->>'longitude')::numeric,(w->>'altitude_ft')::integer,
 (w->>'speed_kts')::integer,w->>'instructions',(w->>'source_mission_waypoint_id')::uuid,(w->>'source_element_waypoint_id')::uuid,(w->>'element_instance')::uuid
 from jsonb_array_elements(d->'waypoints') w;
 insert into public.sortie_briefings(sortie_id,plan_key,mission_notes) values(p_request_id,vista_private.briefing_plan_key(p_request_id),coalesce(d->>'mission_notes',''));
 return p_request_id;
end $$;
create or replace function public.fleet_reservations() returns table(aircraft_id uuid,pilot_label text,callsign text,mission_title text,flight_state text,started_at timestamptz)
language plpgsql security definer set search_path='' as $$
begin
 if not exists(select 1 from public.pilots where auth_user_id=auth.uid() and status='active') then raise exception 'Active pilot login required'; end if;
 return query select s.aircraft_id,p.display_name||' - '||p.pilot_number,s.callsign,s.title,
 case when s.status='airborne' then 'Tracking' else 'Preparing' end,s.started_at
 from public.sorties s join public.pilots p on p.id=s.pilot_id where s.status in ('briefed','airborne');
end $$;
revoke all on function public.publish_mission(uuid),public.withdraw_shared_mission(uuid),public.import_shared_mission(uuid,uuid),public.fleet_reservations() from public,anon,authenticated;
grant execute on function public.publish_mission(uuid),public.withdraw_shared_mission(uuid),public.import_shared_mission(uuid,uuid),public.fleet_reservations() to authenticated;
commit;
