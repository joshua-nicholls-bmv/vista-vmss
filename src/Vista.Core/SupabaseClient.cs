using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vista.Core;

public sealed class VistaApiException(string message, string? code = null, int status = 0) : Exception(message)
{
    public string? Code { get; } = code;
    public int Status { get; } = status;
}

public sealed class SupabaseClient : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };
    private readonly HttpClient http;
    private readonly VistaConfiguration config;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private AuthSession? session;
    private DateTimeOffset expiresAt;
    private readonly ILoginStore? loginStore;
    private bool remember;
    private string loginIdentifier = "";

    public SupabaseClient(VistaConfiguration configuration, HttpMessageHandler? handler = null, ILoginStore? store = null)
    {
        config = configuration;
        loginStore = store;
        if (!Uri.TryCreate(config.SupabaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https")
            throw new ArgumentException("VISTA needs a valid HTTPS Supabase URL.");
        if (!config.PublishableKey.StartsWith("sb_publishable_", StringComparison.Ordinal))
            throw new ArgumentException("Use the VISTA publishable key, never a secret/service-role key.");
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.BaseAddress = new Uri(config.SupabaseUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<Pilot> SignInAsync(string identifier, string password, CancellationToken ct = default)
    {
        remember = false;
        loginStore?.Clear();
        session = null;
        var login = identifier.Trim();
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            throw new VistaApiException("Enter your pilot identifier and password.");
        var email = login.Contains('@') ? login : login.ToUpperInvariant() + "@vista-vmss.com";
        var result = await SendAsync<AuthSession>(HttpMethod.Post, "auth/v1/token?grant_type=password",
            new { email, password }, false, ct);
        SetSession(result);
        try
        {
            var pilots = await ReadAsync<Pilot>($"pilots?auth_user_id=eq.{result.User.Id}&select=*", ct);
            var pilot = pilots.SingleOrDefault();
            if (pilot is null || pilot.Status != "active")
                throw new VistaApiException("Your login needs an active VISTA pilot profile. Contact wing operations.");
            return pilot;
        }
        catch { await SignOutAsync(); throw; }
    }

    public void RememberCurrentLogin(string identifier, bool enabled)
    {
        loginIdentifier = identifier.Trim();
        remember = enabled;
        if (!enabled) loginStore?.Clear();
        else if (session is not null) PersistSession();
    }

    public RememberedLogin? ReadRememberedLogin()
    {
        var saved = loginStore?.Load();
        if (saved is not null && (!string.Equals(saved.ProjectUrl.TrimEnd('/'), config.SupabaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(saved.RefreshToken)))
        { loginStore?.Clear(); return null; }
        return saved;
    }

    public async Task<Pilot?> RestoreLoginAsync(CancellationToken ct = default)
    {
        var saved = ReadRememberedLogin();
        if (saved is null) return null;
        loginIdentifier = saved.Identifier; remember = true;
        try
        {
            var result = await SendAsync<AuthSession>(HttpMethod.Post, "auth/v1/token?grant_type=refresh_token",
                new { refresh_token = saved.RefreshToken }, false, ct);
            SetSession(result); // Persist the rotated token before further network requests.
            var profile = (await ReadAsync<Pilot>($"pilots?auth_user_id=eq.{result.User.Id}&select=*", ct)).SingleOrDefault();
            if (profile is null || profile.Status != "active")
            { await SignOutAsync(); throw new VistaApiException("Your login needs an active VISTA pilot profile. Contact wing operations."); }
            return profile;
        }
        catch (VistaApiException error) when (error.Status is 400 or 401 or 403)
        { session = null; remember = false; loginStore?.Clear(); throw new VistaApiException("Your remembered login has expired. Please sign in again."); }
    }

    private void PersistSession()
    {
        if (remember && session is not null)
            loginStore?.Save(new(config.SupabaseUrl, loginIdentifier, session.RefreshToken));
    }

    public async Task SignOutAsync()
    {
        remember = false;
        loginStore?.Clear();
        try
        {
            if (session is not null)
                await SendAsync<JsonElement>(HttpMethod.Post, "auth/v1/logout?scope=local", null, true, CancellationToken.None);
        }
        catch (Exception e) when (e is VistaApiException or HttpRequestException or TaskCanceledException) { }
        finally { session = null; }
    }

    public async Task<Catalogue> LoadCatalogueAsync(CancellationToken ct = default)
    {
        var bases = ReadAsync<Airfield>("bases?active=eq.true&order=name", ct);
        var types = ReadAsync<AircraftType>("aircraft_types?active=eq.true&order=name", ct);
        var units = ReadAsync<Squadron>("squadrons?active=eq.true&order=name", ct);
        var planes = ReadAsync<Aircraft>("aircraft?order=serial", ct);
        var missions = ReadAsync<Mission>("missions?active=eq.true&order=title", ct);
        var elements = ReadAsync<MissionElement>("mission_elements?active=eq.true&order=title", ct);
        await Task.WhenAll(bases, types, units, planes, missions, elements);
        return new(await bases, await types, await units, await planes, await missions, await elements);
    }

    public Task<List<SharedMission>> LoadSharedMissionsAsync()=>ReadAsync<SharedMission>("shared_missions?select=id,owner_id,title,squadron,author,departure_icao,arrival_icao,revision,active,updated_at&order=updated_at.desc",default);
    public Task<List<FleetReservation>> LoadFleetReservationsAsync()=>SendAsync<List<FleetReservation>>(HttpMethod.Post,"rest/v1/rpc/fleet_reservations",new{},true,default);
    public Task<Guid> PublishMissionAsync(Guid id)=>SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/publish_mission",new{p_sortie_id=id},true,default);
    public Task<Guid> WithdrawSharedMissionAsync(Guid id)=>SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/withdraw_shared_mission",new{p_shared_id=id},true,default);
    public Task<Guid> ImportSharedMissionAsync(Guid id,Guid request)=>SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/import_shared_mission",new{p_shared_id=id,p_request_id=request},true,default);
    public Task<List<MissionArchive>> LoadMissionArchivesAsync() => ReadAsync<MissionArchive>("mission_library_archives?select=pilot_id,plan_id",default);
    public Task<Guid> SetMissionArchivedAsync(Guid id,bool archived) => SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/set_mission_archived",new{p_sortie_id=id,p_archived=archived},true,default);
    public async Task<List<Sortie>> LoadSortiesAsync(Guid pilotId, CancellationToken ct = default)
    {
        var result=new List<Sortie>();
        for(var offset=0;;offset+=200)
        {
            var page=await ReadAsync<Sortie>($"sorties?pilot_id=eq.{pilotId}&order=created_at.desc,id&limit=200&offset={offset}",ct);
            result.AddRange(page);if(page.Count<200)return result;
        }
    }
    public async Task<PilotStatistics> LoadStatisticsAsync(Guid pilotId, CancellationToken ct = default) =>
        (await ReadAsync<PilotStatistics>($"pilot_statistics?pilot_id=eq.{pilotId}", ct)).SingleOrDefault() ?? new();
    public Task<List<StoredWaypoint>> LoadMissionWaypointsAsync(Guid id, CancellationToken ct = default) =>
        ReadAsync<StoredWaypoint>($"mission_waypoints?mission_id=eq.{id}&order=position", ct);
    public Task<List<StoredWaypoint>> LoadElementWaypointsAsync(Guid id, CancellationToken ct = default) =>
        ReadAsync<StoredWaypoint>($"mission_element_waypoints?mission_element_id=eq.{id}&order=position", ct);
    public Task<List<StoredWaypoint>> LoadSortieWaypointsAsync(Guid id, CancellationToken ct = default) =>
        ReadAsync<StoredWaypoint>($"sortie_waypoints?sortie_id=eq.{id}&order=position", ct);
    public Task<Guid> SavePlanAsync(SortiePlan plan, Guid id, CancellationToken ct = default) =>
        SendAsync<Guid>(HttpMethod.Post, "rest/v1/rpc/save_sortie_plan", new { p_plan = plan, p_sortie_id = id }, true, ct);
    public Task<Guid> DeleteDraftAsync(Guid id, CancellationToken ct = default) =>
        SendAsync<Guid>(HttpMethod.Post, "rest/v1/rpc/delete_planned_sortie", new { p_sortie_id = id }, true, ct);

    public async Task<MissionCatalogue> LoadMissionCatalogueAsync(CancellationToken ct = default)
    {
        var families = ReadAsync<MissionFamily>("mission_families?order=title", ct);
        var types = ReadAsync<FamilyAircraftType>("mission_family_aircraft_types?select=*", ct);
        var options = ReadAsync<CatalogueOption>("mission_catalogue_options?order=title", ct);
        var payloads = ReadAsync<PayloadPreset>("mission_payload_presets?order=title", ct);
        var destinations = ReadAsync<PayloadDestination>("mission_payload_destinations?select=*", ct);
        var links = ReadAsync<FamilyPayload>("mission_family_payloads?select=*", ct);
        var points = ReadAsync<StoredWaypoint>("mission_element_waypoints?order=position", ct);
        var elements = ReadAsync<MissionElement>("mission_elements?order=title", ct);
        await Task.WhenAll(families, types, options, payloads, destinations, links, points, elements);
        return new(await families, await types, await options, await payloads, await destinations, await links, await points, await elements);
    }
    public Task<Guid> SaveCataloguePlanAsync(CataloguePlan plan, Guid id, CancellationToken ct = default) =>
        SendAsync<Guid>(HttpMethod.Post, "rest/v1/rpc/save_catalogue_plan", new { p_plan = plan, p_sortie_id = id }, true, ct);
    public Task SetCatalogueEnabledAsync(string kind, string code, bool enabled, CancellationToken ct = default) =>
        SendAsync<System.Text.Json.JsonElement>(HttpMethod.Post, "rest/v1/rpc/set_mission_catalogue_enabled",
            new { p_kind = kind, p_code = code, p_enabled = enabled }, true, ct);

    public Task<Guid> DuplicateSortieAsync(Guid id) => SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/duplicate_saved_sortie",new{p_sortie_id=id},true,default);
    public Task<Guid> ActivateSortieAsync(Guid id) => SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/activate_saved_sortie",new{p_sortie_id=id},true,default);
    public Task<Guid> StartSortieAsync(Guid id) => SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/start_sortie_tracking",new{p_sortie_id=id},true,default);
    public Task<int> UploadTrackAsync(Guid id,IReadOnlyList<TrackUpload> points) => SendAsync<int>(HttpMethod.Post,"rest/v1/rpc/upload_sortie_track",new{p_sortie_id=id,p_points=points},true,default);
    public Task<Guid> FinishSortieAsync(Guid id,TrackedDebrief debrief) => SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/finish_tracked_sortie",new{p_sortie_id=id,p_debrief=debrief},true,default);
    public Task<Guid> CancelActiveSortieAsync(Guid id) => SendAsync<Guid>(HttpMethod.Post,"rest/v1/rpc/cancel_active_sortie",new{p_sortie_id=id},true,default);
    public Task<SortieBriefingRecord> LoadBriefingAsync(Guid id)=>SendAsync<SortieBriefingRecord>(HttpMethod.Post,"rest/v1/rpc/load_sortie_briefing",new{p_sortie_id=id},true,default);
    public Task<SortieBriefingRecord> SaveBriefingAsync(Guid id,string key,SimBriefBriefing document)=>SendAsync<SortieBriefingRecord>(HttpMethod.Post,"rest/v1/rpc/save_sortie_briefing",new{p_sortie_id=id,p_plan_key=key,p_document=document},true,default);
    public Task<SortieBriefingRecord> SaveBriefingNotesAsync(Guid id,string notes,Guid? revision)=>SendAsync<SortieBriefingRecord>(HttpMethod.Post,"rest/v1/rpc/save_briefing_notes",new{p_sortie_id=id,p_notes=notes,p_revision=revision},true,default);
    public Task<SortieBriefingRecord> SignBriefingAsync(Guid id,Guid revision)=>SendAsync<SortieBriefingRecord>(HttpMethod.Post,"rest/v1/rpc/sign_sortie_briefing",new{p_sortie_id=id,p_revision=revision},true,default);
    private Task<List<T>> ReadAsync<T>(string path, CancellationToken ct) =>
        SendAsync<List<T>>(HttpMethod.Get, "rest/v1/" + path, null, true, ct);
    private void SetSession(AuthSession value)
    {
        if (string.IsNullOrEmpty(value.AccessToken) || string.IsNullOrEmpty(value.RefreshToken) || value.User is null || value.User.Id == Guid.Empty)
            throw new VistaApiException("VISTA received an incomplete login response.");
        session = value;
        expiresAt = DateTimeOffset.UtcNow.AddSeconds(value.ExpiresIn);
        PersistSession();
    }
    private async Task EnsureSessionAsync(CancellationToken ct)
    {
        if (session is null) throw new VistaApiException("Sign in to VISTA again.");
        if (expiresAt > DateTimeOffset.UtcNow.AddSeconds(60)) return;
        await refreshLock.WaitAsync(ct);
        try
        {
            if (session is null) throw new VistaApiException("Sign in to VISTA again.");
            if (expiresAt > DateTimeOffset.UtcNow.AddSeconds(60)) return;
            var response = await SendAsync<AuthSession>(HttpMethod.Post, "auth/v1/token?grant_type=refresh_token",
                new { refresh_token = session.RefreshToken }, false, ct);
            SetSession(response);
        }
        catch (VistaApiException error) when (error.Status is 400 or 401 or 403)
        { session = null; remember = false; loginStore?.Clear(); throw; }
        finally { refreshLock.Release(); }
    }
    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool authenticated, CancellationToken ct)
    {
        if (authenticated) await EnsureSessionAsync(ct);
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("apikey", config.PublishableKey);
        if (authenticated) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await http.SendAsync(request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            string message = "VISTA could not complete the request. Please try again.";
            string? code = null;
            try
            {
                using var json = JsonDocument.Parse(content);
                foreach (var key in new[] { "message", "msg", "error_description" })
                    if (json.RootElement.TryGetProperty(key, out var error) && error.ValueKind == JsonValueKind.String)
                    { message = error.GetString() ?? message; break; }
                if (json.RootElement.TryGetProperty("code", out var c)) code = c.GetString();
                if (code == "PGRST202" && path.Contains("briefing")) message="Briefing preparation needs VISTA migration 009. Apply the sortie briefing SQL file.";
                else if (code == "PGRST202" && (path.Contains("sortie_tracking") || path.Contains("saved_sortie") || path.Contains("sortie_track") || path.Contains("tracked_sortie") || path.Contains("active_sortie"))) message = "Live tracking needs VISTA migration 005. Apply the live sortie tracking SQL file.";
                else if (code == "PGRST202" && path.Contains("delete_planned_sortie")) message = "Mission deletion needs VISTA migration 007. Apply the new cancelled-mission deletion SQL file.";
                else if (code == "PGRST202") message = path.Contains("save_catalogue_plan")
                    ? "Mission planning needs the new catalogue planning SQL migration. Apply migration 004 in VISTA."
                    : "Planning is not enabled yet. Run the VISTA planner SQL migration.";
            }
            catch (JsonException) { }
            throw new VistaApiException(message, code, (int)response.StatusCode);
        }
        if (string.IsNullOrWhiteSpace(content)) return default!;
        return JsonSerializer.Deserialize<T>(content, JsonOptions)
            ?? throw new VistaApiException("VISTA received an empty response.");
    }
    public void Dispose() { session = null; http.Dispose(); refreshLock.Dispose(); }
    private sealed record AuthSession(string AccessToken, string RefreshToken, int ExpiresIn, AuthUser User);
    private sealed record AuthUser(Guid Id);
}
