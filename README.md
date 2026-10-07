# VISTA — alpha

Virtual Integrated Sortie & Tasking Application for virtual RAF operations in Microsoft Flight Simulator.

## Download and run

Download the **Windows x64 ZIP** from [Releases](https://github.com/joshua-nicholls-bmv/vista-vmss/releases). Extract the entire ZIP into a folder, then run **Vista.Desktop.exe**. Do not run it inside the ZIP. This portable alpha includes .NET; Visual Studio and a separate .NET installation are not required.

Use the pilot account supplied by VISTA operations. Start MSFS and load the aircraft before connecting. Keep all DLLs and appsettings.json beside the application. This alpha is unsigned; Windows may show a publisher warning.

## Included

- Typhoon and Atlas fleet selection with cross-pilot aircraft reservations.
- Custom and randomised missions, combined tasks, mission-assigned destinations and optional Lichfield transit.
- Saved mission folders, favourites, sharing/import and archive/restore.
- SimBrief dispatch and imported briefings with acknowledgement and printable PDF export.
- SimConnect tracking, mission-point progress, maps, recovery, retryable uploads and post-flight debriefs.
- Pilot settings and optional Windows-protected remembered login.

Shared missions are available to active VISTA pilots and labelled by squadron. Favourites are stored per pilot on the current computer. PDF exports contain a briefing snapshot, not a live map or the original SimBrief OFP.

## Alpha testing

See [ALPHA-TESTING.md](ALPHA-TESTING.md). Report problems through this repository's Issues with steps, expected/actual behaviour, aircraft, simulator version and screenshots. A complete live multi-pilot flight remains part of alpha testing; local mocked checks and a packaged startup smoke test are not a substitute for it.

## Build from source

Open Vista.sln in Visual Studio with the .NET desktop development workload and .NET 10 SDK. Set Vista.Desktop as startup project. Run `scripts/package-alpha.ps1` to create a self-contained Windows x64 build. SimConnect libraries used by the client are included under src/Vista.Desktop/Lib.

Run the mocked Windows desktop checks from the repository root:

```powershell
dotnet run --project tests/DesktopChecks/DesktopChecks.csproj -c Release
```

The test harness uses mock HTTP and dummy authentication data. It writes fixtures under work/. It does not reset or change the hosted database.

## Database / operations

The existing VISTA backend must have migrations through **013** applied by an administrator. Tester downloads do not perform migrations. Supabase schema, catalogue and incremental migrations are included for maintainers. The existing production pilot roster is deliberately omitted; provision Auth users and matching pilot profiles separately. Never point VISTA at the British Midland database.

The client contains only the intended Supabase **publishable** key. A database password, service-role key or pilot password must never be placed in the client.

See [sharing/fleet/PDF instructions](docs/sharing-fleet-pdf.md) and [planner usability](docs/planner-usability.md).

Third-party components retain their respective rights; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). No open-source licence is granted for the VISTA source by this initial upload.
