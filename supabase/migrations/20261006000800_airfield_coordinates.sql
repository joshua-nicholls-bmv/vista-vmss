-- VISTA only. Public-domain OurAirports coordinates retrieved 2026-10-06.
begin;
alter table public.bases add column if not exists latitude numeric(9,6) check(latitude between -90 and 90);
alter table public.bases add column if not exists longitude numeric(10,6) check(longitude between -180 and 180);
update public.bases b set latitude=v.latitude,longitude=v.longitude from (values
 ('EGXC', 53.092999::numeric, -0.166014::numeric),
 ('EGQS', 57.705200::numeric, -3.339170::numeric),
 ('EGVN', 51.750000::numeric, -1.583620::numeric),
 ('EGYM', 52.648395::numeric, 0.550692::numeric),
 ('EGOS', 52.798199::numeric, -2.668040::numeric),
 ('EGOV', 53.248100::numeric, -4.535340::numeric),
 ('EGEC', 55.437199::numeric, -5.686390::numeric),
 ('EGHQ', 50.440601::numeric, -4.995410::numeric),
 ('EGQL', 56.373980::numeric, -2.868862::numeric),
 ('EGXE', 54.297191::numeric, -1.538944::numeric),
 ('EGUW', 52.127451::numeric, 0.956189::numeric),
 ('EGVO', 51.234100::numeric, -0.942825::numeric),
 ('EGUB', 51.614366::numeric, -1.095991::numeric),
 ('EGXW', 53.166199::numeric, -0.523811::numeric),
 ('EGWC', 52.640524::numeric, -2.305326::numeric),
 ('EGPL', 57.481098::numeric, -7.362780::numeric),
 ('LCRA', 34.590401::numeric, 32.987900::numeric),
 ('ETAR', 49.436901::numeric, 7.600280::numeric),
 ('ENGM', 60.193901::numeric, 11.100400::numeric),
 ('LXGB', 36.151679::numeric, -5.349780::numeric),
 ('EPRZ', 50.109791::numeric, 22.024155::numeric)
) as v(icao,latitude,longitude) where b.icao=v.icao and (b.latitude is null or b.longitude is null);
commit;
