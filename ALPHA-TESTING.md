# VISTA Alpha 0.1.0 — testing guide

1. Extract the complete Windows x64 ZIP and launch Vista.Desktop.exe.
2. Sign in with your assigned pilot identifier and password. Check remembered login and explicit sign-out.
3. Plan a Typhoon or Atlas mission. Try randomised locations/loads, combined tasks, destination assignment, corridor transit and return to base.
4. Save, edit, favourite, archive/restore and share a plan. On another pilot account, import the shared plan and review it.
5. Activate a mission, generate its SimBrief flight plan, import the briefing, review and sign it. Export its PDF and check the details.
6. Load the correct aircraft in MSFS, connect, confirm the aircraft and start tracking. Check waypoint progress and a full departure/arrival.
7. With two pilots, confirm that an activated aircraft shows the assigned pilot and cannot be reserved twice. Deactivation should release it.
8. Complete a sortie and submit the debrief. Confirm that the mission remains replayable. Try restarting the app to check recovery during a separate test.

Report the build version (0.1.0-alpha.1), simulator version, aircraft/add-on, mission name, steps and screenshots. Do not include passwords, login tokens or private configuration in an issue.

This is a portable, unsigned alpha. It includes the .NET runtime. MSFS/SimConnect aircraft behaviour, clean-machine compatibility and complete multi-pilot flights still need tester coverage. Internet access is needed for authentication, hosted missions, map tiles and SimBrief. The backend must be at migration 013; testers do not run SQL.
