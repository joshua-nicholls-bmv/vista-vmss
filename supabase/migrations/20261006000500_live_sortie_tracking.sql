-- VISTA ONLY: real sortie activation, tracking uploads and completion. Apply after migration 004.
begin;
create or replace function vista_private.guard_sortie() returns trigger language plpgsql set search_path = '' as $$
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
    if not exists(select 1 from public.sortie_waypoints where sortie_id = new.id) and not (new.plan_snapshot->>'planning_mode' = 'catalogue' and exists(select 1 from public.mission_catalogue_options o where o.id = (new.plan_snapshot #>> '{catalogue_plan,tasks,0,option_id}')::uuid and o.destination_base_id = new.arrival_base_id)) then
      raise exception 'Sortie requires a route before briefing';
    end if;
    if (select max(position) <> count(*) from public.sortie_waypoints where sortie_id = new.id) then
      raise exception 'Route positions must be consecutive starting at 1';
    end if;
  end if;
  return new;
end $$;

create or replace function public.activate_saved_sortie(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
declare p public.pilots; s public.sorties;
begin
 select * into p from public.pilots where auth_user_id=auth.uid() and status='active' for update;
 if not found then raise exception 'Active pilot login required'; end if;
 select * into s from public.sorties where id=p_sortie_id and pilot_id=p.id for update;
 if not found then raise exception 'Choose your own saved mission'; end if;
 if s.status in ('briefed','airborne') then return s.id; end if;
 if s.status<>'planned' then raise exception 'Only planned missions can be activated'; end if;
 perform 1 from public.aircraft a join public.aircraft_types t on t.id=a.aircraft_type_id where a.id=s.aircraft_id and a.status='available' and t.active for update of a;
 if not found then raise exception 'Aircraft unavailable'; end if;
 if exists(select 1 from public.sorties where id<>s.id and status in ('briefed','airborne') and (pilot_id=p.id or aircraft_id=s.aircraft_id)) then raise exception 'Pilot or aircraft already has an active sortie'; end if;
 if not exists(select 1 from public.bases where id=s.departure_base_id and active) or not exists(select 1 from public.bases where id=s.arrival_base_id and active) then raise exception 'Airfield unavailable'; end if;
 if s.plan_snapshot->>'planning_mode'='catalogue' then
  if exists(select 1 from jsonb_array_elements(s.plan_snapshot #> '{catalogue_plan,tasks}') x
   left join public.mission_catalogue_options o on o.id=(x->>'option_id')::uuid
   left join public.mission_families f on f.code=o.family_code
   left join public.mission_family_aircraft_types ft on ft.family_code=o.family_code and ft.aircraft_type_id=(select aircraft_type_id from public.aircraft where id=s.aircraft_id)
   left join public.mission_elements e on e.id=o.element_id
   left join public.mission_payload_presets l on l.id=(x->>'payload_id')::uuid
   where o.id is null or ft.aircraft_type_id is null or not o.enabled or not f.enabled or (o.element_id is not null and not e.active) or (x->>'payload_id' is not null and not l.enabled)) then raise exception 'Saved mission contains disabled catalogue choices'; end if;
  if (s.plan_snapshot #>> '{catalogue_plan,corridor_outbound}')::boolean and not exists(select 1 from public.mission_elements where code='VISTA-LICHFIELD-OUT' and active)
   or (s.plan_snapshot #>> '{catalogue_plan,corridor_return}')::boolean and not exists(select 1 from public.mission_elements where code='VISTA-LICHFIELD-RETURN' and active) then raise exception 'Saved corridor is disabled'; end if;
 end if;
 update public.sorties set status='briefed' where id=s.id;
 return s.id;
end $$;
create or replace function public.start_sortie_tracking(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
declare s public.sorties;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found then raise exception 'Choose your own active sortie'; end if;
 if s.status='airborne' then return s.id; end if;
 if s.status<>'briefed' then raise exception 'Activate a saved mission first'; end if;
 update public.sorties set status='airborne',started_at=now() where id=s.id;return s.id;
end $$;
create or replace function public.upload_sortie_track(p_sortie_id uuid,p_points jsonb) returns integer
language plpgsql security definer set search_path='' as $$
declare s public.sorties; point jsonb; old public.track_points; count_points integer:=0; seq bigint; measured timestamptz;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found or s.status<>'airborne' then raise exception 'An owned tracking sortie is required'; end if;
 if jsonb_typeof(p_points) is distinct from 'array' or jsonb_array_length(p_points) not between 1 and 100 then raise exception 'Upload 1 to 100 track points'; end if;
 for point in select value from jsonb_array_elements(p_points) loop
  seq:=(point->>'sequence')::bigint;measured:=(point->>'measured_at')::timestamptz;
  if seq is null or measured is null or measured<s.started_at-interval '10 minutes' or measured>now()+interval '10 minutes' then raise exception 'Invalid track timing'; end if;
  if exists(select 1 from public.track_points where sortie_id=s.id and ((sequence<seq and measured_at>measured) or (sequence>seq and measured_at<measured))) then raise exception 'Track timestamps must follow sequence order'; end if;
  select * into old from public.track_points where sortie_id=s.id and sequence=seq;
  if found then
   if old.measured_at is distinct from measured or old.latitude is distinct from round((point->>'latitude')::numeric,6) or old.longitude is distinct from round((point->>'longitude')::numeric,6)
    or old.altitude_ft is distinct from (point->>'altitude_ft')::numeric or old.groundspeed_kts is distinct from (point->>'groundspeed_kts')::numeric
    or old.heading_deg is distinct from (point->>'heading_deg')::numeric or old.on_ground is distinct from (point->>'on_ground')::boolean or old.fuel_kg is distinct from (point->>'fuel_kg')::numeric then raise exception 'Conflicting track retry'; end if;
  else
   insert into public.track_points(sortie_id,sequence,measured_at,latitude,longitude,altitude_ft,groundspeed_kts,heading_deg,on_ground,fuel_kg)
   values(s.id,seq,measured,(point->>'latitude')::numeric,(point->>'longitude')::numeric,(point->>'altitude_ft')::numeric,(point->>'groundspeed_kts')::numeric,(point->>'heading_deg')::numeric,(point->>'on_ground')::boolean,(point->>'fuel_kg')::numeric);
  end if;count_points:=count_points+1;
 end loop;return count_points;
end $$;
create or replace function public.finish_tracked_sortie(p_sortie_id uuid,p_debrief jsonb) returns uuid
language plpgsql security definer set search_path='' as $$
declare s public.sorties;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found then raise exception 'Owned sortie required'; end if;
 if s.status='completed' then return s.id; end if;
 if jsonb_typeof(p_debrief) is distinct from 'object' or (p_debrief->>'ended_at')::timestamptz > now()+interval '10 minutes' or length(coalesce(p_debrief->>'pilot_notes',''))>10000 then raise exception 'Invalid debrief'; end if;
 if not exists(select 1 from public.track_points where sortie_id=s.id and not on_ground) or
  not coalesce((select on_ground from public.track_points where sortie_id=s.id order by sequence desc limit 1),false) then raise exception 'Record airborne tracking and a final ground point before completion'; end if;
 perform public.complete_sortie(s.id,(p_debrief->>'ended_at')::timestamptz,(p_debrief->>'flight_seconds')::bigint,(p_debrief->>'distance_nm')::numeric,'successful',coalesce(p_debrief->>'pilot_notes',''),coalesce(p_debrief->'events','[]'::jsonb));
 update public.sortie_debriefs set landing_rate_fpm=(p_debrief->>'landing_rate_fpm')::numeric,fuel_used_kg=(p_debrief->>'fuel_used_kg')::numeric where sortie_id=s.id;
 update public.aircraft set current_base_id=s.arrival_base_id where id=s.aircraft_id;
 return s.id;
end $$;
create or replace function public.cancel_active_sortie(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
begin
 perform 1 from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) and status in ('briefed','airborne') for update;
 if not found then raise exception 'Owned active sortie required'; end if;
 update public.sorties set status='cancelled' where id=p_sortie_id;return p_sortie_id;
end $$;
create or replace function public.duplicate_saved_sortie(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
declare s public.sorties; new_id uuid:=gen_random_uuid();
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id);
 if not found then raise exception 'Choose your own saved mission'; end if;
 insert into public.sorties(id,pilot_id,aircraft_id,source,mission_id,mission_revision,title,departure_base_id,arrival_base_id,alternate_base_id,callsign,route_text,plan_snapshot)
 values(new_id,s.pilot_id,s.aircraft_id,s.source,s.mission_id,s.mission_revision,s.title,s.departure_base_id,s.arrival_base_id,s.alternate_base_id,s.callsign,s.route_text,s.plan_snapshot);
 insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_mission_waypoint_id,source_element_waypoint_id,element_instance)
 select new_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_mission_waypoint_id,source_element_waypoint_id,element_instance from public.sortie_waypoints where sortie_id=s.id;
 return new_id;
end $$;
revoke all on function public.duplicate_saved_sortie(uuid) from public,anon,authenticated;
grant execute on function public.duplicate_saved_sortie(uuid) to authenticated;
-- Mission control is restricted to admins in both UI and server.
create or replace function public.set_mission_catalogue_enabled(p_kind text,p_code text,p_enabled boolean)
returns void language plpgsql security definer set search_path='' as $toggle$
begin
  if not exists(select 1 from public.pilots where auth_user_id=auth.uid() and status='active' and role='admin') then raise exception 'Active admin access required'; end if;
  if p_enabled is null then raise exception 'Provide an enabled state'; end if;
  if p_kind='family' then update public.mission_families set enabled=p_enabled where code=p_code;
  elsif p_kind='option' then update public.mission_catalogue_options set enabled=p_enabled where code=p_code;
  elsif p_kind='payload' then update public.mission_payload_presets set enabled=p_enabled where code=p_code;
  elsif p_kind='element' then update public.mission_elements set active=p_enabled where code=p_code;
  else raise exception 'Unknown catalogue kind'; end if;
  if not found then raise exception 'Catalogue item not found'; end if;
  update public.missions m set active=f.enabled and o.enabled and coalesce(e.active,true)
  from public.mission_catalogue_options o join public.mission_families f on f.code=o.family_code
  left join public.mission_elements e on e.id=o.element_id
  where m.catalogue_option_id=o.id and m.active is distinct from (f.enabled and o.enabled and coalesce(e.active,true));
end $toggle$;

revoke all on function public.activate_saved_sortie(uuid) from public,anon,authenticated;
grant execute on function public.activate_saved_sortie(uuid) to authenticated;

revoke all on function public.start_sortie_tracking(uuid) from public,anon,authenticated;
grant execute on function public.start_sortie_tracking(uuid) to authenticated;

revoke all on function public.upload_sortie_track(uuid,jsonb) from public,anon,authenticated;
grant execute on function public.upload_sortie_track(uuid,jsonb) to authenticated;

revoke all on function public.finish_tracked_sortie(uuid,jsonb) from public,anon,authenticated;
grant execute on function public.finish_tracked_sortie(uuid,jsonb) to authenticated;

revoke all on function public.cancel_active_sortie(uuid) from public,anon,authenticated;
grant execute on function public.cancel_active_sortie(uuid) to authenticated;
commit;
