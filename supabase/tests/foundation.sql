-- Run against a local Supabase database after the migration. All fixtures roll back.
begin;
create function pg_temp.assert_true(value boolean, message text) returns void language plpgsql as $$
begin if value is distinct from true then raise exception 'FAIL: %', message; end if; end $$;
create function pg_temp.expect_error(statement text, expected text) returns void language plpgsql as $$
declare failed boolean := false;
begin
  begin execute statement;
  exception when others then
    if position(expected in SQLERRM) = 0 then raise exception 'Unexpected error: %', SQLERRM; end if;
    failed := true;
  end;
  if not failed then raise exception 'Expected rejection: %', statement; end if;
end $$;
select pg_temp.assert_true((select count(*) = 3 from public.bases where category = 'primary'),'primary bases');
insert into public.bases(icao,name) values ('ZZZZ','Test Secondary Airfield');
select pg_temp.assert_true((select category='secondary' from public.bases where icao='ZZZZ'),'secondary airfield');
insert into auth.users(id) values ('00000000-0000-0000-0000-000000000001'),('00000000-0000-0000-0000-000000000002');
insert into public.pilots(id,auth_user_id,pilot_number,callsign,display_name) values
('10000000-0000-0000-0000-000000000001','00000000-0000-0000-0000-000000000001','VISTA001','TEST1','Test Pilot One'),
('10000000-0000-0000-0000-000000000002','00000000-0000-0000-0000-000000000002','VISTA002','TEST2','Test Pilot Two');
insert into public.aircraft_types(id,code,name) values ('20000000-0000-0000-0000-000000000001','TEST','Test Type');
insert into public.aircraft(id,serial,aircraft_type_id,home_base_id)
select '30000000-0000-0000-0000-000000000001','TEST001','20000000-0000-0000-0000-000000000001',id from public.bases where icao='EGXC';
insert into public.missions(id,code,title,mission_type,departure_base_id,arrival_base_id)
select '50000000-0000-0000-0000-000000000001','TEST','Test Mission','training',id,id from public.bases where icao='EGXC';
insert into public.mission_waypoints(id,mission_id,position,identifier,latitude,longitude) values
('60000000-0000-0000-0000-000000000001','50000000-0000-0000-0000-000000000001',1,'POINT',53,0);
insert into public.mission_elements(id,code,title,element_type) values
('70000000-0000-0000-0000-000000000001','ELEMENT','Test Element','training');
insert into public.mission_element_waypoints(id,mission_element_id,position,identifier,latitude,longitude) values
('80000000-0000-0000-0000-000000000001','70000000-0000-0000-0000-000000000001',1,'ELEMENT1',54,0);
insert into public.sorties(id,pilot_id,aircraft_id,source,mission_id,mission_revision,title,departure_base_id,arrival_base_id,callsign)
select '40000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000002',
'30000000-0000-0000-0000-000000000001','predefined','50000000-0000-0000-0000-000000000001',1,'Predefined Test',id,id,'TEST2' from public.bases where icao='EGXC';
insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,source_mission_waypoint_id)
select '40000000-0000-0000-0000-000000000003',position,identifier,latitude,longitude,id from public.mission_waypoints;
insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude,source_element_waypoint_id,element_instance)
select '40000000-0000-0000-0000-000000000003',2,identifier,latitude,longitude,id,gen_random_uuid() from public.mission_element_waypoints;
update public.mission_waypoints set latitude=55;
select pg_temp.assert_true((select latitude=53 from public.sortie_waypoints where sortie_id='40000000-0000-0000-0000-000000000003' and position=1),'template independent snapshot');
insert into public.sorties(id,pilot_id,aircraft_id,source,title,departure_base_id,arrival_base_id,callsign)
select '40000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000001',
'30000000-0000-0000-0000-000000000001','custom','Test Flight',id,id,'TEST1' from public.bases where icao='EGXC';
select pg_temp.expect_error($q$update public.sorties set status='briefed'$q$,'requires a route');
insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude) values
('40000000-0000-0000-0000-000000000001',1,'TEST',53,0);
select pg_temp.expect_error($q$insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude)
values ('40000000-0000-0000-0000-000000000001',1,'DUP',53,0)$q$,'unique constraint');
select pg_temp.expect_error($q$insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude)
values ('40000000-0000-0000-0000-000000000001',2,'BAD',91,0)$q$,'check constraint');
update public.sorties set status='briefed' where id='40000000-0000-0000-0000-000000000001';
select pg_temp.expect_error($q$update public.sortie_waypoints set latitude=54$q$,'editable only while planned');
select pg_temp.expect_error($q$update public.sorties set route_text='ALTERED'$q$,'Plan is frozen');
select pg_temp.expect_error($q$update public.sorties set status='completed',started_at=now(),ended_at=now()$q$,'Invalid sortie transition');
insert into public.sorties(id,pilot_id,aircraft_id,source,title,departure_base_id,arrival_base_id,callsign)
select '40000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002',
'30000000-0000-0000-0000-000000000001','custom','Other Flight',id,id,'TEST2' from public.bases where icao='EGXC';
insert into public.sortie_waypoints(sortie_id,position,identifier,latitude,longitude) values
('40000000-0000-0000-0000-000000000002',1,'TEST',53,0);
select pg_temp.expect_error($q$update public.sorties set status='briefed' where id='40000000-0000-0000-0000-000000000002'$q$,'sorties_one_live_aircraft');
update public.sorties set status='airborne',started_at='2026-10-06T10:00:00Z' where id='40000000-0000-0000-0000-000000000001';
insert into public.track_points(sortie_id,sequence,measured_at,latitude,longitude,altitude_ft,groundspeed_kts,on_ground)
values ('40000000-0000-0000-0000-000000000001',0,'2026-10-06T10:00:00Z',53,0,1000,200,false);
select pg_temp.expect_error($q$insert into public.track_points select * from public.track_points$q$,'unique constraint');
select public.complete_sortie('40000000-0000-0000-0000-000000000001','2026-10-06T11:00:00Z',3600,150,'successful');
select public.complete_sortie('40000000-0000-0000-0000-000000000001','2026-10-06T11:00:00Z',3600,150,'successful');
select pg_temp.expect_error($q$select public.complete_sortie('40000000-0000-0000-0000-000000000001','2026-10-06T11:00:00Z',3600,151,'successful')$q$,'Conflicting completion retry');
select pg_temp.assert_true((select completed_sorties=1 and flight_seconds=3600 from public.pilot_statistics where pilot_id='10000000-0000-0000-0000-000000000001'),'statistics retry safe');
set constraints all immediate;
select pg_temp.expect_error($q$insert into public.sortie_debriefs(sortie_id,flight_seconds,distance_nm,outcome)
values ('40000000-0000-0000-0000-000000000002',0,0,'successful')$q$,'Completed sortie must have');
select set_config('request.jwt.claim.sub','00000000-0000-0000-0000-000000000001',true);
set local role authenticated;
select pg_temp.assert_true((select count(*)=1 from public.pilots),'pilot RLS');
select pg_temp.assert_true((select count(*)=1 from public.sorties),'sortie RLS');
select pg_temp.assert_true((select count(*)=1 from public.pilot_statistics),'statistics view RLS');
select pg_temp.assert_true((select count(*)=1 from public.track_points),'track RLS');
select pg_temp.expect_error($q$update public.pilots set role='admin'$q$,'permission denied');
select pg_temp.expect_error($q$select public.complete_sortie('40000000-0000-0000-0000-000000000001',now(),0,0,'successful')$q$,'permission denied');
reset role;
update public.pilots set role='operations' where pilot_number='VISTA001';
set local role authenticated;
select pg_temp.assert_true((select count(*)=2 from public.pilots),'operations access');
select pg_temp.assert_true((select count(*)=3 from public.sorties),'operations sorties');
reset role;
update public.pilots set status='suspended' where pilot_number='VISTA001';
set local role authenticated;
select pg_temp.assert_true((select count(*)=0 from public.sorties),'suspended access');
select pg_temp.assert_true((select count(*)=0 from public.bases),'suspended catalogue');
reset role;
set local role anon;
select pg_temp.expect_error($q$select * from public.pilots$q$,'permission denied');
reset role;
rollback;
