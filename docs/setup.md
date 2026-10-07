# Initial setup

## Dedicated hosted database

1. Use the separate VISTA organisation and its new Supabase project. Confirm the project name, organisation and project reference in its dashboard. This foundation has no remote link or existing credentials.
2. Confirm that the target database has none of the VISTA tables already present. This migration creates new objects; it deliberately fails on existing names instead of overwriting them.
3. Open `supabase/migrations/20261006000100_vista_initial.sql` and run the **entire file once** in that VISTA project's SQL Editor. The transaction commits only when every statement succeeds.
4. Confirm that `bases` contains EGXC / RAF Coningsby, EGQS / RAF Lossiemouth and EGVN / RAF Brize Norton, all marked primary. RLS should be enabled on all 14 tables.
5. Configure VISTA Auth for the eventual client applications. Local signups are disabled in config.toml; hosted Auth settings must be configured separately. Provision users through an administrative flow, then insert their pilot profiles through the trusted server or SQL Editor. Supply the corresponding `auth.users.id`; do not create pilot profiles from arbitrary user metadata. Assign the first administrator explicitly in the dedicated VISTA database.
6. Add verified aircraft types, squadrons and aircraft. Add secondary airfields to `bases` with `category='secondary'`. No assumed squadron assignments, aircraft fleets or real mission routes are seeded.
7. Fill the VISTA values from `.env.example` into private environment configuration. The service-role key belongs only on the server. It must never be shipped in an operations browser or ACARS client.

The WPF starter uses authenticated database functions for draft planning; apply `20261006000300_planner_api.sql` once to enable them. Direct pilot table mutations remain closed. Telemetry/completion/SimBrief operations still require the future trusted API described in `services/api/README.md`. See `docs/visual-studio.md` for running the desktop application.

## Existing VISTA installation

The user reports applying the initial migration. Project reference: `dlqfpbqenqumdlogfrxo`. Apply `20261006000200_pilot_display.sql` next if it has not already been run; do not rerun either schema migration. It permits unassigned pilot callsigns and provides `Name - Identifier - Callsign` through `pilots.display_label`.

Create pilot accounts privately in Supabase Auth, then link their Auth UUIDs to VISTA pilot profiles. Assign the administrator role only to authorised administrators. Pilot rosters and account setup records are not included in this public repository.

The user has subsequently reported completing pilot setup. Next, run the full `supabase/setup/initial_fleet.sql` file to load the confirmed fleet. No additional migration is needed. See `docs/fleet-setup.md` for source boundaries, duplicate resolution and repeat behaviour.

## CLI alternative and local validation

The supplied `supabase/config.toml` is a local configuration. With the Supabase CLI and Docker installed, from the `vista` directory:

```sh
supabase start
supabase db reset --local
psql "postgresql://postgres:postgres@127.0.0.1:54322/postgres" -v ON_ERROR_STOP=1 -f supabase/tests/foundation.sql
```

Reset recreates only the disposable local database when `--local` is used. Tests insert temporary users and fixtures, then roll back. They require the local database owner connection, not a browser token.

For a fresh hosted project, choose either the SQL Editor procedure above or CLI-managed migrations. If using the CLI, confirm the VISTA project reference before `supabase link --project-ref <VISTA_PROJECT_REF>`, inspect `supabase db push --dry-run`, and then apply `supabase db push`. Do not push the initial migration again after manually running it in SQL Editor; reconcile migration history first using the Supabase CLI migration-repair workflow.

Do not use a remote reset for installation. Nothing in the delivered project is linked to a remote Supabase database.

References: [Supabase migration workflow](https://supabase.com/docs/guides/local-development/database-migrations), [RLS](https://supabase.com/docs/guides/database/postgres/row-level-security).
