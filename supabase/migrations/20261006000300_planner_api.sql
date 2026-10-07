-- VISTA only. Authenticated planning API; no service key is exposed to browsers.
begin;

create function public.save_sortie_plan(p_plan jsonb, p_sortie_id uuid default null)
returns uuid language plpgsql security definer set search_path = '' as $$
declare
  pilot public.pilots;
  plane public.aircraft;
  template public.missions;
  existing public.sorties;
  new_id uuid := coalesce(p_sortie_id,gen_random_uuid());
  departure_id uuid;
  arrival_id uuid;
  alternate_id uuid;
  plan_source text;
  plan_title text;
  flight_callsign text;
  route text;
  point jsonb;
  element_point record;
  position_no integer := 0;
  element_waypoint_id uuid;
  element_instance_id uuid;
begin
  select * into pilot from public.pilots where auth_user_id=auth.uid() and status='active' for update;
  if not found then raise exception 'An active VISTA pilot login is required'; end if;
  if jsonb_typeof(p_plan) is distinct from 'object' then raise exception 'Invalid plan'; end if;
  select * into existing from public.sorties where id=new_id for update;
  if found and (existing.pilot_id<>pilot.id or existing.status<>'planned') then
    raise exception 'Only your own planned sortie can be edited';
  end if;
  select a.* into plane from public.aircraft a join public.aircraft_types t on t.id=a.aircraft_type_id
  where a.id=(p_plan->>'aircraft_id')::uuid and a.status='available' and t.active for share of a;
  if not found then raise exception 'Selected aircraft is unavailable'; end if;
  if exists(select 1 from public.sorties where aircraft_id=plane.id and status in ('briefed','airborne')) then
    raise exception 'Selected aircraft is reserved by a live sortie';
  end if;
  plan_source := p_plan->>'source';
  plan_title := trim(p_plan->>'title');
  flight_callsign := trim(p_plan->>'callsign');
  route := coalesce(trim(p_plan->>'route_text'),'');
  if plan_title is null or length(plan_title) not between 1 and 120 then raise exception 'Enter a title of 1 to 120 characters'; end if;
  if flight_callsign is null or length(flight_callsign) not between 1 and 32 then raise exception 'Enter a flight callsign of 1 to 32 characters'; end if;
  if length(route)>8000 then raise exception 'Route text is too long'; end if;
  alternate_id := nullif(p_plan->>'alternate_base_id','')::uuid;
  if plan_source='predefined' then
    select * into template from public.missions where id=(p_plan->>'mission_id')::uuid and active for share;
    if not found then raise exception 'Selected mission is unavailable'; end if;
    if template.aircraft_type_id is not null and template.aircraft_type_id<>plane.aircraft_type_id then
      raise exception 'Aircraft type does not match this mission';
    end if;
    if template.squadron_id is not null and template.squadron_id is distinct from plane.squadron_id then
      raise exception 'Aircraft squadron does not match this mission';
    end if;
    departure_id := template.departure_base_id;
    arrival_id := template.arrival_base_id;
  elsif plan_source='custom' then
    departure_id := (p_plan->>'departure_base_id')::uuid;
    arrival_id := (p_plan->>'arrival_base_id')::uuid;
    if jsonb_typeof(p_plan->'waypoints') is distinct from 'array' then raise exception 'Provide an ordered route'; end if;
    if jsonb_array_length(p_plan->'waypoints') not between 1 and 300 then raise exception 'Provide 1 to 300 route points'; end if;
  else raise exception 'Choose predefined or custom planning'; end if;
  if departure_id is null or arrival_id is null or not exists(select 1 from public.bases where id=departure_id and active)
    or not exists(select 1 from public.bases where id=arrival_id and active)
    or (alternate_id is not null and not exists(select 1 from public.bases where id=alternate_id and active)) then
    raise exception 'Choose valid active departure, arrival and alternate airfields';
  end if;

  if existing.id is null then
    insert into public.sorties(id,pilot_id,aircraft_id,source,mission_id,mission_revision,title,
      departure_base_id,arrival_base_id,alternate_base_id,callsign,route_text,planned_departure_at)
    values(new_id,pilot.id,plane.id,plan_source,template.id,template.revision,plan_title,
      departure_id,arrival_id,alternate_id,flight_callsign,route,nullif(p_plan->>'planned_departure_at','')::timestamptz);
  else
    update public.sorties set aircraft_id=plane.id,source=plan_source,mission_id=template.id,
      mission_revision=template.revision,title=plan_title,departure_base_id=departure_id,arrival_base_id=arrival_id,
      alternate_base_id=alternate_id,callsign=flight_callsign,route_text=route,
      planned_departure_at=nullif(p_plan->>'planned_departure_at','')::timestamptz where id=new_id;
    delete from public.sortie_waypoints where sortie_id=new_id;
  end if;

  if plan_source='predefined' then
    insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_mission_waypoint_id)
    select new_id,w.position,w.identifier,w.latitude,w.longitude,w.altitude_ft,w.speed_kts,w.instructions,w.id
    from public.mission_waypoints w where mission_id=template.id order by position;
  else
    for point in select value from jsonb_array_elements(p_plan->'waypoints') loop
      if jsonb_typeof(point) is distinct from 'object' or length(trim(coalesce(point->>'identifier',''))) not between 1 and 32
        or length(coalesce(point->>'instructions',''))>2000 then raise exception 'Invalid route point'; end if;
      if nullif(point->>'source_mission_waypoint_id','') is not null then raise exception 'Custom routes cannot claim mission waypoint lineage'; end if;
      element_waypoint_id := nullif(point->>'source_element_waypoint_id','')::uuid;
      element_instance_id := nullif(point->>'element_instance','')::uuid;
      if element_waypoint_id is not null then
        select w.* into element_point from public.mission_element_waypoints w
        join public.mission_elements e on e.id=w.mission_element_id where w.id=element_waypoint_id and e.active;
        if not found or element_instance_id is null then raise exception 'Invalid mission element point'; end if;
        if element_point.identifier is distinct from point->>'identifier'
          or element_point.latitude is distinct from (point->>'latitude')::numeric
          or element_point.longitude is distinct from (point->>'longitude')::numeric
          or element_point.altitude_ft is distinct from nullif(point->>'altitude_ft','')::integer
          or element_point.speed_kts is distinct from nullif(point->>'speed_kts','')::integer
          or element_point.instructions is distinct from coalesce(point->>'instructions','') then
          raise exception 'Mission element changed; reload it or use an independent custom point';
        end if;
      elsif element_instance_id is not null then raise exception 'Element instance requires an element waypoint'; end if;
      position_no := position_no + 1;
      insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_element_waypoint_id,element_instance)
      values(new_id,position_no,trim(point->>'identifier'),(point->>'latitude')::numeric,(point->>'longitude')::numeric,
        nullif(point->>'altitude_ft','')::integer,nullif(point->>'speed_kts','')::integer,
        coalesce(point->>'instructions',''),element_waypoint_id,element_instance_id);
    end loop;
  end if;
  if not exists(select 1 from public.sortie_waypoints where sortie_id=new_id)
    or (select max(position)<>count(*) from public.sortie_waypoints where sortie_id=new_id) then
    raise exception 'Route must have consecutive points starting at 1';
  end if;
  update public.sorties set plan_snapshot=jsonb_build_object(
    'version',1,'title',plan_title,'source',plan_source,'mission_id',template.id,'mission_revision',template.revision,
    'aircraft_serial',plane.serial,'aircraft_type_id',plane.aircraft_type_id,'pilot_number',pilot.pilot_number,
    'departure_icao',(select icao from public.bases where id=departure_id),
    'arrival_icao',(select icao from public.bases where id=arrival_id),
    'route_text',route,'captured_at',now(),
    'waypoints',(select jsonb_agg(to_jsonb(w) order by position) from public.sortie_waypoints w where sortie_id=new_id)
  ) where id=new_id;
  return new_id;
end $$;

create function public.cancel_planned_sortie(p_sortie_id uuid)
returns uuid language plpgsql security definer set search_path = '' as $$
declare pilot_id uuid; s public.sorties;
begin
  select id into pilot_id from public.pilots where auth_user_id=auth.uid() and status='active' for update;
  if pilot_id is null then raise exception 'An active VISTA pilot login is required'; end if;
  select * into s from public.sorties where id=p_sortie_id for update;
  if not found or s.pilot_id<>pilot_id then raise exception 'Only your own draft can be cancelled'; end if;
  if s.status='cancelled' then return s.id; end if;
  if s.status<>'planned' then raise exception 'Only planned sorties can be cancelled here'; end if;
  update public.sorties set status='cancelled' where id=s.id;
  return s.id;
end $$;
revoke all on function public.save_sortie_plan(jsonb,uuid),public.cancel_planned_sortie(uuid) from public,anon,authenticated;
grant execute on function public.save_sortie_plan(jsonb,uuid),public.cancel_planned_sortie(uuid) to authenticated;
comment on function public.save_sortie_plan(jsonb,uuid) is
  'Pilot-owned draft planning boundary. Identity derived from auth.uid(); direct table writes remain denied.';
commit;
