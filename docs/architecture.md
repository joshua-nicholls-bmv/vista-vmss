# Data model and integration decisions

## Catalogue and identity

| Object | Purpose |
|---|---|
| bases | Primary RAF bases and secondary departure, arrival or alternate airfields; unique four-letter ICAO |
| aircraft_types | Shared type catalogue, optional SimBrief aircraft code and profile ID |
| squadrons | Code, role, home base and optional principal aircraft type |
| aircraft | Unique serial, type, squadron, home/current airfield and availability |
| pilots | UUID profile linked to Supabase Auth, separate VISTA pilot number/callsign, SimBrief ID, assignment, role and status |
| missions | Predefined mission catalogue, type, departure/arrival, optional squadron/type and revision |
| mission_waypoints | Ordered mission points with coordinates, altitude, speed and instructions |
| mission_elements | Independently reusable route segments, with type and revision |
| mission_element_waypoints | Ordered points within each reusable segment |

Use `active=false` to retire catalogue entries. Referenced aircraft, pilots and templates cannot be deleted casually. Source waypoint references use restrictive foreign keys so historical lineage is retained. Prefer new template revisions and archival rather than deleting referenced points. Latitude/longitude are decimal degrees; altitude is feet, speed knots, distance nautical miles, fuel kilograms. Timestamps use PostgreSQL `timestamptz`; use UTC at integration boundaries.

## Planning, execution and history

| Object | Purpose |
|---|---|
| sorties | Retained plan and lifecycle record; predefined mission reference/revision or custom source |
| sortie_waypoints | Flattened ordered flight route, independent coordinates/instructions, optional original mission/element waypoint IDs |
| simbrief_exports | Retry-keyed export requests, route text, provider flight ID, OFP URL, response or error |
| track_points | Sequence-keyed telemetry, approximately every five seconds, separate capture and receipt times |
| sortie_debriefs | One completion summary, validated duration/distance, outcome, notes and JSON events per sortie |
| active_sorties (view) | Briefed and airborne sorties |
| completed_sorties (view) | Completed sorties with debrief data |
| pilot_statistics (view) | Completed count, seconds/hours, distance and most recent completion |

Planned sorties are drafts. Multiple drafts are allowed. Briefing reserves one pilot and one aircraft; partial unique indexes prevent double reservations. Cancelled sorties are retained but do not contribute to statistics. Aircraft location is null when not known; the server updates it on validated arrival. Aircraft allocation is derived from live sorties, separately from maintenance status.

The transition sequence is `planned -> briefed -> airborne -> completed`; any nonterminal state may be cancelled. Completed/cancelled sortie records cannot be updated. Completion and debrief insertion occur atomically using `complete_sortie`; a deferred constraint ensures that completed sorties have a debrief and other states have none. Statistics are computed rather than independently writable counters. Debrief correction by privileged server writes will change computed statistics; an audited correction policy is future work.

Route positions must be unique, positive and consecutive when briefing. Route points and the plan become immutable after briefing. The trusted backend must validate mission/type compatibility, template ownership, source waypoint lineage and the plan snapshot contents. It must also reject unrelated waypoint IDs and inconsistent aircraft assignments before creating a plan. The relational model intentionally permits a squadron to operate more than its principal aircraft type.

For a predefined mission, copy its route and revision into the sortie. For a custom flight, select departure/arrival/alternate airfields and build a route from individual points and reusable elements. Each inserted element occurrence has an `element_instance` UUID, allowing the same element multiple times. Expand elements into `sortie_waypoints`, preserving original point references; serialize a complete human-readable plan, aircraft identification and template revision details into `plan_snapshot`. That JSON field is a historical envelope, not a substitute for ordered route rows.

SimBrief export is an explicit operation on the saved plan. Store both the generated route text and request payload before sending. Translate coordinates and selected aircraft profiles according to the provider's supported format. The current files do not implement that translation, authentication or external API call. Provider responses must be sanitized before storing because pilots can read their own export rows.

## Access and remaining implementation

Authenticated active pilots can read catalogues and their own profile, sorties, routes, track points, exports, debriefs and statistics. Active operations/admin profiles can read all pilot operations data. Suspended/inactive profiles and anonymous users cannot read this operational data. Pilot roles cannot be changed by browser or ACARS writes. Security-definer helpers live in an unexposed private schema with fixed empty search paths; views use invoker security to retain underlying RLS.

Draft create/update/cancel now uses the authenticated functions in `20261006000300_planner_api.sql` as its trusted database boundary. Those functions derive identity from `auth.uid()` and validate ownership/status without giving clients direct table writes. Other mutations still require a trusted VISTA backend. RLS does not constrain the service role, so that backend must enforce caller identity and ownership. No service-role key is shared with clients. Profiles are administratively provisioned; there is no automatic Auth trigger.

The WPF starter now provides Supabase Auth login/token refresh and pilot-owned planning transactions. Before ACARS release, implement SimBrief conversion/handoff, reliable telemetry ingestion, completion validation, operational audit logging and retention. Realtime subscriptions, partitioning, spatial indexes and materialized statistics are deferred until actual traffic requires them. See `docs/fleet-setup.md` for the user-configured virtual fleet.
