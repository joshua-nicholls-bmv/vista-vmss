# Mission-assigned arrivals — v0.26

Apply migration 012 after 011 in VISTA Supabase, then restart debugging.

Step 01 offers Choose airport, Assign from mission and Return to departure. Assign from mission permits an unknown arrival; the summary explains that assignment is pending. Select an aircraft and recognised departure, continue to Mission tasks, select a mission type, then Generate mission. This selects an enabled location/destination and compatible enabled load and opens review. Manual task selection remains available.

Return to departure uses the departure as arrival and rejects destination tasks requiring another airfield. Assign from mission uses the single destination task's airport; training-only plans return to departure. One destination task can be combined with compatible training tasks, whose ordered waypoints occur before final arrival. Aircraft, departure, catalogue availability, payload and destination validation remain enforced server-side.

Generate becomes Regenerate after tasks exist and regenerates the selected task families. Page changes, catalogue refresh, saving, briefing and replay do not regenerate tasks. Saved plans store resolved airport IDs, concrete task/load choices and route snapshots. Reopening a draft uses its resolved airport in Choose airport mode until the pilot explicitly changes the mode or regenerates.

Validation: mocked desktop checks passed for unknown arrivals, destination/load generation, stable assignment across refresh/page changes, explicit regeneration, combined tasks, return mode, outbound rejection and saved resolved destination. Disposable SQL checks passed for combined-route persistence, conflicting destination rejection, existing ownership and lifecycle rules. Release builds passed with zero warnings/errors and screens were rendered/inspected. The hosted migration and a live flight were not performed.
