-- VISTA ONLY. Apply after 010. Library folders do not change flight records.
begin;
alter table public.sorties add column if not exists mission_plan_id uuid;
create index if not exists sorties_mission_plan on public.sorties(mission_plan_id);
create table if not exists public.mission_library_archives (
 pilot_id uuid not null references public.pilots(id), plan_id uuid not null,
 archived_at timestamptz not null default now(), primary key(pilot_id,plan_id));
alter table public.mission_library_archives enable row level security;
drop policy if exists own_library_archives on public.mission_library_archives;
create policy own_library_archives on public.mission_library_archives for select to authenticated using(vista_private.owns_pilot(pilot_id));
revoke all on public.mission_library_archives from public,anon,authenticated;
grant select on public.mission_library_archives to authenticated;
create or replace function public.set_mission_archived(p_sortie_id uuid,p_archived boolean) returns uuid
language plpgsql security definer set search_path='' as $$
declare s public.sorties; root_id uuid;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found or p_archived is null then raise exception 'Owned mission required'; end if;
 perform 1 from public.pilots where id=s.pilot_id for update;
 root_id:=coalesce(s.mission_plan_id,s.id);
 if exists(select 1 from public.sorties where pilot_id=s.pilot_id and coalesce(mission_plan_id,id)=root_id and status in ('briefed','airborne')) then raise exception 'Deactivate the mission before changing its folder'; end if;
 if p_archived then insert into public.mission_library_archives(pilot_id,plan_id) values(s.pilot_id,root_id) on conflict do nothing;
 else delete from public.mission_library_archives where pilot_id=s.pilot_id and plan_id=root_id; end if;
 return root_id;
end $$;
revoke all on function public.set_mission_archived(uuid,boolean) from public,anon,authenticated;
grant execute on function public.set_mission_archived(uuid,boolean) to authenticated;
create or replace function public.duplicate_saved_sortie(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
declare s public.sorties; new_id uuid:=gen_random_uuid();
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found then raise exception 'Choose your own saved mission'; end if;
 if exists(select 1 from public.mission_library_archives where pilot_id=s.pilot_id and plan_id=coalesce(s.mission_plan_id,s.id)) then raise exception 'Restore this archived mission before flying'; end if;
 if exists(select 1 from public.mission_library_archives where pilot_id=s.pilot_id and plan_id=coalesce(s.mission_plan_id,s.id)) then raise exception 'Restore this archived mission before flying'; end if;
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
 insert into public.sorties(id,pilot_id,aircraft_id,source,mission_id,mission_revision,title,departure_base_id,arrival_base_id,alternate_base_id,callsign,route_text,plan_snapshot,mission_plan_id)
 values(new_id,s.pilot_id,s.aircraft_id,s.source,s.mission_id,s.mission_revision,s.title,s.departure_base_id,s.arrival_base_id,s.alternate_base_id,s.callsign,s.route_text,s.plan_snapshot,coalesce(s.mission_plan_id,s.id));
 insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_mission_waypoint_id,source_element_waypoint_id,element_instance)
 select new_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions,source_mission_waypoint_id,source_element_waypoint_id,element_instance from public.sortie_waypoints where sortie_id=s.id;
 return new_id;
end $$;
create or replace function public.activate_saved_sortie(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
declare p public.pilots; s public.sorties;
begin
 select * into p from public.pilots where auth_user_id=auth.uid() and status='active' for update;
 if not found then raise exception 'Active pilot login required'; end if;
 select * into s from public.sorties where id=p_sortie_id and pilot_id=p.id for update;
 if not found then raise exception 'Choose your own saved mission'; end if;
 if exists(select 1 from public.mission_library_archives where pilot_id=p.id and plan_id=coalesce(s.mission_plan_id,s.id)) then raise exception 'Restore this archived mission before activation'; end if;
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
commit;
