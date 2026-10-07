# British Midland tracking reuse

Source inspected read-only: the separately maintained British Midland solution and its WPF project. VISTA remains a separate solution, Auth project and database. No British Midland source or database was modified.

## Included in VISTA v0.7

- `SimulatorConnection.cs` extracts the window-message hook, MSFS handshake, packed aircraft structure, data registration, one-second requests, timeout and disconnect pattern from `MainWindow.xaml.cs`. The original data definition order and native packing are retained. Four engine-combustion variables support the Atlas as well as twin-engine aircraft.
- `SimulatorTelemetry.cs` extracts the OUT/OFF/ON/IN detection rules into a testable flight tracker: ground movement above 1 kt with brake released, ground-to-air transition, touchdown using the last airborne vertical speed, and confirmed parking. Confirmation uses elapsed sample time rather than timer tick counts. Initial and post-gap samples do not invent transitions; invalid fuel readings do not overwrite the last valid reading. Repeated takeoffs and landings support circuits.
- Fresh telemetry supplies track points every five seconds, retaining the cadence of `TrackRecorder.cs`. The local diagnostic buffer retains the most recent 1,800 points and 200 event messages. It is not a complete persistent flight recorder.
- Remembered login follows British Midland's Windows current-user DPAPI approach. VISTA stores only its project URL, identifier and revocable refresh token in its own `%LOCALAPPDATA%\VISTA\login.dat`. Tokens are updated after refresh rotation. Passwords and access tokens are not persisted. Explicit sign-out clears the saved login; invalid credentials are discarded, while transient network failures preserve the token for a later restart/retry.
- Local SDK libraries are referenced from `Vista.Desktop/Lib`, making the source build independent of British Midland's installed paths. They are the MSFS 2024 SDK libraries already installed on this computer. Build targets x64.

## Still to integrate

British Midland's stable airborne phase engine, approach/go-around confirmation, aircraft validation, durable flight recovery and live operations upload are not yet transplanted. Their implementation is coupled to British Midland's UI, airline bookings, reports and backend. Reuse their simulator rules, then connect them to VISTA-specific services rather than importing the window or backend wholesale.

VISTA needs trusted, ownership-checked APIs for briefing/reservation, start, telemetry ingestion and atomic debrief completion. The existing planner API supports draft operations only. The Simulator page therefore provides a local connection/tracking test and never changes a database sortie's status or pilot statistics. SimBrief dispatch is also pending.

## Check in MSFS

Stop debugging, reload changed files if Visual Studio prompts, then run VISTA again. Sign in, open **Simulator**, load an aircraft in MSFS and choose **Connect to MSFS**. Check title, location, altitude and speed against the simulator. Choose **Start local test** while parked; make a short flight and check OUT/OFF/ON/IN events. Disconnect/reconnect to check that no extra landing appears. Stop the test when finished. A local test is cleared by sign-out or application exit.

Compilation, mocked login flows, DPAPI round trips, tracking replays and a native-library connection attempt were checked. A full real-simulator flight and hosted sortie submission were not verified.

References: [MSFS managed SimConnect client guidance](https://docs.flightsimulator.com/msfs2024/html/6_Programming_APIs/SimConnect/Programming_SimConnect_Clients_Using_Managed_Code.htm), [Windows DPAPI current-user protection](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata).
