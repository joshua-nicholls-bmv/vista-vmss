-- VISTA only. Persistent SimBrief snapshot, exercise notes and simulated acknowledgement.
-- Apply after 008. Existing airborne flights can continue; new tracking requires a signed current briefing.
begin;
create table if not exists public.sortie_briefings (
 sortie_id uuid primary key references public.sorties(id) on delete cascade,
 plan_key text not null,
 revision uuid not null default gen_random_uuid(),
 document jsonb,
 mission_notes text not null default '' check(length(mission_notes)<=20000),
 imported_at timestamptz,
 signed_at timestamptz,
 signed_by uuid references public.pilots(id),
 signed_label text,
 updated_at timestamptz not null default now()
);
alter table public.sortie_briefings enable row level security;
drop policy if exists briefing_owner_read on public.sortie_briefings;
create policy briefing_owner_read on public.sortie_briefings for select to authenticated
using(exists(select 1 from public.sorties s where s.id=sortie_id and vista_private.owns_pilot(s.pilot_id)));
revoke all on public.sortie_briefings from public,anon,authenticated;
grant select on public.sortie_briefings to authenticated;

create or replace function vista_private.briefing_plan_key(p_id uuid) returns text
language sql stable security definer set search_path='' as $$
 select md5(jsonb_build_object('aircraft',s.aircraft_id,'registration',a.serial,'type',a.aircraft_type_id,
 'departure',s.departure_base_id,'arrival',s.arrival_base_id,'alternate',s.alternate_base_id,
 'callsign',s.callsign,'title',s.title,'route',s.route_text,'snapshot',s.plan_snapshot,
 'points',coalesce((select jsonb_agg(to_jsonb(w)-array['id','sortie_id'] order by w.position)
 from public.sortie_waypoints w where w.sortie_id=s.id),'[]'::jsonb))::text)
 from public.sorties s join public.aircraft a on a.id=s.aircraft_id where s.id=p_id;
$$;
revoke all on function vista_private.briefing_plan_key(uuid) from public,anon,authenticated;

create or replace function public.load_sortie_briefing(p_sortie_id uuid) returns jsonb
language plpgsql security definer set search_path='' as $$
declare s public.sorties; b public.sortie_briefings; k text;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id);
 if not found then raise exception 'Owned mission required'; end if;
 k:=vista_private.briefing_plan_key(s.id);
 select * into b from public.sortie_briefings where sortie_id=s.id;
 return jsonb_build_object('sortie_id',s.id,'current_plan_key',k,'plan_key',b.plan_key,
 'revision',b.revision,'document',b.document,'mission_notes',coalesce(b.mission_notes,''),
 'imported_at',b.imported_at,'signed_at',b.signed_at,'signed_by',b.signed_by,'signed_label',b.signed_label);
end $$;

create or replace function public.save_sortie_briefing(p_sortie_id uuid,p_plan_key text,p_document jsonb) returns jsonb
language plpgsql security definer set search_path='' as $$
declare s public.sorties; k text; reg text; dep text; arr text; num text;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found or s.status not in ('planned','briefed') then raise exception 'Owned unflown mission required'; end if;
 k:=vista_private.briefing_plan_key(s.id);
 if p_plan_key is distinct from k then raise exception 'Plan changed. Reopen SimBrief and generate the revised mission'; end if;
 if jsonb_typeof(p_document) is distinct from 'object' or octet_length(p_document::text)>2000000 then raise exception 'Invalid briefing document'; end if;
 select serial into reg from public.aircraft where id=s.aircraft_id;
 select icao into dep from public.bases where id=s.departure_base_id;
 select icao into arr from public.bases where id=s.arrival_base_id;
 num:=substring(trim(s.callsign) from '([0-9]{1,4})$');
 if p_document->>'departure' is distinct from dep or p_document->>'arrival' is distinct from arr
 or p_document->>'registration' is distinct from reg or p_document->>'flight_number' is distinct from num
 or p_document->>'plan_key' is distinct from k
 or p_document->>'static_id' is distinct from ('VISTA_'||replace(s.id::text,'-','')||'_'||left(k,12))
 or nullif(p_document->>'route','') is null then raise exception 'SimBrief document does not match this mission revision'; end if;
 insert into public.sortie_briefings(sortie_id,plan_key,document,imported_at)
 values(s.id,k,p_document,now()) on conflict(sortie_id) do update set plan_key=k,document=p_document,
 revision=gen_random_uuid(),imported_at=now(),signed_at=null,signed_by=null,signed_label=null,updated_at=now();
 return public.load_sortie_briefing(s.id);
