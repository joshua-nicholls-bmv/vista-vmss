-- VISTA ONLY. Apply this whole new migration after complete_mission_catalogue.sql.
-- Identity, load values and waypoint coordinates are derived on the server.
begin;
create or replace function public.save_catalogue_plan(p_plan jsonb,p_sortie_id uuid default null)
returns uuid language plpgsql security definer set search_path='' as $body$
declare
  pilot public.pilots; plane public.aircraft; existing public.sorties;
  option_row public.mission_catalogue_options; family public.mission_families;
  payload public.mission_payload_presets; element public.mission_elements;
  departure public.bases; arrival public.bases; alternate_id uuid;
  new_id uuid:=coalesce(p_sortie_id,gen_random_uuid()); plan_title text; flight_callsign text;
  task jsonb; tasks jsonb:='[]'::jsonb; points jsonb:='[]'::jsonb;
  route_element_ids uuid[]:='{}'; instance_ids uuid[]:='{}'; route_element_id uuid; instance_id uuid;
  outbound boolean; return_transit boolean; direct boolean:=false;
  payload_kg integer:=0; personnel integer:=0; position_no integer:=0; i integer;
  wp public.mission_element_waypoints; departure_at timestamptz;
begin
  select * into pilot from public.pilots where auth_user_id=auth.uid() and status='active' for update;
  if not found then raise exception 'An active VISTA pilot login is required'; end if;
  if jsonb_typeof(p_plan) is distinct from 'object' then raise exception 'Invalid plan'; end if;
  plan_title:=trim(p_plan->>'title');flight_callsign:=trim(p_plan->>'callsign');
  if plan_title is null or length(plan_title) not between 1 and 120 then raise exception 'Enter a title of 1 to 120 characters'; end if;
  if flight_callsign is null or length(flight_callsign) not between 1 and 32 then raise exception 'Enter a flight callsign of 1 to 32 characters'; end if;
  if jsonb_typeof(p_plan->'tasks') is distinct from 'array' then raise exception 'Choose mission tasks'; end if;
  if jsonb_array_length(p_plan->'tasks') not between 1 and 12 then raise exception 'Choose 1 to 12 mission tasks'; end if;
  select * into existing from public.sorties where id=new_id for update;
  if found and (existing.pilot_id<>pilot.id or existing.status<>'planned') then raise exception 'Only your own planned sortie can be edited'; end if;
  select a.* into plane from public.aircraft a join public.aircraft_types t on t.id=a.aircraft_type_id
  where a.id=(p_plan->>'aircraft_id')::uuid and a.status='available' and t.active for share of a;
  if not found then raise exception 'Selected aircraft is unavailable'; end if;
  if exists(select 1 from public.sorties where aircraft_id=plane.id and status in ('briefed','airborne')) then raise exception 'Selected aircraft is reserved by a live sortie'; end if;
  select * into departure from public.bases where id=(p_plan->>'departure_base_id')::uuid and active;
  if not found then raise exception 'Choose an active departure airfield'; end if;
  select * into arrival from public.bases where id=(p_plan->>'arrival_base_id')::uuid and active;
  if not found then raise exception 'Choose an active arrival airfield'; end if;
  alternate_id:=nullif(p_plan->>'alternate_base_id','')::uuid;
  if alternate_id is not null and not exists(select 1 from public.bases where id=alternate_id and active) then raise exception 'Choose an active alternate'; end if;
  outbound:=coalesce((p_plan->>'corridor_outbound')::boolean,false);
  return_transit:=coalesce((p_plan->>'corridor_return')::boolean,false);
  if outbound then
    select * into element from public.mission_elements where code='VISTA-LICHFIELD-OUT' and active for share;
    if not found then raise exception 'Lichfield outbound transit is disabled'; end if;
    route_element_ids:=array_append(route_element_ids,element.id);instance_ids:=array_append(instance_ids,gen_random_uuid());
  end if;
  for task in select value from jsonb_array_elements(p_plan->'tasks') loop
    if jsonb_typeof(task) is distinct from 'object' then raise exception 'Invalid task'; end if;
    select * into option_row from public.mission_catalogue_options where id=(task->>'option_id')::uuid and enabled for share;
    if not found then raise exception 'A selected mission option is disabled or missing'; end if;
    select * into family from public.mission_families where code=option_row.family_code and enabled for share;
    if not found then raise exception 'A selected mission family is disabled'; end if;
    if not exists(select 1 from public.mission_family_aircraft_types where family_code=family.code and aircraft_type_id=plane.aircraft_type_id)
      then raise exception 'Aircraft type does not support this task'; end if;
    if not departure.icao=any(family.departure_icaos) then raise exception 'Departure does not support this task'; end if;
    element:=null;payload:=null;
    if option_row.destination_base_id is not null then
      direct:=true;
      if jsonb_array_length(p_plan->'tasks')<>1 or arrival.id<>option_row.destination_base_id then raise exception 'Destination missions require their selected arrival and one task'; end if;
    else
      if not arrival.icao=any(family.return_icaos) then raise exception 'Arrival does not support this training task'; end if;
      select * into element from public.mission_elements where id=option_row.element_id and active for share;
      if not found then raise exception 'A selected route element is disabled'; end if;
      route_element_ids:=array_append(route_element_ids,element.id);instance_ids:=array_append(instance_ids,gen_random_uuid());
    end if;
    if exists(select 1 from public.mission_family_payloads where family_code=family.code) then
      select p.* into payload from public.mission_payload_presets p join public.mission_family_payloads f on f.payload_id=p.id
      where p.id=nullif(task->>'payload_id','')::uuid and p.enabled and f.family_code=family.code for share of p;
      if not found then raise exception 'Choose an enabled load compatible with this task'; end if;
      if exists(select 1 from public.mission_payload_destinations where payload_id=payload.id)
        and not exists(select 1 from public.mission_payload_destinations where payload_id=payload.id and base_id=arrival.id)
        then raise exception 'Load does not support the selected destination'; end if;
      payload_kg:=payload_kg+payload.payload_kg;personnel:=personnel+payload.personnel_count;
    elsif nullif(task->>'payload_id','') is not null then raise exception 'This task does not accept a load'; end if;
    tasks:=tasks||jsonb_build_array(jsonb_build_object('option_id',option_row.id,'option_code',option_row.code,
      'family_code',family.code,'family_title',family.title,'title',option_row.title,'element_id',element.id,
      'element_revision',element.revision,'element_metadata',element.catalogue_metadata,
      'payload_id',payload.id,'payload',case when payload.id is not null then to_jsonb(payload) else null end));
  end loop;
  if return_transit then
    select * into element from public.mission_elements where code='VISTA-LICHFIELD-RETURN' and active for share;
    if not found then raise exception 'Lichfield return transit is disabled'; end if;
    route_element_ids:=array_append(route_element_ids,element.id);instance_ids:=array_append(instance_ids,gen_random_uuid());
  end if;
  if cardinality(route_element_ids)>0 then
    for i in 1..cardinality(route_element_ids) loop
      route_element_id:=route_element_ids[i];instance_id:=instance_ids[i];
      if not exists(select 1 from public.mission_element_waypoints where mission_element_id=route_element_id) then raise exception 'A route element has no waypoints'; end if;
      for wp in select * from public.mission_element_waypoints where mission_element_id=route_element_id order by position for share loop
        position_no:=position_no+1;
        points:=points||jsonb_build_array(jsonb_build_object('position',position_no,'identifier',wp.identifier,
          'latitude',wp.latitude,'longitude',wp.longitude,'altitude_ft',wp.altitude_ft,'speed_kts',wp.speed_kts,
          'instructions',wp.instructions,'source_element_waypoint_id',wp.id,'element_instance',instance_id));
      end loop;
    end loop;
  end if;
  if position_no>300 or (position_no=0 and not direct) then raise exception 'Invalid compiled route length'; end if;
  departure_at:=nullif(p_plan->>'planned_departure_at','')::timestamptz;
  if existing.id is null then
    insert into public.sorties(id,pilot_id,aircraft_id,source,title,departure_base_id,arrival_base_id,alternate_base_id,callsign,planned_departure_at)
    values(new_id,pilot.id,plane.id,'custom',plan_title,departure.id,arrival.id,alternate_id,flight_callsign,departure_at);
  else
    update public.sorties set aircraft_id=plane.id,source='custom',mission_id=null,mission_revision=null,title=plan_title,
      departure_base_id=departure.id,arrival_base_id=arrival.id,alternate_base_id=alternate_id,callsign=flight_callsign,
      route_text='',planned_departure_at=departure_at where id=new_id;
    delete from public.sortie_waypoints where sortie_id=new_id;
  end if;
  insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_element_waypoint_id,element_instance)
  select new_id,(p->>'position')::integer,p->>'identifier',(p->>'latitude')::numeric,(p->>'longitude')::numeric,
    (p->>'altitude_ft')::integer,(p->>'speed_kts')::integer,p->>'instructions',(p->>'source_element_waypoint_id')::uuid,(p->>'element_instance')::uuid
  from jsonb_array_elements(points) p;
  update public.sorties set plan_snapshot=jsonb_build_object('version',2,'planning_mode','catalogue',
    'title',plan_title,'callsign',flight_callsign,'aircraft_serial',plane.serial,'aircraft_type_id',plane.aircraft_type_id,
    'departure_icao',departure.icao,'arrival_icao',arrival.icao,'pilot_number',pilot.pilot_number,
    'catalogue_plan',jsonb_build_object('title',plan_title,'callsign',flight_callsign,'aircraft_id',plane.id,
      'departure_base_id',departure.id,'arrival_base_id',arrival.id,'alternate_base_id',alternate_id,
      'corridor_outbound',outbound,'corridor_return',return_transit,'planned_departure_at',departure_at,'tasks',tasks),
    'payload_kg',payload_kg,'personnel_count',personnel,
    'waypoints',points,'captured_at',now()) where id=new_id;
  return new_id;
end $body$;
revoke all on function public.save_catalogue_plan(jsonb,uuid) from public,anon,authenticated;
grant execute on function public.save_catalogue_plan(jsonb,uuid) to authenticated;
commit;

