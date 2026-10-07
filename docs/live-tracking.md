# VISTA v0.10: live ACARS and saved missions

Apply the complete new `supabase/migrations/20261006000500_live_sortie_tracking.sql` in VISTA's Supabase SQL Editor after migration 004. The Visual Studio solution has been updated directly. Stop debugging, reload changed files if prompted, then press F5.

## Five tabs

1. ACARS: simulator connection, active mission, live position/altitude/groundspeed/vertical speed/fuel, flight phase, block time, OUT/OFF/ON/IN, planned route and recorded track, events, upload state and debrief submission.
2. Mission Planner: the approved mission catalogue, payloads, combined tasks, Lichfield controls and manual route editor.
3. Saved Missions: select a saved plan, activate it, open its route in SimBrief, edit/delete drafts or fly a completed/cancelled mission again. Fly again creates a new sortie and copies the saved route; it preserves the previous flight record.
4. Fleet: the existing fleet catalogue and filters.
5. Mission Control: admins only, enforced in the application and the new server function. Operations-role pilots can still fly their own sorties but cannot change catalogue switches.

## Flight workflow

Save a plan in Mission Planner. Select it under Saved Missions and use Activate and open SimBrief. Activation reserves the pilot and aircraft and freezes the saved plan. Direct destination missions can activate without invented intermediate waypoints. One active mission per pilot/aircraft is enforced on the server.

SimBrief opens with saved departure/arrival, registration, callsign and ordered route coordinates. Atlas flights automatically select A400 (A400m-Atlas); both Typhoon variants select the configured Eurofighter saved airframe Internal ID. Airline ICAO is always RRR. The numeric suffix of the ACARS flight callsign becomes the flight number: DREAD 408 → RRR / 408; RIDID 321 → RRR / 321. Joined callsigns such as DREAD408 are also supported, and leading zeros are preserved. A callsign must end in a one-to-four-digit number before dispatch. Routine aircraft/profile overrides have been removed so they cannot replace these agreed mappings.

Waypoint coordinates use degree/minute/second route tokens. Prescribed altitude/speed instructions are also included in remarks; VISTA retains exact mission values. Review and generate the briefing in SimBrief. This update opens its planning form; it does not claim automatic generation or load simulator avionics.

Load the correct aircraft and departure location in MSFS, connect on ACARS, confirm the aircraft/mission match, and Start flight tracking while stationary on the ground with the parking brake set. Start before taxi. Simulator telemetry arrives each second using the British Midland-derived SimConnect transport and packed data definition. British Midland-derived OUT/OFF/ON/IN rules detect stand departure, takeoff, landing and confirmed parking. Tracking stores five-second points locally and uploads queued batches. Landing rate uses the last airborne vertical-speed sample, consistent with the extracted tracking rules.

Land, taxi in, set the parking brake and wait for IN. Enter debrief notes and Submit completed sortie. Queued points are uploaded first. The completion stores flight duration, track distance, landing rate, fuel use and events and updates the pilot statistics. The fleet current location is set to the plan's arrival; pilots must verify they landed at that destination. Cancellation releases the active reservation and retains the recorded database history.

## Recovery

Per-pilot recovery is saved under `%LOCALAPPDATA%/VISTA/tracking/` after track points/events and upload acknowledgements. Points and a tracker checkpoint remain available through a network interruption, simulator disconnect or same-pilot restart on the same Windows computer. Reconnect MSFS after reopening. Retry uploads can be used explicitly. Retries reuse the same sortie/sequence; conflicting point retries are rejected by the server. A pending debrief is frozen locally and can be submitted again after a lost response without making a second completed sortie.

Recovery files contain tracking data, not credentials. They are deleted after acknowledged completion/cancellation. The displayed track retains the latest 1,800 points while the recovery/upload queue retains the full sortie. Disconnects and telemetry gaps do not invent a takeoff or landing, and distance ignores gaps greater than 15 seconds. Recovery requires the original computer's valid local checkpoint; if it is absent, the application explains that the mission must be resumed on the original computer or cancelled. Unreadable recovery files are preserved rather than preventing sign-in.

The route display is a geographic grid showing planned task points and recorded positions, not a chart or a terrain basemap. Airport coordinate points are not fabricated. Initial airport-to-task transit uses the pilot's plan in the simulator.