end $$;

create or replace function public.save_briefing_notes(p_sortie_id uuid,p_notes text,p_revision uuid) returns jsonb
language plpgsql security definer set search_path='' as $$
declare s public.sorties; b public.sortie_briefings; k text;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found or s.status not in ('planned','briefed') then raise exception 'Owned unflown mission required'; end if;
 if p_notes is null or length(p_notes)>20000 then raise exception 'Notes must be at most 20000 characters'; end if;
 select * into b from public.sortie_briefings where sortie_id=s.id for update;
 if b.revision is distinct from p_revision then raise exception 'Briefing changed. Reopen it before saving notes'; end if;
 k:=vista_private.briefing_plan_key(s.id);
 insert into public.sortie_briefings(sortie_id,plan_key,mission_notes) values(s.id,k,p_notes)
 on conflict(sortie_id) do update set mission_notes=p_notes,revision=gen_random_uuid(),
 signed_at=null,signed_by=null,signed_label=null,updated_at=now();
 return public.load_sortie_briefing(s.id);
end $$;

create or replace function public.sign_sortie_briefing(p_sortie_id uuid,p_revision uuid) returns jsonb
language plpgsql security definer set search_path='' as $$
declare s public.sorties; b public.sortie_briefings; p public.pilots;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found or s.status<>'briefed' then raise exception 'Activate your mission before signing'; end if;
 select * into b from public.sortie_briefings where sortie_id=s.id for update;
 if not found or b.document is null or b.plan_key is distinct from vista_private.briefing_plan_key(s.id)
 or b.revision is distinct from p_revision then raise exception 'Import and review the current briefing before signing'; end if;
 select * into p from public.pilots where id=s.pilot_id and auth_user_id=auth.uid() and status='active';
 if not found then raise exception 'Active pilot login required'; end if;
 update public.sortie_briefings set signed_at=coalesce(signed_at,now()),signed_by=p.id,
 signed_label=p.display_name||' · '||p.pilot_number,updated_at=now() where sortie_id=s.id;
 return public.load_sortie_briefing(s.id);
end $$;

create or replace function public.start_sortie_tracking(p_sortie_id uuid) returns uuid
language plpgsql security definer set search_path='' as $$
declare s public.sorties;
begin
 select * into s from public.sorties where id=p_sortie_id and vista_private.owns_pilot(pilot_id) for update;
 if not found then raise exception 'Choose your own active sortie'; end if;
 if s.status='airborne' then return s.id; end if;
 if s.status<>'briefed' then raise exception 'Activate a saved mission first'; end if;
 if not exists(select 1 from public.sortie_briefings b where b.sortie_id=s.id and b.document is not null
 and b.signed_at is not null and b.signed_by=s.pilot_id and b.plan_key=vista_private.briefing_plan_key(s.id))
 then raise exception 'Import and sign the current sortie briefing before tracking'; end if;
 update public.sorties set status='airborne',started_at=now() where id=s.id;return s.id;
end $$;
revoke all on function public.load_sortie_briefing(uuid),public.save_sortie_briefing(uuid,text,jsonb),
 public.save_briefing_notes(uuid,text,uuid),public.sign_sortie_briefing(uuid,uuid),public.start_sortie_tracking(uuid) from public,anon,authenticated;
grant execute on function public.load_sortie_briefing(uuid),public.save_sortie_briefing(uuid,text,jsonb),
 public.save_briefing_notes(uuid,text,uuid),public.sign_sortie_briefing(uuid,uuid),public.start_sortie_tracking(uuid) to authenticated;
commit;
