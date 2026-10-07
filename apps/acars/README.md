# ACARS client

The WPF VISTA client starts in `src/Vista.Desktop`, using shared services in `src/Vista.Core`. Login, fleet selection and pilot-owned draft planning are implemented. The next ACARS stage will add active sorties, approximately five-second telemetry, resilient upload and debrief submission using the British Midland architectural concepts. Do not reuse its credentials or connect to its database. Upload telemetry with a stable per-sortie sequence number for retry deduplication. No simulator integration is implemented yet.
