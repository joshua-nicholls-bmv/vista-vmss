# VISTA v0.23 — sortie preparation

## Install

Run the complete `supabase/migrations/20261007000900_sortie_briefing.sql` in the dedicated VISTA Supabase SQL Editor after migrations 001–008. This migration stores briefing snapshots, notes and signatures and requires a current signed briefing before a new tracked flight. It has not been applied remotely by this update.

Stop Visual Studio debugging, reload changed files and press F5. Your existing solution folder remains the same.

## Pilot workflow

1. In Operations, search/select a named saved mission and choose Activate & brief.
2. Open SimBrief from the briefing, generate the flight and import its briefing. Older generated flights must be regenerated from VISTA because the link now identifies the current plan revision.
3. Review overview, route, mission tasks and weather. Imported fuel, loading and timings appear where available; missing information stays unspecified. The full OFP opens separately.
4. Add and save any mission notes. Select the paperwork action, tick the acknowledgement and sign. This records the authenticated pilot and server time, then closes the briefing.
5. Connect the simulator, confirm that its aircraft matches the mission, and start tracking.

A changed plan or newly imported briefing requires a fresh signature. Unsaved notes prevent signing. Existing airborne recovery remains supported. Saved briefings and signatures reload on the same mission; another pilot cannot read or alter them.

## Layout

Operations is the map-led flight workspace with next waypoint, telemetry, movement times and expandable events/debrief. Mission Planner has three stages: aircraft/airports, combined mission tasks, then review/save. Saved Missions has list and detail panes; Fleet includes squadron filtering and aircraft details. Mission Control remains admin-only. Pilot Settings opens from the profile button.

Voice-over controls remain removed. Visual mission entry alerts remain available.

## Mission instructions

Strike/airdrop instructions are generated from saved ordered route points. Exercise nine-line information is explicitly incomplete where elevation, marks or altitude references have not been supplied. Clee Hill retains the supplied visual description and exercise restrictions, with the altitude reference awaiting confirmation. These are simulator exercise briefs.

## Validation

Release build passed with zero warnings/errors. Mocked desktop checks covered preparation, saved notes/signatures, stale plan rejection, re-import acknowledgement, tracking/recovery, planner and login flows. Disposable database checks covered migration repeatability, ownership/RLS, revision conflicts and signed-flight gates. WPF screens were rendered and inspected. Hosted migration, live SimBrief retrieval and an end-to-end MSFS flight remain for user verification.
