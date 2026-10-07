-- Apply once after the initial VISTA migration. No identities are assigned here.
begin;
alter table public.pilots alter column callsign drop not null;
alter table public.pilots add constraint pilots_callsign_nonblank
  check (callsign is null or length(trim(callsign)) > 0);
alter table public.pilots add column display_label text generated always as (
  display_name || ' - ' || pilot_number || ' - ' || coalesce(callsign, 'Callsign pending')
) stored;
comment on column public.pilots.pilot_number is
  'Human-readable pilot identifier. 69th Expeditionary Air Wing format pending confirmation.';
comment on column public.pilots.callsign is
  'Optional personal callsign; NULL until assigned. Separate from the sortie flight callsign.';
comment on column public.pilots.display_label is
  'Name - Identifier - Callsign; unassigned callsigns display Callsign pending.';
commit;
