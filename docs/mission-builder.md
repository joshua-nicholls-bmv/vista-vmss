# VISTA mission planner v0.9

The complete mission catalogue is already installed according to the user. Apply only the new complete `supabase/migrations/20261006000400_catalogue_planning_api.sql` file in the dedicated VISTA project SQL Editor, then stop debugging in Visual Studio, reload changed files if prompted, and press F5. This file adds a planning function; it does not replace existing data or migrations.

## Pilot workflow

1. Open Flight planner, choose an aircraft and type departure/arrival ICAOs. Names appear beneath recognised codes. Mission families are filtered by aircraft type. Selecting a family supplies supported default airports when needed; Typhoon departure/arrival can independently use EGXC or EGQS.
2. Choose a mission type and a task location or destination. Randomise location/destination picks another enabled option. Destination missions set the selected arrival automatically.
3. Choose a compatible cargo/personnel preset if required. Logistics loads are filtered to the chosen destination. Personnel and airdrop loads can be filtered by load type; randomise load picks another compatible enabled preset.
4. Add task to sortie. Combine route-based training tasks in flight order, for example airdrop then LFA7 then RTB. Destination flights use one task. Select a task to move, remove or reroll it; reroll changes only that task. Up to 12 tasks are supported.
5. Tick either or both Lichfield corridor controls. Outbound is inserted before mission tasks east-to-west. Return is inserted afterwards west-to-east using the agreed 1 NM north offsets.
6. Review the ordered route, total payload, personnel, title and flight callsign. Save mission plan stores a draft with its selected tasks, load values and waypoint snapshot. Save is also available beneath the route preview.
7. Open My sorties to reopen or delete a planned draft. Catalogue drafts use Save mission plan so their task/load metadata is retained. Advanced/manual route editing remains in an expandable section for independent custom plans.

Atlas training flights return EGVN to EGVN. Typhoon tasks support EGXC/EGQS unless their destination determines arrival. Direct logistics/support/personnel plans can have no intermediate waypoints; airports remain their endpoints. Airdrop entry/drop/exit order, strike entry 4000 ft/350 kt and Wash FL140 are shown; unspecified levels/speeds remain at pilot discretion. Payload weight includes personnel equipment where specified; mixed loads do not add personnel mass a second time.

## Admin controls

Mission controls is visible to admin pilots. Select a mission family, option/destination, route element or load preset and click Enable/Disable selected item. Changes use the existing role-checked catalogue function. Disabled items are excluded from new selection; re-enabling a family preserves individually disabled options. Existing saved snapshots stay stored. A draft with a disabled task/load must be adjusted before it can be saved again.

## Validation and scope

Release builds pass with zero warnings/errors. Mocked WPF checks cover aircraft/family filtering, destination-specific loads, combined routes, reroll/reorder, switches, draft restoration, typed ICAOs, save requests, remembered-login and local tracking regressions. Screens were rendered and inspected. Disposable PostgreSQL checks cover the complete catalogue and new planning function, waypoint order/lineage, server-derived loads, zero-point destination routes, draft replacement, aircraft/airport/load validation, ownership and terminal-state protection.

These checks do not use the live database or pilot passwords. Apply migration 004 in VISTA before testing hosted saves. The v0.10 update adds SimBrief route launch and live sortie/debrief submission. See [live tracking instructions](live-tracking.md). Simulator avionics/payload loading and automatic OFP import remain outside the current update.
