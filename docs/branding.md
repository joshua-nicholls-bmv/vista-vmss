# VISTA visual identity

- User-supplied 69th Expeditionary Air Wing crest, embedded unchanged as `src/Vista.Desktop/Assets/69eaw-crest.png`.
- Air force blue: **#6BA4B8**. Navigation, numeric highlights, focus states and secondary actions.
- Red arrows red: **#DB4612**. Primary actions and restrained accents.
- Deep navy surfaces, blue-grey borders, consistent spacing, left navigation and styled tables/forms across pilot login, overview, planner, fleet and sortie register.
- Preferred typeface: **Effra**. WPF uses `Effra, Segoe UI` so that computers with Effra installed use it; others use Segoe UI. Effra was not installed on the build machine and no licensed font files were supplied, so it is not embedded. Install a licensed copy on the application computer, or provide licensed files for a future embedding update.

This is a visual update with no SQL migration or credential changes. The open extracted VISTA solution receives complete replacements for `App.xaml`, `MainWindow.xaml` and `Vista.Desktop.csproj`, plus the new crest asset. Existing configuration, C# logic and database files are preserved. Original versions of affected files are backed up in the task workspace before direct application.


## Slate palette update (v0.11)

Background #182129; cards #25313B; controls/navigation #202B34; raised surfaces #303E49; borders #465763. Air force blue #6BA4B8 remains the identity and routine-action colour. Red arrows red #DB4612 is reserved for starting flight tracking and submitting completed sorties. Text is soft white #F1F5F7; secondary text is grey-blue #B2BFC8. Green #86B79B and amber #E0B46B accompany connection, flight and pending-upload text; status is never conveyed by colour alone. The acronym appears beneath VISTA in the header.
