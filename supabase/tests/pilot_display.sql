-- Run locally after both migrations. Fixtures roll back.
begin;
insert into auth.users(id) values
('00000000-0000-0000-0000-000000000069'),
('00000000-0000-0000-0000-000000000070');
insert into public.pilots(auth_user_id,pilot_number,display_name) values
('00000000-0000-0000-0000-000000000069','TEST-69-001','Example Pilot'),
('00000000-0000-0000-0000-000000000070','TEST-69-002','Second Pilot');
do $$ begin
  if (select display_label from public.pilots where pilot_number='TEST-69-001')
    is distinct from 'Example Pilot - TEST-69-001 - Callsign pending' then
    raise exception 'Pending callsign display failed';
  end if;
end $$;
update public.pilots set callsign='EXAMPLE' where pilot_number='TEST-69-001';
do $$ begin
  if (select display_label from public.pilots where pilot_number='TEST-69-001')
    is distinct from 'Example Pilot - TEST-69-001 - EXAMPLE' then
    raise exception 'Assigned callsign display failed';
  end if;
  begin
    update public.pilots set callsign=' ' where pilot_number='TEST-69-002';
    raise exception 'Blank callsign unexpectedly accepted';
  exception when check_violation then null;
  end;
  begin
    update public.pilots set callsign='EXAMPLE' where pilot_number='TEST-69-002';
    raise exception 'Duplicate callsign unexpectedly accepted';
  exception when unique_violation then null;
  end;
end $$;
rollback;
