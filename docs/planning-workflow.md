# ICAO-led mission planning

The planning screen follows the user's requested airport → mission → waypoints → flight plan flow. The supplied shared discussion and pasted context describe missions and reusable elements as VISTA-owned catalogues, with routes eventually passed to SimBrief.

1. Enter departure and arrival ICAOs. Input is normalised to uppercase. VISTA recognises active airfields in its `bases` catalogue and displays their names. Initial primary fields are EGXC (Coningsby), EGQS (Lossiemouth) and EGVN (Brize Norton). Secondary airfields work when present and active in that same catalogue.
2. Choose an aircraft. Missions are filtered locally as inputs change: exact departure/arrival pair, active mission, and any aircraft-type/squadron restrictions. Arrival equal to departure is supported for return-to-base training routes. The app does not reverse routes or infer geographic suitability from nearby coordinates.
3. Choose a matching mission to fetch and preview its ordered waypoint list in the background. Selection previews only; it does not overwrite current route points. Slow responses from previously selected missions are discarded.
4. Choose **Use selected mission** to load its route and title. Replacing existing points requires confirmation. The loaded route remains a predefined mission; its points are read-only. Changing airports or aircraft so it no longer matches clears that loaded route and returns to custom planning.
5. Choose **Plan custom route** to keep the current points and make them editable, or build your own route. Reusable mission elements can be appended. Custom routes remain intact when airport inputs change.
6. Enter title, callsign and optional alternate ICAO, review route points and save the draft. Unknown departure/arrival codes or a non-empty unknown alternate prevent saving. Draft reopening restores airport inputs and matching context. Mission saves retain the existing server-side route snapshot rules.

No new schema migration is required: the workflow uses existing `bases`, `missions`, `mission_waypoints`, `mission_elements` and planner APIs. Missions without waypoints show a clear message. This update does not manufacture mission routes, add airfields automatically or write mission seed data remotely.

Airport recognition currently covers the VISTA catalogue, not every worldwide airport. Operations needs to add additional airfields and approved mission routes to the dedicated VISTA database. The pilot client retains its existing restricted permissions.

SimBrief dispatch is not part of this update. Saved airport IDs, callsign, aircraft, route text and ordered coordinate points provide the planning inputs for that next integration; military waypoint identifiers still need conversion/validation for SimBrief.

## Verification

Mocked WPF checks exercise normalisation, recognised/unknown fields, exact airport pairs, aircraft type and squadron restrictions, route ordering, background response races, explicit route loading, mission-to-custom conversion, changing endpoints, alternate validation and custom/predefined saves. Existing login, tracking and desktop checks also run. No hosted migration or real pilot password is used by those tests.
