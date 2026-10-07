# Validation performed

On 6 October 2026 the complete migration executed successfully in a disposable PGlite PostgreSQL runtime. Supabase roles (`anon`, `authenticated`, `service_role`) and the minimal `auth.users` / `auth.uid()` interface were emulated. This was an isolated in-memory database with no live project credentials.

The delivered `supabase/tests/foundation.sql` then passed checks for:

- Three initial primary bases.
- Secondary airfield support, predefined mission planning and reusable element expansion.
- Sortie coordinates retained independently when mission template coordinates change.
- Route required before briefing; unique route ordering and coordinate bounds.
- Route and plan immutability after briefing, and legal lifecycle transitions.
- Exclusive aircraft reservation.
- Duplicate telemetry sequence rejection.
- Atomic completion, identical retry success, conflicting retry rejection and stable statistics.
- Deferred completed-sortie/debrief consistency.
- Pilot-specific profile, sortie, telemetry and statistics visibility.
- No pilot role escalation or direct completion RPC permission.
- Operations visibility, suspended profile restrictions and anonymous denial.

This verifies SQL execution and database behaviours. It is not a hosted Supabase deployment test or a complete API, Auth, SimBrief or simulator integration test. Docker, the Supabase CLI and a native PostgreSQL client were unavailable in this workspace; run the supplied tests in a local Supabase instance before hosted deployment. No remote migration was applied.

The second migration was also applied to that local test runtime. Pilot-display tests verified multiple unassigned callsigns, pending/assigned display labels, blank callsign rejection and assigned callsign uniqueness. The foundation tests were repeated after this migration to verify existing lifecycle and access behaviour.

The initial pilot setup file was tested with the supplied UUIDs and expected login emails in the isolated runtime. Checks verified three profiles, Josh's administrator role, pending callsign labels, transaction rollback on an email mismatch, and safe repetition preserving an assigned callsign and a subsequently changed role. No hosted user records were accessed or modified.

After a reported missing temporary-table error in SQL Editor, the setup file was replaced with one self-contained atomic DO block. It no longer relies on temporary tables or shared session state. The same setup and regression checks passed again with the replacement file.

The Atlas table on page 6 of the supplied PDF was extracted with pdfplumber and visually checked on a rendered image. It contains exactly 20 distinct Atlas serials, ZM400 through ZM419; Tail Code values are N/A. No Typhoon data was imported from that source.

The fleet setup executed successfully in the isolated PostgreSQL runtime. Every serial/type/base/unit assignment was compared with the manifest. Tests verified 62 unique aircraft, per-base totals of 20/25/17, user-selected duplicate resolution, repeated setup preserving availability/location/SimBrief settings, atomic rollback on an existing assignment conflict, and authenticated pilot read access without fleet write access. The earlier schema/lifecycle/profile checks also passed. No hosted fleet records were accessed or modified.

## WPF starter and planning API

The v0.6 branding redesign embeds the user's supplied crest unchanged and applies blue #6BA4B8, red #DB4612, navy surfaces and preferred Effra typography to login and all four application pages. Effra is not installed on the build machine; visual checks therefore use its Segoe UI fallback. The updated solution open in Visual Studio was built using a separate output directory to preserve the currently running application. That build completed with zero warnings/errors. Desktop mock/regression checks passed with the redesigned templates, and all five screens were rendered for inspection. Theme/project/layout files were backed up before direct replacement; the open solution's credentials, C# logic and database files were not changed.

The separate WPF solution compiled in Release with .NET SDK 10.0.401, with zero warnings/errors. Isolated HTTP mock checks exercised identifier-to-email login, access-token refresh, catalogue loading, 20-Atlas filtering, plan request serialization, pilot display labels, edited-element lineage removal and sign-out clearing of profile/catalogue state. WPF XAML/resources were instantiated and the login, overview, planner and fleet views rendered offscreen for visual inspection. This is not a full manual native-interface test.

The third SQL migration executed successfully in the local PostgreSQL runtime. Checks exercised authenticated draft create/update, stable sortie identity, route snapshots, rollback of invalid route updates, rejection of cross-pilot edits/cancellation even for an operations profile, server copying of predefined mission waypoints, repeat-safe draft cancellation, terminal-draft immutability and anonymous denial. Earlier schema/fleet/profile checks passed in the same run.

The dedicated VISTA Auth settings endpoint returned HTTP 200 using the supplied publishable key in a read-only connection check. No real pilot password was supplied. Live Supabase Auth login and hosted planning have not been tested, and no remote migration was applied. No website was published. SimConnect and SimBrief integration are not implemented.

## v0.7 login and simulator checks

Build completed with zero warnings/errors. Desktop checks passed for current-user DPAPI encryption/decryption, corrupt-file cleanup, restart without a password, saved token rotation, explicit sign-out removal, unchecked-login opt-out, expired token removal, project binding, transient network preservation, and existing planner flows. Tracking replays passed for movement, takeoff, touchdown rate, additional circuits, parked confirmation, invalid fuel and reconnect/midair baselines. All six WPF screens rendered. The installed MSFS native/managed libraries loaded and a connection attempt returned without a crash. A complete live simulator flight remains unverified. Dummy credentials and mocked HTTP were used; no real database writes occurred. DPAPI tests ran under the Windows user because the sandbox account has no usable DPAPI user profile.


## v0.8 workflow checks

Typed ICAO normalisation/recognition, unknown inputs, exact departure/arrival pairing, type/squadron mission restrictions, ordered waypoint previews, stale asynchronous response rejection, explicit route adoption, custom conversion/preservation, invalid alternate rejection, predefined/custom serialization and reopening drafts passed in the WPF mock harness. Existing remembered-login and tracking checks passed again. No live database writes or schema changes were made. Preview routes used synthetic test fixtures only.



## v0.23 preparation and layout

See [sortie preparation](sortie-preparation.md) for the current workflow, migration and validation. Current briefing records persist server-side and require a current pilot acknowledgement before new tracking.
