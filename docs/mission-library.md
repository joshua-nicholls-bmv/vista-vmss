# Mission library — v0.25

Apply migration 011 after 010 in the dedicated VISTA database, then restart Visual Studio debugging.

Saved Missions has Not yet flown, Previously flown and Archived folders, counts and a search across name, squadron, route and task. Select a card for its actions. Completed flights stay replayable; completion never archives a plan. Archive and Restore are explicit pilot-owned actions and unavailable for active missions. Archived plans cannot activate until restored.

Replay attempts created from this release share a plan identifier, so each plan has one card, a completed-flight count and last-flown date. Earlier duplicate records have no recorded lineage; this migration deliberately does not guess which similarly named missions should be merged or delete existing rows.

Operations shows simulator, briefing and aircraft readiness, with a next-step action. Connect/Disconnect share one control. Sign briefing opens review and paperwork; Start tracking still requires a signed current briefing, fresh simulator data and aircraft confirmation. Finish flight is enabled only when parked and ready to submit. Aircraft confirmation appears after signing. Retry sync appears for a paused upload with queued points. Deactivate sits with the active mission; Abort flight requires confirmation and retains recorded data. There is no flight-history button.

Validation: zero-warning/error build, mocked WPF folder/search/archive/restore/replay grouping checks and existing tracking/login regressions passed. SQL lifecycle/RLS checks ran in a disposable database, including migration repeatability, archived-flight blocking, active archive rejection and foreign-owner rejection. WPF cards and Operations layouts rendered and inspected. No hosted migration or live simulator flight was performed.