## Verification

Release builds pass with zero warnings/errors. Mocked WPF checks cover activation/dispatch parameters, DMS coordinate carry/signs, custom airframe IDs, five-second queues, offline retention, lost upload/completion acknowledgements, same-pilot checkpoint recovery, landing/parking confirmation, completion and fly-again/cancellation. Existing planning, protected login and tracking regression checks pass. Screens were rendered and inspected.

Disposable PostgreSQL checks cover direct-route activation, pilot/aircraft reservation, authenticated ownership, ordered/retry-safe track uploads, conflicting and foreign write rejection, single completion/debrief, historical-preserving cloning, cancellation and admin-only controls. The assistant did not apply hosted SQL or start a real flight. A live MSFS/Supabase/SimBrief flight still needs to be exercised by the pilot after migration 005.

SimBrief reference: [official dispatch parameters and custom airframes](https://beta.docs.navigraph.com/simbrief/using-the-api), [official custom coordinate syntax](https://forum.navigraph.com/t/custom-waypoints-with-degrees-minutes-seconds/13425). British Midland source was read only; its credentials, database and source files were not changed.


## Delete drafts (v0.13)

Apply the complete migration `20261006000600_delete_planned_sortie.sql`, then restart the app. Saved Missions now has Delete selected draft instead of draft cancellation. Select your planned draft and confirm deletion; it removes that draft, its route points and any draft export records permanently. The open editor is cleared if it contained the deleted draft. Active, completed and cancelled historical sorties cannot be deleted by this option. Active mission cancellation remains available separately. Deletion is authenticated, owner-checked and safe to retry after a lost response. Local SQL and mocked desktop checks passed; hosted SQL has not been applied by the assistant.


## Delete cancelled, unflown missions (v0.14)

Apply the complete migration `20261006000700_delete_cancelled_sortie.sql` and restart the app. Delete selected mission is enabled for planned and cancelled records where tracking has never started. The server additionally refuses deletion if any track point or debrief exists. Cancelled mission route points are removed safely while remaining frozen against edits. Confirmation, ownership checks and retry safety remain in place. Local database and mocked desktop checks passed.


Typhoon SimBrief dispatch (v0.15): `simbrief_typhoon_airframe_id` is configured as `404036_1780602917520`. All Typhoon registrations reuse this performance profile; the selected fleet serial is supplied separately through `reg`. EUFI is the aircraft ICAO identifier, but is absent from the public SimBrief aircraft profile list retrieved on 6 October 2026. The saved Internal ID is required instead of EUFI. Missing profile configuration is reported before activating a mission. No database migration is needed. Mocked desktop checks verify the profile ID and selected registration; live SimBrief acceptance remains to be confirmed in the user account.


Atlas SimBrief dispatch (v0.16): all Atlas missions use saved airframe Internal ID `404036_1780234377464`, configured in `simbrief_atlas_airframe_id`. The selected fleet registration is passed separately as `reg`. Both Atlas and Typhoon profile settings are loaded at startup. Missing/invalid profile IDs block dispatch before activation. Build and mocked desktop checks passed; confirm the result in SimBrief after restarting. No SQL migration is required.


## SimBrief briefing import (v0.17)

Activate a mission, open SimBrief from ACARS and generate the briefing. Enter the numeric SimBrief Pilot ID (Account Settings) in the ACARS briefing panel, then select Import briefing. An existing pilot simbrief_user_id is used when the field is empty. The typed ID is session-only and cleared on sign-out. No API key or SQL migration is required.

The request uses the pilot ID and VISTA mission static_id. Imports must match departure, arrival, registration and RRR flight number; a returned static_id is also validated when supplied. ACARS shows the generated route, ramp/trip fuel in kilograms, initial cruise altitude and estimated flight duration. Pounds are converted; unknown fuel units and incomplete routes are rejected. Errors preserve a prior valid briefing with a visible refresh failure message. Changing/clearing the active mission or signing out clears the briefing. The imported route is read-only and does not replace frozen mission waypoints, constraints or tracking history. Briefings are session-only; re-import after restarting.

Build, mocked import/mismatch/unit conversion/offline/lifecycle tests and WPF visual checks passed. Live retrieval in the user's SimBrief account remains to be verified. API documentation: https://beta.docs.navigraph.com/simbrief/fetching-ofp-data and https://beta.docs.navigraph.com/simbrief/using-the-api .


## Mission progress and pilot settings (v0.18)

ACARS shows the next saved mission point, distance NM, true bearing, current-groundspeed ETA, ordered progress count and prescribed altitude/speed/instructions. Missing limits remain pilot discretion. Automatic advancement requires active tracking, fresh airborne telemetry and proximity within 1 NM of the next point. Manual previous/pass/skip controls support diversions; passing proximity does not prove compliance with mission constraints. Direct missions report proceed to arrival. This version does not use generated SimBrief transit fixes as mission task points.

The new sixth Pilot Settings tab shows profile identity, callsign, role/status and existing completed-sortie/hour statistics. Save the numeric SimBrief Pilot ID here to remember it on this computer, isolated by pilot UUID. The ACARS ID field uses the same value. Profile identity is read-only. Local settings and progress checkpoints live under LOCALAPPDATA/VISTA/settings. Progress is isolated by pilot and mission UUID, with ordered route identity validation before restoring. No database migration is needed. Route Watch still uses its geographic grid.

Build and mocked settings persistence/validation, progress controls, distance/bearing and existing lifecycle checks passed; settings page visually inspected. Live in-simulator waypoint passage is ready for user verification.


## Branding, simulator status, engine events and geographic map (v0.19)

The generated VISTA logo replaces the 69EAW crest in the login panel and header. A persistent header badge plus ACARS banner show SIMULATOR CONNECTED in green, connecting/waiting in amber or SIMULATOR DISCONNECTED in red. Connected means telemetry received within ten seconds. The banner shows engines running/stopped or unavailable.

Combustion transitions now emit ENGINE START and ENGINE SHUTDOWN, including during an active mission's preflight. Preflight events survive starting live tracking and join recovery/debrief events. First observation with engines already running is labelled accurately; reconnects/telemetry gaps do not invent starts. These are aggregate engine events: first engine running and all engines stopped. Start tracking before engine start for a complete recorded flight; observed preflight events are session-local until tracking is started.

Route Watch now displays OpenStreetMap raster tiles aligned through Web Mercator projection, ordered waypoint labels, the planned route, recorded track and current position. Scroll to zoom, drag to pan, Fit route or double-click to restore automatic fitting. Track segments with telemetry gaps over 15 seconds are not connected. Only visible tiles are requested, with four concurrent downloads, a dedicated User-Agent, visible attribution and a minimum seven-day disk cache under LOCALAPPDATA/VISTA/map-tiles. No prefetch/bulk-download feature. HTTPS tile template is configurable with map_tile_url_template in appsettings.json; default https://tile.openstreetmap.org/{z}/{x}/{y}.png. Missing tiles display an explicit internet/loading status; overlays remain available. The background is a geographic map, not an aviation chart.

Build, mocked engine preflight/start/shutdown/gap checks, connection labels, Mercator projection and existing desktop/tracking/login checks passed. One real UK tile downloaded/decoded and a repeated request was served from disk cache. Header/banner visuals inspected. In-simulator add-on combustion reporting remains to be checked on the user's aircraft. No SQL migration required.

Sources: https://operations.osmfoundation.org/policies/tiles/ and https://docs.flightsimulator.com/html/Programming_Tools/SimVars/Aircraft_SimVars/Aircraft_Engine_Variables.htm


## Full mission map, airport suggestions and mission list (v0.20)

Apply migration `20261006000800_airfield_coordinates.sql` in VISTA Supabase, then restart. It adds nullable latitude/longitude to bases and seeds missing pairs for all 21 current airfields from the public-domain OurAirports download, retrieved 6 October 2026. Existing complete coordinate pairs are preserved. Source and exact seed coordinates are in docs/airfield-coordinates.json. Client behaviour reports missing coordinates explicitly rather than inventing locations.

Airport controls accept partial ICAO or names (for example conin), showing up to eight active-airfield suggestions. Click a result or use Up/Down and Enter to choose its ICAO; Escape dismisses. Selection uses the existing recognised-airfield planning validation. Mission name is prominent and editable; task randomisation retains a custom title. Saved Missions show the aircraft's current squadron/force and only ACTIVE (planned/briefed/airborne) or Archived (completed/cancelled). Underlying lifecycle states and history remain intact.

Map route lines connect departure, ordered task points and arrival. Same-base flights show one Departure/RTB marker and the return leg. Distinct airfields have separate labelled markers. Passed task points are green, next is gold, other points white; actual aircraft is a heading arrow. Follow aircraft tracks its location, Fit full mission restores automatic overview, scrolling/dragging disables follow for manual inspection. Enlarge map opens a second window with the same live bindings, controls and legend. Header/ACARS connection indication is now a compact coloured dot and label.

Build, local disposable SQL migration repeatability/count checks, airport search and binding selection, custom-name preservation, display status/squadron, map fit/follow and existing regressions passed. Planner and full-route overlays visually inspected with network disabled in desktop tests. Existing map tile loading was verified in v0.19; live mission/airfield overlays are ready for user verification after migration 008.


## Spoken strike and airdrop entry instructions (v0.21)

Voice is enabled by default and announces the next eligible strike entry or airdrop run-in point within 10 NM while live tracking airborne. All other mission types, target/drop/exit points, stale or ground telemetry stay silent. Eligible points are matched to their original catalogue element through saved source waypoint identity: S1ENTRY..S5ENTRY and DALTON-IN, NETHERAVON-IN, PEMBREY-IN. Merely naming a custom point similarly does not enable announcements. The callout states prescribed altitude/speed and reads saved instructions; missing restrictions remain pilot discretion. Numeric instruction phrases use British-style wording.

ACARS Mission Progress has Mute/Unmute and a visual entry instruction. Pilot Settings has enable, volume, Test voice and Save voice settings. Mute cancels pending playback and saves the preference for that pilot on this computer. Visual entry alerts remain while muted, and unmuting does not replay a previously alerted entry. Per-point alert IDs are stored with the existing mission-progress checkpoint, preventing repeats after reconnect/restart; each new sortie has fresh IDs. Mission end/change, sign-out and closing cancel speech. No SQL migration is needed.

Speech uses local Windows SAPI asynchronously as plain text (never interpreting mission text as speech XML), prefers an installed British male voice, then another British voice, then available Windows English. Speaking pace is measured. This PC has Microsoft Hazel Desktop - English (Great Britain); zero-volume integration verified that VISTA selects it. Delivery style is based on clear phrases and pace; Windows supplies the voice timbre. Missing voice/output failures remain visible while tracking continues.

Build and mocked entry classification, 10 NM threshold, ground/stale exclusion, once-only alerts, discretionary restrictions, muted visual alerts, cancellation, checkpoint writes, test voice, saved mute and existing flight/login/planning regressions passed. Local Windows speech initialised successfully at zero volume. Use Test voice for audible checking, then verify on a strike or airdrop flight. The mission planner also now has point 06 Sortie brief below 05 and explicit multi-task guidance in point 02.

Speech flags reference: https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee125223(v=vs.85)


## v0.22 — RAF Operations redesign

Sky blue #87CEEB, deep navy backgrounds, gradient cards, softer corners and consistent action styles. Compact header and icon navigation; ACARS presents the active sortie, flight clocks, larger map and live aircraft panel ahead of the expandable SimBrief briefing. Mission progress and visual entry instructions remain. Fleet, mission library, planner, catalogue controls, pilot settings and login share the refreshed theme. Effra remains preferred with Segoe UI fallback. Destructive actions have a separate red treatment.

Windows text-to-speech and all voice controls have been removed. Existing local settings files remain readable: old voice fields are ignored. Visual strike/airdrop entry alerts at 10 NM and waypoint progression are retained. No database migration is required.

Validation: Release build with zero warnings/errors; mocked desktop checks cover planning, SimBrief, tracking, recovery, login, progress and once-only visual entry alerts. WPF renders inspected at 1440×920 and 1200×700. Speech service absence checked. Map tiles are intentionally disabled in mocked rendering; this release does not change the live map provider or simulator connection.


## v0.23 preparation and layout

See [sortie preparation](sortie-preparation.md) for the current workflow, migration and validation. Current briefing records persist server-side and require a current pilot acknowledgement before new tracking.
