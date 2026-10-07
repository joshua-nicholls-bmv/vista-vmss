-- VISTA ONLY: permanent deletion of an owned, unflown planned draft.
begin;
create or replace function public.delete_planned_sortie(p_sortie_id uuid)
returns uuid language plpgsql security definer set search_path='' as $$
declare owner_id uuid; draft public.sorties;
begin
 select id into owner_id from public.pilots where auth_user_id=auth.uid() and status='active' for update;
 if owner_id is null then raise exception 'Active VISTA pilot login required'; end if;
 select * into draft from public.sorties where id=p_sortie_id for update;
 if not found then return p_sortie_id; end if;
 if draft.pilot_id<>owner_id then raise exception 'Only your own draft can be deleted'; end if;
 if draft.status<>'planned' then raise exception 'Only planned drafts can be deleted'; end if;
 if exists(select 1 from public.track_points where sortie_id=draft.id) or exists(select 1 from public.sortie_debriefs where sortie_id=draft.id) then raise exception 'A draft with flight history cannot be deleted'; end if;
 -- Delete route children while their planned parent still exists for guard_route.
 delete from public.sortie_waypoints where sortie_id=draft.id;
 delete from public.simbrief_exports where sortie_id=draft.id;
 delete from public.sorties where id=draft.id;
 return p_sortie_id;
end $$;
revoke all on function public.delete_planned_sortie(uuid) from public,anon,authenticated;
grant execute on function public.delete_planned_sortie(uuid) to authenticated;
commit;
