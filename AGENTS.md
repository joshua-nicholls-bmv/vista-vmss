# VISTA development workflow

- VISTA is separate from British Midland. Never connect to or modify British Midland databases, projects, code or deployment settings.
- Deliver complete new/replacement files with exact project-relative paths, not snippets requiring manual assembly.
- Keep secrets out of source and out of client applications. Use only VISTA-prefixed environment variables.
- Once a migration is applied, append migrations; do not rewrite applied history.
- Keep pilot identity server-derived from Supabase Auth. Trusted server mutations must validate ownership and pilot status even though service_role bypasses RLS.
- Preserve UTC timestamps, explicit aviation units, UUID internal identity, ordered routes and immutable historical plans.
- Run relevant SQL tests on a disposable/local database. Never reset a linked production database.
