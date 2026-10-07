-- VISTA ONLY: allow owned cancelled, unflown missions to be deleted. Apply after 006.
begin;
create or replace function vista_private.guard_route() returns trigger language plpgsql set search_path = '' as $$
declare s_id uuid;
begin
  if TG_OP = 'UPDATE' and new.sortie_id <> old.sortie_id then raise exception 'Cannot move route points'; end if;
  if TG_OP = 'DELETE' then s_id := old.sortie_id; else s_id := new.sortie_id; end if;
  perform 1 from public.sorties where id = s_id and (status = 'planned' or (TG_OP = 'DELETE' and status = 'cancelled' and started_at is null
    and not exists(select 1 from public.track_points where sortie_id=s_id)
    and not exists(select 1 from public.sortie_debriefs where sortie_id=s_id))) for update;
  if not found then raise exception 'Route is editable only while planned; unflown cancelled routes may be deleted'; end if;
  if TG_OP = 'DELETE' then return old; end if;
  return new;
end $$;

create or replace function public.delete_planned_sortie(p_sortie_id uuid)
returns uuid language plpgsql security definer set search_path='' as $$
declare owner_id uuid; draft public.sorties;
begin
 select id into owner_id from public.pilots where auth_user_id=auth.uid() and status='active' for update;
 if owner_id is null then raise exception 'Active VISTA pilot login required'; end if;
 select * into draft from public.sorties where id=p_sortie_id for update;
 if not found then return p_sortie_id; end if;
 if draft.pilot_id<>owner_id then raise exception 'Only your own draft can be deleted'; end if;
 if draft.status not in ('planned','cancelled') then raise exception 'Only planned or cancelled missions can be deleted'; end if;
 if draft.started_at is not null or exists(select 1 from public.track_points where sortie_id=draft.id) or exists(select 1 from public.sortie_debriefs where sortie_id=draft.id) then raise exception 'A mission with flight history cannot be deleted'; end if;
 -- Delete route children while their eligible parent exists for guard_route.
 delete from public.sortie_waypoints where sortie_id=draft.id;
 delete from public.simbrief_exports where sortie_id=draft.id;
 delete from public.sorties where id=draft.id;
 return p_sortie_id;
end $$;
revoke all on function public.delete_planned_sortie(uuid) from public,anon,authenticated;
grant execute on function public.delete_planned_sortie(uuid) to authenticated;
commit;
