# Planner usability — v0.28

No new SQL is required. Stop debugging, reload changed files and press F5.

The task page uses Choose → Randomise if wanted → Add task. Generate/Regenerate is removed from this page. Review & save has Regenerate entire mission with confirmation before task choices are replaced. Location and load labels are shorter, load controls hide for missions without payloads, and randomisation actions only enable with choices. The task list shows its count and supports ordering, removal and randomisation. Review mission shows the task count and waits for valid task/airport choices. Corridor settings are collapsed under Optional corridor transit, with outbound/return FL140 labels. Return to base sets the final arrival to departure and refuses to override an existing destination task.

Review shows required-field validation, links back to aircraft/airports and tasks, and a name/callsign shortcut. Save mission and Save & open SimBrief retain the server validation rules. The sidebar estimates great-circle distance through the saved task points; this is a planning estimate and may differ from SimBrief and the flown track. Missing airport coordinates are reported rather than invented. Changed plans remind the pilot to generate and import a fresh SimBrief briefing; existing server revision/signature gates remain in place.

Favourites pin plans to the top of their folder and the Operations selector. Add/remove a favourite from the selected mission's details. Favourites persist per pilot UUID on this computer under the existing VISTA settings directory; they do not synchronise between computers.

Leaving a changed planner, starting a new plan, signing out or closing the application offers Save draft / Discard / Keep editing. Save draft uses the existing validated saved-plan API: incomplete or invalid plans remain in the planner with their validation message. It does not store incomplete local drafts. Discard clears the unsaved form, preserving previously saved database records. Keep editing or closing the prompt retains edits. Opening an existing draft and successfully saving establish a clean baseline.

No duplicate-mission action or remembered planner defaults were added. No flight-history button was added.

Validation: release builds passed with zero warnings/errors. Mocked desktop checks covered dirty-state baselines, navigation waiting for a decision, retaining edits after invalid save, discard, checklist, distance availability, hidden load controls, task feedback, return to base, favourites pin/unpin/persistence, and existing planner/tracking/login regression checks. WPF task and review screens were rendered and inspected. No hosted database changes or live simulator flight were performed.
