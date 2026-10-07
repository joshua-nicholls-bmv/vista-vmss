-- VISTA project dlqfpbqenqumdlogfrxo only. Run this entire file in SQL Editor.
-- 20 Atlas tail numbers from PDF page 6; 42 Typhoons from the user's roster.
-- One atomic statement with no temporary tables. Safe to repeat.
-- Newly created aircraft are virtually available at their home base.
-- Existing availability, current location, SimBrief settings and active flags are preserved.
-- A conflicting existing home/type/unit assignment aborts instead of silently overwriting it.
do $$
declare
  type_roster constant jsonb := '[
  {
    "code": "ATLAS",
    "name": "Atlas"
  },
  {
    "code": "TYPHOON-FGR4",
    "name": "Typhoon FGR4"
  },
  {
    "code": "TYPHOON-T3",
    "name": "Typhoon T3"
  }
]'::jsonb;
  unit_roster constant jsonb := '[
  {
    "code": "AMF",
    "name": "Air Mobility Force",
    "base_icao": "EGVN",
    "aircraft_type_code": "ATLAS",
    "role": "air_mobility"
  },
  {
    "code": "3-F",
    "name": "No. 3 (Fighter) Squadron",
    "base_icao": "EGXC",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "fighter"
  },
  {
    "code": "XI-F",
    "name": "No. XI (Fighter) Squadron",
    "base_icao": "EGXC",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "fighter"
  },
  {
    "code": "12",
    "name": "No. 12 Squadron",
    "base_icao": "EGXC",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "fighter"
  },
  {
    "code": "29",
    "name": "No. 29 Squadron",
    "base_icao": "EGXC",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "training"
  },
  {
    "code": "41-TES",
    "name": "No. XLI Test and Evaluation Squadron (41 TES)",
    "base_icao": "EGXC",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "test_and_evaluation"
  },
  {
    "code": "1-F",
    "name": "No. 1 (Fighter) Squadron",
    "base_icao": "EGQS",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "fighter"
  },
  {
    "code": "II-AC",
    "name": "No. II (Army Cooperation) Squadron",
    "base_icao": "EGQS",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "army_cooperation"
  },
  {
    "code": "6",
    "name": "No. 6 Squadron",
    "base_icao": "EGQS",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "fighter"
  },
  {
    "code": "IX-B",
    "name": "No. IX (Bomber) Squadron",
    "base_icao": "EGQS",
    "aircraft_type_code": "TYPHOON-FGR4",
    "role": "aggressor_combat"
  }
]'::jsonb;
  aircraft_roster constant jsonb := '[
  {
    "serial": "ZM400",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "Sustainment Fleet",
    "tail_code": null
  },
  {
    "serial": "ZM401",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "Sustainment Fleet",
    "tail_code": null
  },
  {
    "serial": "ZM402",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM403",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "Sustainment Fleet",
    "tail_code": null
  },
  {
    "serial": "ZM404",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM405",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "Sustainment Fleet",
    "tail_code": null
  },
  {
    "serial": "ZM406",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "Sustainment Fleet",
    "tail_code": null
  },
  {
    "serial": "ZM407",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM408",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "Sustainment Fleet",
    "tail_code": null
  },
  {
    "serial": "ZM409",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "Sustainment Fleet",
    "tail_code": null
  },
  {
    "serial": "ZM410",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "Sustainment Fleet",
    "tail_code": null
  },
  {
    "serial": "ZM411",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM412",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM413",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM414",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM415",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM416",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM417",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM418",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZM419",
    "aircraft_type_code": "ATLAS",
    "unit_code": "AMF",
    "base_icao": "EGVN",
    "source": "atlas_pdf",
    "source_unit": "24/70 Sqn Pool",
    "tail_code": null
  },
  {
    "serial": "ZK345",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "3-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 3 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK311",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "3-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 3 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK354",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "3-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 3 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK364",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "3-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 3 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZJ921",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "3-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 3 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK301",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "XI-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XI (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK308",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "XI-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XI (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK318",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "XI-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XI (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK321",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "XI-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XI (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK332",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "XI-F",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XI (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK363",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "12",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 12 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK335",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "12",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 12 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK361",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "12",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 12 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK368",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "12",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 12 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK371",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "12",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 12 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZJ913",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "29",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 29 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK343",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "29",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 29 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK353",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "29",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 29 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK381",
    "aircraft_type_code": "TYPHOON-T3",
    "unit_code": "29",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 29 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZJ806",
    "aircraft_type_code": "TYPHOON-T3",
    "unit_code": "29",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. 29 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK315",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "41-TES",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XLI Test and Evaluation Squadron (41 TES)",
    "tail_code": null
  },
  {
    "serial": "ZK379",
    "aircraft_type_code": "TYPHOON-T3",
    "unit_code": "41-TES",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XLI Test and Evaluation Squadron (41 TES)",
    "tail_code": null
  },
  {
    "serial": "ZK303",
    "aircraft_type_code": "TYPHOON-T3",
    "unit_code": "41-TES",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XLI Test and Evaluation Squadron (41 TES)",
    "tail_code": null
  },
  {
    "serial": "ZK360",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "41-TES",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XLI Test and Evaluation Squadron (41 TES)",
    "tail_code": null
  },
  {
    "serial": "ZK376",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "41-TES",
    "base_icao": "EGXC",
    "source": "user_roster",
    "source_unit": "No. XLI Test and Evaluation Squadron (41 TES)",
    "tail_code": null
  },
  {
    "serial": "ZK341",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "1-F",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 1 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK348",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "1-F",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 1 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK314",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "1-F",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 1 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK372",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "1-F",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 1 (Fighter) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK316",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "II-AC",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. II (Army Cooperation) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK320",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "II-AC",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. II (Army Cooperation) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK329",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "II-AC",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. II (Army Cooperation) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK333",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "II-AC",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. II (Army Cooperation) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK366",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "6",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 6 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK346",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "6",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 6 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK306",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "6",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 6 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK313",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "6",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 6 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZK324",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "6",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. 6 Squadron",
    "tail_code": null
  },
  {
    "serial": "ZJ924",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "IX-B",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. IX (Bomber) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZJ935",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "IX-B",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. IX (Bomber) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZJ914",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "IX-B",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. IX (Bomber) Squadron",
    "tail_code": null
  },
  {
    "serial": "ZJ947",
    "aircraft_type_code": "TYPHOON-FGR4",
    "unit_code": "IX-B",
    "base_icao": "EGQS",
    "source": "user_roster",
    "source_unit": "No. IX (Bomber) Squadron",
    "tail_code": null
  }
]'::jsonb;
  r record;
  b_id uuid;
  t_id uuid;
  u_id uuid;
