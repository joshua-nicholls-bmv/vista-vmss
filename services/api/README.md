# Trusted API boundary

Draft create/update/cancel is now implemented in `20261006000300_planner_api.sql` and called by the WPF client. Those security-definer functions derive identity from `auth.uid()`, validate ownership and active status, and expose only draft operations. The guidance below remains the contract for future ACARS, telemetry, completion and SimBrief service work. Direct client table writes are still denied.

Future backend must validate Supabase access tokens, resolve the pilot from auth_user_id, check active pilot status, and enforce ownership on every mutation. Never accept a client-supplied pilot identity or trust client-supplied statistics. Only the server holds the service-role key. Direct authenticated database writes are intentionally unavailable in v0.1.

Planning transaction: create a planned sortie, copy mission waypoints when source=predefined, or assemble custom waypoints and expand each selected mission element. Assign consecutive positions starting at 1. Persist source element/waypoint IDs and coordinates, so future template edits cannot change the route. Save the final route text and structured snapshot in the sortie. Freeze the plan before briefing.

SimBrief handoff: validate departure/arrival ICAO and aircraft profile, generate a SimBrief-compatible route from the snapshot, create an export row with a unique request_key, then send it outside the transaction. Store the returned flight ID/OFP URL and response. On retries, reuse the request_key. Export records preserve request/response history. This release models the integration but does not call SimBrief.

Lifecycle: planned -> briefed -> airborne -> completed; planned/briefed/airborne -> cancelled. Aircraft and pilot active uniqueness are database-enforced. For completion call public.complete_sortie as service_role; it locks the sortie and writes its debrief atomically. Repeat identical completion safely; a conflicting second result is rejected. Derive duration and distance on the server from validated telemetry. Persist UTC event timestamps, and retain track points after completion. Debriefs are private to their pilot and operations staff. Define retention and review/edit policies before production use.

Telemetry: authenticate ownership, require airborne status, batch insert; on conflict (sortie_id, sequence) do nothing only if the payload matches. Reject conflicting reuse. Validate measured_at against the sortie timeline and apply reasonable flight-envelope checks at ingestion. These API behaviours remain implementation requirements, not delivered endpoints.
