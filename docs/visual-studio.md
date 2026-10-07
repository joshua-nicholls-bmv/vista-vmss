# Open VISTA in Visual Studio

VISTA is a separate **C# WPF Windows desktop solution**. British Midland has not been edited. Its simulator integration is reused in separate VISTA services. The user confirmed WPF as the required interface framework.

## First run

1. Extract the complete VISTA source archive to a new folder, separate from British Midland.
2. Use Visual Studio 2026 with the **.NET desktop development** workload and .NET 10 SDK. This project targets .NET 10 for its current long-term support window. It was compiled with SDK 10.0.401. Visual Studio 2022 cannot build this target; if that is the installed version, arrange a supported target/version before opening the solution.
3. Open **Vista.sln**. Do not create a blank project or paste partial code into an existing British Midland solution.
4. Set **Vista.Desktop** as the startup project, then press **F5**.
5. Sign in with `69EAW-001` or `69EAW-001@vista-vmss.com` and the password assigned in VISTA Supabase Auth. Mark and Joe use their own identifiers/passwords. No test passwords are supplied.
6. In the dedicated VISTA Supabase SQL Editor, run the **complete** `supabase/migrations/20261006000300_planner_api.sql` file once. This is the new planning API. Do not rerun the earlier migrations or fleet/pilot setup just to enable planning.

The supplied `src/Vista.Desktop/appsettings.json` already contains the dedicated VISTA project URL and the publishable key provided by the user. No service-role key is used or needed. Passwords and access tokens remain in memory. Selecting Remember my login saves a refresh token protected for your Windows account and restores the session at startup. Signing out deletes it. Leave the checkbox clear to require login after closing the application.

## Included now

- Login through Supabase Auth, with refresh-token handling and local sign-out.
- Pilot header in `Name - Identifier - Callsign` format.
- Overview with the authenticated pilot's drafts and statistics.
- Live fleet catalogue with home-base and text filters.
- Custom flight planning with aircraft, departure/arrival/alternate, flight callsign, optional route text and ordered coordinate points.
- Predefined mission route loading and reusable mission-element insertion when operations has populated those catalogues.
- Saved draft creation/update, reopening, and confirmed cancellation.

There are no fabricated mission routes. If the mission/element dropdowns are empty, those catalogues have not yet been populated. Custom flights work with manually entered coordinates. Aircraft availability is rechecked by the database on save; pilots cannot necessarily see another pilot's live allocation, so an apparently available aircraft may be rejected if it is already reserved.

To save a custom draft: select New flight plan, enter a title and flight callsign, choose an aircraft and type departure/arrival ICAOs, add at least one route point, and enter its identifier/coordinates. Tab out of the final edited cell, then Save flight plan. Reopen it in My sorties. Editing a copied element point removes its lineage and makes it an independent custom point. Saving a predefined draft copies the mission's current revision and route on the server.

## Project boundaries

`Vista.Core` contains HTTP/Supabase configuration, authentication, records and planning requests. `Vista.Desktop` contains WPF XAML, commands and view-model logic. No third-party NuGet packages are needed. Forms are built from complete XAML and C# files, suitable for subsequent complete-file replacement.

The database planning functions are the first trusted mutation boundary. They derive pilot identity from `auth.uid()`, validate active status and ownership, and save route snapshots atomically. Direct client table writes remain unavailable. Completion and telemetry still belong to a future trusted ACARS/API path; the new functions cannot elevate pilot roles or mark flights completed.

## Next stages

SimConnect connection and a local tracking diagnostic are included. Simulator aircraft validation, briefing/reservation, flight start, telemetry upload, debrief submission and SimBrief dispatch are pending. See the British Midland reuse guide for implementation boundaries. The desktop planner stores route data and route text but does not contact SimBrief. No Windows installer or signing certificate is included.

Build and local mock integration/SQL checks passed. Live Auth/database login was not tested because no pilot password was supplied. The hosted planning migration has not been applied by this workspace.

References: [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy), [Supabase password login](https://supabase.com/docs/reference/javascript/auth-signinwithpassword), [Supabase database functions](https://supabase.com/docs/guides/database/functions).


## Airport-led planning

The planner now begins with typed ICAOs, recognised airfield names and aircraft selection, followed by compatible mission suggestions and ordered waypoint previews. A custom route remains available when there are no matches. See [workflow and catalogue requirements](planning-workflow.md). Restart debugging to load these changes.

