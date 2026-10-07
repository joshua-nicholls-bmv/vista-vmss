# Complete VISTA mission catalogue setup

Run the entire `supabase/setup/complete_mission_catalogue.sql` file in the SQL Editor for **VISTA project dlqfpbqenqumdlogfrxo**. The initial schema, pilot-display migration and initial fleet setup must already be installed. This is an additive setup; do not replace or rerun the initial schema. The workspace has not applied this file remotely.

The file runs in one transaction. A private installation marker prevents reseeding on repeat execution, preserving UUIDs, subsequent route edits and enable/disable choices. Existing airfield names and active flags are preserved. Newly created catalogue entries start enabled. SQL errors roll back the transaction; do not continue with fragments from the file.

## Approved content

- 12 mission families: six Typhoon and six Air Mobility Force types.
- 41 task/destination options: Wash North, five strikes, two low-level areas for each fleet, three AAR areas, eight dispersal destinations, ten logistics destinations, two fighter-support destinations, five personnel destinations and three airdrop locations.
- 16 reusable elements, including distinct Lichfield outbound and return routes.
- 142 airport/aircraft-specific mission rows. Typhoon tasks cover all four Coningsby/Lossiemouth pairs for both FGR4 and T3. Dispersal departs from either base and finishes at its selected destination. Atlas training returns to Brize; logistics/support/personnel flights finish at the selected destination.
- 30 payload presets: the 20 approved cargo manifests, four personnel loads (20/40/60/80), three paradrop personnel loads (20/40/60), and three airdrop cargo loads (2000/4000/6000 kg).

Cargo presets have eligible destinations and mission families. The radar engineering detachment's 4800 kg already includes its six engineers; do not add another personnel allowance to that total. Other personnel presets use the agreed 100 kg/person planning assumption. Weights describe virtual simulator loads.

## Coordinate and unit rules

User-supplied locations are reference points, not area boundaries. Wash North enters at FL140; the supplied FL050-FL245 limits are stored separately in metadata. Its waypoint also carries the FL140 instruction so the existing desktop retains the pressure-reference meaning alongside the 14000-ft numeric field.

Strike entry points are generated 8 NM due north, at 4000 ft and 350 kt, before the user-supplied target. Target altitude/speed remain NULL. Strike 4 is retained at the supplied Scottish location despite the earlier England-only description.

Airdrop elements contain entry, drop and exit in that order. Outer points are 4 NM from the drop. Dalton/Netheravon enter from north and exit south. Pembrey enters from southeast (135 degrees true) and exits northwest (315 degrees true). Unspecified altitude and speed are NULL.

Lichfield outbound follows the four supplied east-to-west points. Return reverses that order and offsets each point 1 NM due north. Optional outbound belongs before tasks; optional return belongs after tasks. Geographic offsets use the WGS84 ellipsoid and nautical miles (1852 metres), rounded to six decimals.

Airports remain separate plan endpoints. No airport coordinate or en-route transit waypoint was invented. Oslo is stored as Gardermoen (ENGM), as previously proposed. Corrected codes are Marham EGYM, Lossiemouth EGQS, Benson EGUB and Wattisham EGUW. Wattisham's correction is confirmed in the [UK Military AIP](https://www.aidu.mod.uk/aip/pdf/ad/EGUW-Wattisham-Textual.pdf).

## Application integration status

The v0.9 desktop reads these catalogues and supplies random location/load selection, cargo/personnel pickers, combined training tasks, Lichfield outbound/return checkboxes, previews and operations switches. Apply the new migration `20261006000400_catalogue_planning_api.sql` to save composed and direct-airport plans with server-derived load/route snapshots. See [mission planner instructions](mission-builder.md).

The SQL supplies role-checked `set_mission_catalogue_enabled(kind, code, enabled)` for the management switches. Kinds are `family`, `option`, `payload` and `element`; only active operations/admin profiles may use it. Direct client writes remain denied. Family/option/element switches synchronise the associated mission active flags; payload choices retain their own flags. Individual disabled options remain disabled when their family is re-enabled. Only approved catalogue data is exposed to active pilots; anonymous reads are denied.

## Validation

The complete file executed in disposable local PostgreSQL with Supabase roles/Auth emulated. Checks passed for prerequisite rejection and rollback, expected counts, ordered strike/airdrop profiles, separate flight-level metadata, pilot catalogue reads, denied direct writes/anonymous access, role-checked management calls, disabled-option propagation and repeat preservation. Historical sorties, fleet assignments and British Midland data were not modified.

The accompanying `mission-catalogue.json` is the complete readable data manifest used to assemble the SQL.

The v0.10 update adds live tracking and SimBrief launch. Apply migration 005 after 004; it restricts catalogue switches to admins only. See [live workflow](live-tracking.md).
