# Initial VISTA fleet

Run the entire `supabase/setup/initial_fleet.sql` file in the dedicated VISTA SQL Editor. No new schema migration is required for this fleet setup. The file is a single atomic DO block followed by a verification query, with no temporary tables or session dependencies. This workspace has not applied it remotely.

The setup creates three aircraft types and ten operational groupings. Aircraft serials remain unique. Atlas is grouped under the exact user-requested title **Air Mobility Force** at RAF Brize Norton; this is a VISTA grouping stored in the existing `squadrons` table, not a claim that Air Mobility Force is a numbered squadron. No 24/70 Squadron allocation is imported into VISTA.

## Source boundary

Only the Atlas tail-number list was extracted from page 6 of the [provided government annex](https://assets.publishing.service.gov.uk/media/5dce8cc5e5274a076734209a/Aircraft-Tail-Numbers-Annex-10288.pdf). All 20 rows were visually checked against that page. They are ZM400 through ZM419 inclusive. The table's Tail Code column is N/A throughout; it does not supply aircraft callsigns. No callsigns have been invented or assigned.

All Typhoon types, serials and allocations come from the user's supplied roster, not the PDF or linked third-party pages. The setup preserves those user selections as virtual fleet configuration; it does not certify current real-world squadron allocations or variants. Atlas source-unit labels are retained only in `fleet-manifest.json` for traceability, not used as VISTA squadron assignments or present-day availability.

New aircraft start virtually `available` at their configured home base. This is a VISTA initialization default, not a statement about real-world serviceability. The PDF's historical sustainment entries do not determine today's status.

## Fleet

| Base | VISTA squadron/grouping | Aircraft serials |
|---|---|---|
| RAF Brize Norton | Air Mobility Force | ZM400, ZM401, ZM402, ZM403, ZM404, ZM405, ZM406, ZM407, ZM408, ZM409, ZM410, ZM411, ZM412, ZM413, ZM414, ZM415, ZM416, ZM417, ZM418, ZM419 |
| RAF Coningsby | No. 3 (Fighter) Squadron | ZK345, ZK311, ZK354, ZK364, ZJ921 |
| RAF Coningsby | No. XI (Fighter) Squadron | ZK301, ZK308, ZK318, ZK321, ZK332 |
| RAF Coningsby | No. 12 Squadron | ZK363, ZK335, ZK361, ZK368, ZK371 |
| RAF Coningsby | No. 29 Squadron | ZJ913, ZK343, ZK353, ZK381 (T3), ZJ806 (T3) |
| RAF Coningsby | No. XLI Test and Evaluation Squadron (41 TES) | ZK315, ZK379 (T3), ZK303 (T3), ZK360, ZK376 |
| RAF Lossiemouth | No. 1 (Fighter) Squadron | ZK341, ZK348, ZK314, ZK372 |
| RAF Lossiemouth | No. II (Army Cooperation) Squadron | ZK316, ZK320, ZK329, ZK333 |
| RAF Lossiemouth | No. 6 Squadron | ZK366, ZK346, ZK306, ZK313, ZK324 |
| RAF Lossiemouth | No. IX (Bomber) Squadron | ZJ924, ZJ935, ZJ914, ZJ947 |

Typhoons without an explicit T3 annotation above are configured as FGR4. No livery data is configured by this setup.

User-confirmed duplicate resolution: ZK335 belongs to 12 Squadron, ZK353 to 29 Squadron and ZJ921 to 3 Squadron, all at Coningsby. Their duplicate Lossiemouth entries were removed. Totals: 20 Atlas, 38 Typhoon FGR4 and 4 Typhoon T3; 62 distinct aircraft. Per-base totals: Brize Norton 20, Coningsby 25, Lossiemouth 17.

## Repeat behaviour and remaining setup

Matching existing records are reused; current aircraft location/status, SimBrief settings and active flags are not overwritten. A conflicting serial/home-base/type/unit assignment raises an error and rolls back the entire setup block. Verify any such discrepancy before changing existing records. No pilot assignments, login records, flight callsigns or mission definitions are changed.

Pilot home-base/squadron choices, SimBrief profiles and mission definitions remain to be supplied. After this catalogue is loaded, the next application work is pilot login and a planner for predefined missions and custom flights.
