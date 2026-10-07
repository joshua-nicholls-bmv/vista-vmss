# Mission sharing, fleet reservations and briefing PDFs — v0.30

## Install

Apply `supabase/migrations/20261007001300_sharing_and_fleet.sql` after migration 012 in the dedicated VISTA database. Then stop debugging, reload files and press F5. The migration has not been applied remotely by this update.

## Share missions

Select a saved mission and choose Share / update mission. A confirmation explains that publishing makes a snapshot available to all active VISTA pilots, labelled with the aircraft's squadron. It is wing-wide sharing, not a private squadron-membership system. Browse shared missions opens a searchable squadron-filtered browser. Add to my missions imports an independent editable saved plan. The receiving pilot should review aircraft/callsign, then generate their own SimBrief briefing and acknowledgement.

Publication includes saved route points, task/load snapshot and saved mission notes. It does not publish tracking, debriefs, SimBrief documents, signatures, login credentials or authentication identifiers. Republishing increments the shared revision; existing imported plans do not change. Importing the same revision twice reuses the previous import, including after a lost response. A new published revision can be imported independently. Only the publisher can withdraw sharing. Withdrawal stops new imports and does not delete recipients' copies. Catalogue availability and aircraft reservation rules still apply at save/activation.

## Fleet availability

Fleet shows Available, Preparing or Tracking alongside the assigned pilot. Details include callsign and mission. Preparing means an activated mission has reserved the aircraft; Tracking is the sortie tracking state and does not imply that the aircraft is airborne. The read-only authenticated reservation API exposes no track or debrief data. Reserved aircraft are excluded from planner choices for every pilot, while activation still enforces reservations server-side. Fleet refreshes every 30 seconds while open and has a manual Refresh availability action. Status is a checked snapshot, not a permanent guarantee.

## Briefing PDF

Open an active or saved briefing and select Export briefing PDF. Choose the filename in the save dialog. Save note edits first. The standalone A4 PDF includes mission/aircraft/pilot, acknowledgement state, tasks, instructions, notes, fuel/loading, timings, ordered coordinate waypoints, SimBrief route and weather where imported. It is a printable briefing snapshot, not the original SimBrief OFP or an embedded live map. Draft/out-of-date imports are labelled; missing values remain unspecified. Existing Full flight plan still opens the original OFP. Portable PDF fonts are used and text wraps across numbered pages without needing external runtimes or a PDF printer.

## Validation

Release builds passed with zero warnings/errors. Disposable SQL tests covered repeated migration application, publishing/withdrawal ownership, active-pilot read access, independent route imports, retry/deduplication, fresh briefing/signature requirements, aircraft reservation labels/release and anonymous denial. Mocked WPF tests covered publication/browser/import/withdrawal and cross-pilot availability filtering, alongside planner/tracking/login regressions. PDF parser checks verified complete text and horizontal page bounds for normal and five-page long-note/long-token fixtures; all rendered pages were visually inspected. No hosted migration, real multi-pilot session or live MSFS flight was performed.