begin
  if (select count(*) from public.bases where icao in ('EGVN','EGXC','EGQS')) <> 3 then
    raise exception 'The three VISTA primary bases must exist before fleet setup';
  end if;
  if (select count(*) <> count(distinct serial) from jsonb_to_recordset(aircraft_roster) as a(serial text)) then
    raise exception 'Fleet roster contains duplicate aircraft serials';
  end if;

  for r in select * from jsonb_to_recordset(type_roster) as a(code text,name text) loop
    if exists(select 1 from public.aircraft_types t where t.code=r.code and t.name<>r.name) then
      raise exception 'Aircraft type conflicts with the configured name: %', r.code;
    end if;
    insert into public.aircraft_types(code,name) values(r.code,r.name) on conflict(code) do nothing;
  end loop;

  for r in select * from jsonb_to_recordset(unit_roster) as a(
    code text,name text,base_icao text,aircraft_type_code text,role text
  ) loop
    select id into strict b_id from public.bases where icao=r.base_icao;
    select id into strict t_id from public.aircraft_types where code=r.aircraft_type_code;
    if exists(select 1 from public.squadrons s where s.code=r.code and (
      s.name is distinct from r.name or s.base_id is distinct from b_id or
      s.aircraft_type_id is distinct from t_id or s.role is distinct from r.role
    )) then
      raise exception 'Existing squadron/grouping conflicts with roster: %', r.code;
    end if;
    insert into public.squadrons(code,name,base_id,aircraft_type_id,role)
    values(r.code,r.name,b_id,t_id,r.role) on conflict(code) do nothing;
  end loop;

  for r in select * from jsonb_to_recordset(aircraft_roster) as a(
    serial text,aircraft_type_code text,unit_code text,base_icao text
  ) loop
    select id into strict b_id from public.bases where icao=r.base_icao;
    select id into strict t_id from public.aircraft_types where code=r.aircraft_type_code;
    select id into strict u_id from public.squadrons where code=r.unit_code;
    if exists(select 1 from public.aircraft a where a.serial=r.serial and (
      a.aircraft_type_id is distinct from t_id or a.squadron_id is distinct from u_id
      or a.home_base_id is distinct from b_id
    )) then
      raise exception 'Existing aircraft assignment conflicts with roster: %', r.serial;
    end if;
    insert into public.aircraft(serial,aircraft_type_id,squadron_id,home_base_id,current_base_id,status)
    values(r.serial,t_id,u_id,b_id,b_id,'available') on conflict(serial) do nothing;
  end loop;
end $$;

-- Verification: 20 Brize Norton, 25 Coningsby, 17 Lossiemouth aircraft.
select b.name as base,s.name as squadron_or_grouping,t.name as aircraft_type,count(*) as aircraft_count
from public.aircraft a join public.bases b on b.id=a.home_base_id
join public.squadrons s on s.id=a.squadron_id join public.aircraft_types t on t.id=a.aircraft_type_id
where s.code in ('AMF','3-F','XI-F','12','29','41-TES','1-F','II-AC','6','IX-B')
group by b.name,s.name,t.name order by b.name,s.name,t.name;
