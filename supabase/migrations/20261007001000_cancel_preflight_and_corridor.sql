-- VISTA ONLY: reuse unflown cancelled plans and set Lichfield transit FL140. Apply after 009.
begin;
create or replace function vista_private.guard_sortie() returns trigger language plpgsql set search_path = '' as $$
begin
  if TG_OP = 'INSERT' then
    if new.status <> 'planned' then raise exception 'Create sorties in planned state'; end if;
  else
    if old.status in ('completed','cancelled') and not (old.status='cancelled' and new.status='planned' and old.started_at is null and not exists(select 1 from public.track_points where sortie_id=old.id) and not exists(select 1 from public.sortie_debriefs where sortie_id=old.id)) then raise exception 'Terminal sorties are immutable'; end if;
    if new.status <> old.status and not (
      (old.status in ('briefed','cancelled') and new.status='planned' and old.started_at is null and not exists(select 1 from public.track_points where sortie_id=old.id) and not exists(select 1 from public.sortie_debriefs where sortie_id=old.id)) or
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
create or replace function public.cancel_active_sortie(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
begin
 perform 1 from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) and status in ('briefed','airborne') for update;
 if not found then raise exception 'Owned active sortie required'; end if;
 if exists(select 1 from public.sorties where id=p_sortie_id and status='briefed' and started_at is null)
    and not exists(select 1 from public.track_points where sortie_id=p_sortie_id)
    and not exists(select 1 from public.sortie_debriefs where sortie_id=p_sortie_id) then
   update public.sorties set status='planned' where id=p_sortie_id;
   update public.sortie_briefings set signed_at=null,signed_by=null,signed_label=null where sortie_id=p_sortie_id;
 else
   update public.sorties set status='cancelled' where id=p_sortie_id;
 end if;
 return p_sortie_id;
end $$;
create or replace function public.duplicate_saved_sortie(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
declare s public.sorties; new_id uuid:=gen_random_uuid();
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found then raise exception 'Choose your own saved mission'; end if;
 if s.status='cancelled' and s.started_at is null
   and not exists(select 1 from public.track_points where sortie_id=s.id)
   and not exists(select 1 from public.sortie_debriefs where sortie_id=s.id) then
  update public.sorties set status='planned' where id=s.id;
  update public.sortie_briefings set signed_at=null,signed_by=null,signed_label=null where sortie_id=s.id;
  update public.sortie_waypoints w set altitude_ft=14000
   from public.mission_element_waypoints e join public.mission_elements m on m.id=e.mission_element_id
   where w.sortie_id=s.id and w.source_element_waypoint_id=e.id and m.code in ('VISTA-LICHFIELD-OUT','VISTA-LICHFIELD-RETURN');
  return s.id;
 end if;
 insert into public.sorties(id,pilot_id,aircraft_id,source,mission_id,mission_revision,title,departure_base_id,arrival_base_id,alternate_base_id,callsign,route_text,plan_snapshot)
 values(new_id,s.pilot_id,s.aircraft_id,s.source,s.mission_id,s.mission_revision,s.title,s.departure_base_id,s.arrival_base_id,s.alternate_base_id,s.callsign,s.route_text,s.plan_snapshot);
 insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_mission_waypoint_id,source_element_waypoint_id,element_instance)
 select new_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_mission_waypoint_id,source_element_waypoint_id,element_instance from public.sortie_waypoints where sortie_id=s.id;
 return new_id;
end $$;
update public.mission_element_waypoints w set altitude_ft=14000,
 instructions=concat_ws(' ',nullif(w.instructions,''),'Transit at FL140.')
 from public.mission_elements e where w.mission_element_id=e.id
 and e.code in ('VISTA-LICHFIELD-OUT','VISTA-LICHFIELD-RETURN') and w.altitude_ft is distinct from 14000;
-- Update editable saved plans only. Frozen/recorded routes retain their original history.
update public.sortie_waypoints w set altitude_ft=14000
 from public.mission_element_waypoints e join public.mission_elements m on m.id=e.mission_element_id,
 public.sorties s
 where w.source_element_waypoint_id=e.id and s.id=w.sortie_id and s.status='planned'
 and m.code in ('VISTA-LICHFIELD-OUT','VISTA-LICHFIELD-RETURN');
commit;
