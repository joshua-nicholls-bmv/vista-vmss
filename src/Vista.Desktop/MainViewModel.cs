using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using Vista.Core;

namespace Vista.Desktop;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Notify(name); return true; }
}

public sealed class WaypointRow : ObservableObject
{
    private int position;
    private string identifier = "POINT", instructions = "";
    private decimal latitude, longitude;
    private int? altitudeFt, speedKts;
    private Guid? sourceElementWaypointId, elementInstance;
    public int Position { get => position; set => Set(ref position, value); }
    public string Identifier { get => identifier; set { if (Set(ref identifier, value)) ClearLineage(); } }
    public decimal Latitude { get => latitude; set { if (Set(ref latitude, value)) ClearLineage(); } }
    public decimal Longitude { get => longitude; set { if (Set(ref longitude, value)) ClearLineage(); } }
    public int? AltitudeFt { get => altitudeFt; set { if (Set(ref altitudeFt, value)) ClearLineage(); } }
    public int? SpeedKts { get => speedKts; set { if (Set(ref speedKts, value)) ClearLineage(); } }
    public string Instructions { get => instructions; set { if (Set(ref instructions, value)) ClearLineage(); } }
    private void ClearLineage() { sourceElementWaypointId = null; elementInstance = null; }
    public PlanWaypoint ToPlan() => new(Identifier.Trim(), Latitude, Longitude, AltitudeFt, SpeedKts,
        Instructions, sourceElementWaypointId, elementInstance);
    public static WaypointRow FromStored(StoredWaypoint w, bool fromElement = false, Guid? instance = null) => new()
    {
        identifier = w.Identifier, latitude = w.Latitude, longitude = w.Longitude,
        altitudeFt = w.AltitudeFt, speedKts = w.SpeedKts, instructions = w.Instructions,
        sourceElementWaypointId = fromElement ? w.Id : w.SourceElementWaypointId,
        elementInstance = fromElement ? instance : w.ElementInstance
    };
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly SupabaseClient api;
    private SimulatorConnection? simulator;
    private SimulatorTelemetry? telemetry;
    private FlightTracker? testTracker;
    private DateTimeOffset? lastTrackAt;
    private string simulatorStatus = "MSFS disconnected", trackingSummary = "Local tracking test stopped";
    public string SimulatorStatus { get => simulatorStatus; private set => Set(ref simulatorStatus, value); }
    public string TrackingSummary { get => trackingSummary; private set => Set(ref trackingSummary, value); }
    public string SimulatorAircraft => telemetry?.AircraftTitle ?? "Waiting for simulator aircraft";
    public string SimulatorPosition => telemetry is null ? "—" : $"{telemetry.Latitude:F5}, {telemetry.Longitude:F5}";
    public string SimulatorMetrics => telemetry is null ? "—" : $"{telemetry.AltitudeFt:N0} ft   /   {telemetry.GroundSpeedKts:N0} kt   /   {telemetry.VerticalSpeedFpm:N0} fpm";
    public string SimulatorGround => telemetry is null ? "—" : telemetry.OnGround ? "ON GROUND" : "AIRBORNE";
    public ObservableCollection<string> TrackingEvents { get; } = [];
    public ObservableCollection<SimulatorTelemetry> LocalTrackPoints { get; } = [];
    public ICommand ConnectSimulatorCommand { get; }
    public ICommand DisconnectSimulatorCommand { get; }
    public ICommand StartTrackingTestCommand { get; }
    public ICommand StopTrackingTestCommand { get; }
    private Catalogue? catalogue;
    private Pilot? pilot;
    private string identifier = "", message = "", planTitle = "", flightCallsign = "", routeText = "", fleetSearch = "";
    private bool isBusy, isPredefined, refreshing;
    private bool rememberLogin;
    public bool RememberLogin { get => rememberLogin; set => Set(ref rememberLogin, value); }
    private int selectedTab;
    private Guid draftId = Guid.NewGuid();
    private DateTimeOffset? plannedDepartureAt;
    private FleetAircraft? selectedAircraft;
    private Airfield? departure, arrival, alternate, fleetBase;
    private string departureIcao = "", arrivalIcao = "", alternateIcao = "", missionPreviewStatus = "Choose a matching mission to preview its route.";
    private int previewVersion;
    public string DepartureIcao { get => departureIcao; set { if (Set(ref departureIcao, NormalizeIcao(value))) Departure = ResolveAirfield(departureIcao); } }
    public string ArrivalIcao { get => arrivalIcao; set { if (Set(ref arrivalIcao, NormalizeIcao(value))) Arrival = ResolveAirfield(arrivalIcao); } }
    public string AlternateIcao { get => alternateIcao; set { if (Set(ref alternateIcao, NormalizeIcao(value))) Alternate = ResolveAirfield(alternateIcao); } }
    public string DepartureRecognition => Recognition(DepartureIcao, Departure);
    public string ArrivalRecognition => Recognition(ArrivalIcao, Arrival);
    public string AlternateRecognition => string.IsNullOrEmpty(AlternateIcao) ? "Optional" : Recognition(AlternateIcao, Alternate);
    public ObservableCollection<Mission> MatchingMissions { get; } = [];
    public ObservableCollection<StoredWaypoint> MissionPreview { get; } = [];
    public string MissionPreviewStatus { get => missionPreviewStatus; private set => Set(ref missionPreviewStatus, value); }
    public string MissionMatchSummary => Departure is null || Arrival is null
        ? "Enter recognised departure and arrival ICAOs to find mission routes."
        : MatchingMissions.Count == 0 ? "No matching missions. Plan a custom flight or ask operations to add a route."
        : $"{MatchingMissions.Count} matching mission(s)" + (SelectedAircraft is null ? " · choose an aircraft to check compatibility" : " · compatible with your aircraft");
    public Task MissionPreviewTask { get; private set; } = Task.CompletedTask;
    public ICommand CustomRouteCommand { get; }
    private Mission? selectedMission;
    private MissionElement? selectedElement;
    private WaypointRow? selectedWaypoint;
    private Sortie? selectedSortie;
    private PilotStatistics statistics = new();
    public string Identifier { get => identifier; set => Set(ref identifier, value); }
    public string Message { get => message; private set => Set(ref message, value); }
    public bool IsBusy { get => isBusy; private set { Set(ref isBusy, value); Notify(nameof(IsReady)); Notify(nameof(CanUseMission)); CommandManager.InvalidateRequerySuggested(); } }
    public bool IsReady => !IsBusy;
    public bool IsSignedIn => pilot is not null;
    public bool IsLoginVisible => !IsSignedIn;
    public string PilotLabel => pilot?.DisplayLabel ?? "";
    public string PilotRole => pilot?.Role ?? "";
    public int SelectedTab { get => selectedTab; set {if(value==selectedTab)return;RequestPlannerExit(()=>NavigateAfterPlannerPrompt(value));} }
    public string PlanTitle { get => planTitle; set => Set(ref planTitle, value); }
    public string FlightCallsign { get => flightCallsign; set => Set(ref flightCallsign, value); }
    public string RouteText { get => routeText; set => Set(ref routeText, value); }
    public bool IsPredefined
    {
        get => isPredefined;
        set { if (Set(ref isPredefined, value)) { Waypoints.Clear(); Notify(nameof(IsCustom)); Notify(nameof(SourceLabel)); } }
    }
    public bool IsCustom => !IsPredefined;
    public string SourceLabel => IsPredefined ? "Predefined mission" : "Custom flight";
    public FleetAircraft? SelectedAircraft { get => selectedAircraft; set { Set(ref selectedAircraft, value); Notify(nameof(AircraftSummary)); UpdateMissionMatches(); UpdateBuilderChoices(); } }
    public Airfield? Departure { get => departure; set { Set(ref departure, value); if (value is not null) Set(ref departureIcao, value.Icao, nameof(DepartureIcao)); Notify(nameof(DepartureRecognition)); Notify(nameof(RouteSummary)); UpdateMissionMatches(); if (!refreshing) CompileBuilderPreview(); } }
    public Airfield? Arrival { get => arrival; set { Set(ref arrival, value); if (value is not null) Set(ref arrivalIcao, value.Icao, nameof(ArrivalIcao)); Notify(nameof(ArrivalRecognition)); Notify(nameof(RouteSummary)); UpdateMissionMatches(); if (!refreshing) CompileBuilderPreview(); } }
    public Airfield? Alternate { get => alternate; set { Set(ref alternate, value); if (value is not null) Set(ref alternateIcao, value.Icao, nameof(AlternateIcao)); Notify(nameof(AlternateRecognition)); if (!refreshing) CompileBuilderPreview(); } }
    public Mission? SelectedMission
    {
        get => selectedMission;
        set { if (Set(ref selectedMission, value)) { if (IsPredefined && !refreshing) Waypoints.Clear(); Notify(nameof(MissionDescription)); Notify(nameof(CanUseMission)); MissionPreviewTask = PreviewMissionAsync(value); } }
    }
    public string MissionDescription => SelectedMission?.Description ?? "Choose a mission, then load its route.";
    public MissionElement? SelectedElement { get => selectedElement; set => Set(ref selectedElement, value); }
    public WaypointRow? SelectedWaypoint { get => selectedWaypoint; set => Set(ref selectedWaypoint, value); }
    public Sortie? SelectedSortie { get => selectedSortie; set { Set(ref selectedSortie,value); Notify(nameof(SelectedMissionSummary)); Notify(nameof(CanDeleteSelectedDraft)); NotifyPreparation(); } }
    public bool CanDeleteSelectedDraft => SelectedSortie?.Status is "planned" or "cancelled" && SelectedSortie.StartedAt is null;
    public string SelectedMissionSummary => SelectedSortie is null ? "Select a saved mission to review and activate it." : $"{Bases.FirstOrDefault(b=>b.Id==SelectedSortie.DepartureBaseId)?.Icao} → {Bases.FirstOrDefault(b=>b.Id==SelectedSortie.ArrivalBaseId)?.Icao} · {Fleet.FirstOrDefault(a=>a.Id==SelectedSortie.AircraftId)?.Serial} · {SelectedSortie.Callsign}";
    public string AircraftSummary => SelectedAircraft?.Label ?? "Choose an aircraft";
    public string RouteSummary => $"{Departure?.Icao ?? "----"} / {Arrival?.Icao ?? "----"}";
    public int WaypointCount => Waypoints.Count;
    public int FleetCount => Fleet.Count;
    public int DraftCount => Sorties.Count(s => s.Status == "planned");
    public int MissionCount => Missions.Count;
    public long CompletedSorties => statistics.CompletedSorties;
    public string FlightHours => statistics.FlightHours.ToString("0.00");
    public string FleetSearch { get => fleetSearch; set { Set(ref fleetSearch, value); FleetView.Refresh(); } }
    public Airfield? FleetBase { get => fleetBase; set { Set(ref fleetBase, value); FleetView.Refresh(); } }
    public ObservableCollection<Airfield> Bases { get; } = [];
    public ObservableCollection<FleetAircraft> Fleet { get; } = [];
    public ObservableCollection<FleetAircraft> AvailableAircraft { get; } = [];
    public ObservableCollection<Mission> Missions { get; } = [];
    public ObservableCollection<MissionElement> Elements { get; } = [];
    public ObservableCollection<WaypointRow> Waypoints { get; } = [];
    public ObservableCollection<Sortie> Sorties { get; } = [];
    public ICollectionView FleetView { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SignOutCommand { get; }
    public ICommand NewPlanCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand AddPointCommand { get; }
    public ICommand RemovePointCommand { get; }
    public ICommand MovePointUpCommand { get; }
    public ICommand MovePointDownCommand { get; }
    public ICommand UseMissionCommand { get; }
    public bool CanUseMission => IsSignedIn && !IsBusy && SelectedAircraft is not null && SelectedMission is not null && MatchingMissions.Contains(SelectedMission);
    public ICommand AppendElementCommand { get; }
    public ICommand EditDraftCommand { get; }
    public ICommand DeleteDraftCommand { get; }
    public ICommand ClearFleetFilterCommand { get; }

    public MainViewModel(SupabaseClient client)
    {
        api = client;
        InitializeMissionBuilder(); InitializeLiveTracking();
        CustomRouteCommand = new RelayCommand(() => { isPredefined = false; Notify(nameof(IsPredefined)); Notify(nameof(IsCustom)); Notify(nameof(SourceLabel)); Message = "Custom route: edit points or append a reusable element."; }, () => IsSignedIn && !IsBusy);
        ConnectSimulatorCommand = new RelayCommand(() => simulator?.Connect(), () => IsSignedIn && simulator is not null && !IsBusy);
        DisconnectSimulatorCommand = new RelayCommand(() => simulator?.Disconnect(), () => simulator is not null && !IsBusy);
        StartTrackingTestCommand = new RelayCommand(StartTrackingTest, () => IsSignedIn && telemetry is not null && testTracker is null && !IsBusy);
        StopTrackingTestCommand = new RelayCommand(StopTrackingTest, () => testTracker is not null && !IsBusy);
        FleetView = CollectionViewSource.GetDefaultView(Fleet);
        FleetView.Filter = value => value is FleetAircraft a && (FleetUnit=="All squadrons"||a.Unit==FleetUnit) && (FleetBase is null || a.Aircraft.HomeBaseId == FleetBase.Id)
            && (string.IsNullOrWhiteSpace(FleetSearch) || $"{a.Serial} {a.Type} {a.Unit}".Contains(FleetSearch, StringComparison.OrdinalIgnoreCase));
        RefreshCommand = new AsyncCommand(() => RunAsync(RefreshAsync), () => IsSignedIn && !IsBusy);
        SignOutCommand = new AsyncCommand(() => {RequestPlannerExit(()=>_=RunAsync(async () => { await api.SignOutAsync(); ClearSession(); Message = "Signed out."; }));return Task.CompletedTask;}, () => IsSignedIn && !IsBusy);
        NewPlanCommand = new RelayCommand(()=>RequestPlannerExit(NewPlan), () => IsSignedIn && !IsBusy);
        SaveCommand = new AsyncCommand(() => RunAsync(SaveAsync), () => IsSignedIn && !IsBusy && Waypoints.Count > 0);
        AddPointCommand = new RelayCommand(() => Waypoints.Add(new() { Identifier = $"POINT{Waypoints.Count + 1}" }), () => IsCustom && !IsBusy && Waypoints.Count < 300);
        RemovePointCommand = new RelayCommand(() => { if (SelectedWaypoint is not null) Waypoints.Remove(SelectedWaypoint); }, () => IsCustom && !IsBusy && SelectedWaypoint is not null);
        MovePointUpCommand = new RelayCommand(() => MovePoint(-1), () => IsCustom && !IsBusy && SelectedWaypoint is not null && Waypoints.IndexOf(SelectedWaypoint) > 0);
        MovePointDownCommand = new RelayCommand(() => MovePoint(1), () => IsCustom && !IsBusy && SelectedWaypoint is not null && Waypoints.IndexOf(SelectedWaypoint) < Waypoints.Count - 1);
        UseMissionCommand = new AsyncCommand(() => RunAsync(UseMissionAsync), () => IsSignedIn && !IsBusy && SelectedMission is not null && SelectedAircraft is not null && MatchingMissions.Contains(SelectedMission));
        AppendElementCommand = new AsyncCommand(() => RunAsync(AppendElementAsync), () => !IsBusy && IsCustom && SelectedElement is not null);
        EditDraftCommand = new AsyncCommand(() => {RequestPlannerExit(()=>_=RunAsync(EditDraftAsync));return Task.CompletedTask;}, () => !IsBusy && SelectedSortie?.Status == "planned");
        DeleteDraftCommand = new AsyncCommand(() => RunAsync(DeleteDraftAsync), () => !IsBusy && CanDeleteSelectedDraft);
        ClearFleetFilterCommand = new RelayCommand(() => { FleetSearch = ""; FleetBase = null; FleetUnit="All squadrons"; });
        Waypoints.CollectionChanged += (_, _) => { Renumber(); Notify(nameof(WaypointCount)); CommandManager.InvalidateRequerySuggested(); };
    }

    private static string NormalizeIcao(string? value) => (value ?? "").Trim().ToUpperInvariant();
    private Airfield? ResolveAirfield(string icao) => Bases.FirstOrDefault(b => b.Active && b.Icao.Equals(icao, StringComparison.OrdinalIgnoreCase));
    private static string Recognition(string icao, Airfield? airfield) => airfield?.Name ??
        (icao.Length == 0 ? "Enter a four-letter ICAO" : icao.Length != 4 || !icao.All(c => c is >= 'A' and <= 'Z')
        ? "Use four letters, for example EGXC" : "ICAO not in the active VISTA airfield catalogue");
    private bool MissionMatches(Mission mission) => mission.Active && Departure is not null && Arrival is not null
        && mission.DepartureBaseId == Departure.Id && mission.ArrivalBaseId == Arrival.Id
        && (SelectedAircraft is null || (mission.AircraftTypeId is null || mission.AircraftTypeId == SelectedAircraft.Aircraft.AircraftTypeId)
            && (mission.SquadronId is null || mission.SquadronId == SelectedAircraft.Aircraft.SquadronId));
    private void UpdateMissionMatches()
    {
        if (refreshing) return;
        var selection = SelectedMission;
        var matches = Missions.Where(MissionMatches).OrderBy(m => m.Title).ToList();
        refreshing = true;
        try
        {
            Replace(MatchingMissions, matches);
            SelectedMission = matches.FirstOrDefault(m => m.Id == selection?.Id);
        }
        finally { refreshing = false; }
        if (selection is not null && SelectedMission is null)
        {
            if (IsPredefined) { IsPredefined = false; Message = "Airport or aircraft changed. Choose a matching mission route again."; }
        }
        Notify(nameof(MissionMatchSummary)); Notify(nameof(CanUseMission)); CommandManager.InvalidateRequerySuggested();
    }
    private async Task PreviewMissionAsync(Mission? mission)
    {
        var version = ++previewVersion; MissionPreview.Clear();
        if (mission is null) { MissionPreviewStatus = "Choose a matching mission to preview its route."; return; }
        MissionPreviewStatus = "Loading ordered mission waypoints…";
        try
        {
            var points = await api.LoadMissionWaypointsAsync(mission.Id);
            if (version != previewVersion) return;
            Replace(MissionPreview, points.OrderBy(p => p.Position));
            MissionPreviewStatus = points.Count == 0 ? "This mission has no waypoints yet. Ask operations to add its route."
                : $"{points.Count} ordered waypoints · revision {mission.Revision} · load to use this route";
        }
        catch (Exception error) when (error is VistaApiException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { if (version == previewVersion) MissionPreviewStatus = "Could not preview this route. Select the mission again or refresh and retry."; }
    }

    public void AttachSimulator(SimulatorConnection connection)
    {
        simulator = connection;
        connection.StatusChanged += status => { SimulatorStatus = status; NotifyFlight(); };
        connection.Interrupted += () =>
        {
            telemetry = null; preflightEngines?.Interrupted(); testTracker?.ConnectionInterrupted(); liveTracker?.ConnectionInterrupted(); NotifyTelemetry(); NotifyFlight();
            if (testTracker is not null) TrackingSummary = "Tracking paused · reconnect to continue the local test";
        };
        connection.TelemetryReceived += ProcessSimulatorTelemetry;
    }
    public void ProcessSimulatorTelemetry(SimulatorTelemetry sample)
    {
            telemetry = sample; SimulatorStatus = "MSFS connected · telemetry live"; NotifyTelemetry();
            try { if(liveTracker is null&&activeSortie is not null)preflightEngines?.Accept(sample); AcceptLiveSample(sample); UpdateProgress(sample); NotifyFlight(); }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException) { UploadStatus = "Local recovery could not be saved: " + error.Message; }
            if (testTracker is null) return;
            testTracker.Accept(sample);
            // Same five-second recording cadence as BM; only fresh samples are recorded.
            if (lastTrackAt is null || sample.CapturedAt - lastTrackAt >= TimeSpan.FromSeconds(5))
            {
                LocalTrackPoints.Add(sample); lastTrackAt = sample.CapturedAt;
                if (LocalTrackPoints.Count > 1800) LocalTrackPoints.RemoveAt(0);
            }
            TrackingSummary = $"Local test · {testTracker.Takeoffs} takeoffs / {testTracker.Landings} landings / {LocalTrackPoints.Count} retained points";
    }
    private void NotifyTelemetry()
    {
        Notify(nameof(SimulatorAircraft)); Notify(nameof(SimulatorPosition)); Notify(nameof(SimulatorMetrics)); Notify(nameof(SimulatorGround)); Notify(nameof(ConnectionStatusTone)); Notify(nameof(FlightStatusTone));
        CommandManager.InvalidateRequerySuggested();
    }
    private void StartTrackingTest()
    {
        LocalTrackPoints.Clear(); TrackingEvents.Clear(); lastTrackAt = null;
        testTracker = new FlightTracker();
        testTracker.FlightEvent += text =>
        {
            TrackingEvents.Insert(0, $"{telemetry?.CapturedAt.LocalDateTime:HH:mm:ss} · {text}");
            if (TrackingEvents.Count > 200) TrackingEvents.RemoveAt(TrackingEvents.Count - 1);
        };
        TrackingSummary = "Local tracking test running · waiting for fresh telemetry";
    }
    private void StopTrackingTest()
    { testTracker = null; TrackingSummary = $"Local test stopped · {LocalTrackPoints.Count} retained points"; }

    public Task SignInAsync(string password) => RunAsync(async () =>
    {
        var signedPilot = await api.SignInAsync(Identifier, password);
        try
        {
            pilot = signedPilot;
            await RefreshAsync();
            NewPlan(); SelectedTab = 0;
            NotifyIdentity();
            api.RememberCurrentLogin(Identifier, RememberLogin);
            Message = "Signed in. Your fleet and sortie drafts are ready.";
        }
        catch { await api.SignOutAsync(); ClearSession(); throw; }
    });
    public Task RestoreLoginAsync() => RunAsync(async () =>
    {
        var saved = api.ReadRememberedLogin();
        if (saved is null) { Message = "Enter your wing account to sign in."; return; }
        Identifier = saved.Identifier; RememberLogin = true;
        try
        {
            pilot = await api.RestoreLoginAsync();
            await RefreshAsync(); NewPlan(); SelectedTab = 0; NotifyIdentity();
            Message = "Welcome back. Your remembered login was restored.";
        }
        catch { ClearSession(); throw; }
    });
    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Message = "Working…";
        try { await action(); }
        catch (Exception error) when (error is VistaApiException or HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException or System.IO.IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        { Message = error is TaskCanceledException ? "The request timed out. Your plan is still here; try again." : error.Message; }
        finally { IsBusy = false; }
    }
    private async Task RefreshAsync()
    {
        if (pilot is null) return;
        var next = await api.LoadCatalogueAsync();
        var flights = await api.LoadSortiesAsync(pilot.Id);
        var stats = await api.LoadStatisticsAsync(pilot.Id);
        var archives = await api.LoadMissionArchivesAsync();
        var reservations=await api.LoadFleetReservationsAsync();
        var missionData = await api.LoadMissionCatalogueAsync();
        var departureId = Departure?.Id; var arrivalId = Arrival?.Id; var alternateId = Alternate?.Id;
        var sortieId = SelectedSortie?.Id; var missionId = SelectedMission?.Id; var elementId = SelectedElement?.Id; var filterBaseId = FleetBase?.Id;
        refreshing = true;
        try
        {
        catalogue = next; statistics = stats;
        Replace(Bases, next.Bases);
        Replace(Fleet, next.Aircraft.Select(a => new FleetAircraft(a,
            next.Types.FirstOrDefault(t => t.Id == a.AircraftTypeId)?.Name ?? "Inactive type",
            next.Bases.FirstOrDefault(b => b.Id == a.HomeBaseId)?.Name ?? "Inactive base",
            next.Squadrons.FirstOrDefault(s => s.Id == a.SquadronId)?.Name ?? "Unassigned")));
        var selectedAircraftId = SelectedAircraft?.Id;
        ApplyFleetReservations(reservations);
        Replace(AvailableAircraft, Fleet.Where(f => f.Status == "available" && f.Reservation is null && next.Types.Any(t => t.Id == f.Aircraft.AircraftTypeId)
            && !flights.Any(s => s.AircraftId == f.Id && s.Status is "briefed" or "airborne")));
        SelectedAircraft = AvailableAircraft.FirstOrDefault(a => a.Id == selectedAircraftId);
        Replace(Missions, next.Missions); Replace(Elements, next.Elements); Replace(Sorties, flights.Select(s=>s with {SquadronLabel=Fleet.FirstOrDefault(a=>a.Id==s.AircraftId)?.Unit??"Unassigned"})); SelectedSortie = Sorties.FirstOrDefault(s => s.Id == sortieId);
        Departure = Bases.FirstOrDefault(b => b.Id == departureId); Arrival = Bases.FirstOrDefault(b => b.Id == arrivalId);
        Alternate = Bases.FirstOrDefault(b => b.Id == alternateId); FleetBase = Bases.FirstOrDefault(b => b.Id == filterBaseId);
        SelectedMission = Missions.FirstOrDefault(m => m.Id == missionId); SelectedElement = Elements.FirstOrDefault(e => e.Id == elementId);
        }
        finally { refreshing = false; }
        // Resolve typed ICAOs again after a catalogue refresh, preserving unrecognised input.
        refreshing = true;
        try { Departure = ResolveAirfield(DepartureIcao); Arrival = ResolveAirfield(ArrivalIcao); Alternate = ResolveAirfield(AlternateIcao); }
        finally { refreshing = false; }
        UpdateMissionMatches();
        SetMissionCatalogue(missionData);
        await SyncActiveMissionAsync();
        RebuildLibrary(archives);
        OperationMission=OperationPlans.FirstOrDefault(s=>s.Id==OperationMission?.Id)??activeSortie??OperationPlans.FirstOrDefault();NotifyPreparation();
        Notify(nameof(FleetCount)); Notify(nameof(DraftCount)); Notify(nameof(MissionCount)); Notify(nameof(CompletedSorties)); Notify(nameof(FlightHours));
        Message = "VISTA data refreshed.";
    }
    private void NewPlan()
    {
        ResetMissionBuilder(); PlannerStage=0;
        draftId = Guid.NewGuid(); plannedDepartureAt = null; IsPredefined = false; SelectedMission = null;
        PlanTitle = ""; FlightCallsign = pilot?.Callsign ?? ""; RouteText = "";
        SelectedAircraft = null; Departure = null; Arrival = null; Alternate = null;
        DepartureIcao = ""; ArrivalIcao = ""; AlternateIcao = "";
        Waypoints.Clear(); SelectedElement = null;MarkPlannerSaved(); SelectedTab = 1; Message = "New flight plan.";
    }
    private async Task SaveAsync()
    {
        if (catalogueDraft) throw new InvalidOperationException("Use Save mission plan to preserve this draft's tasks and load selections.");
        if (SelectedAircraft is null || string.IsNullOrWhiteSpace(PlanTitle) || string.IsNullOrWhiteSpace(FlightCallsign))
            throw new InvalidOperationException("Choose an aircraft and enter a title and flight callsign.");
        if (Departure is null || Arrival is null) throw new InvalidOperationException("Choose departure and arrival airfields.");
        if (!string.IsNullOrEmpty(AlternateIcao) && Alternate is null) throw new InvalidOperationException("Recognise the alternate ICAO or clear it before saving.");
        if (IsPredefined && SelectedMission is null) throw new InvalidOperationException("Choose a mission and load its route.");
        if (IsPredefined && !MissionMatches(SelectedMission!)) throw new InvalidOperationException("This mission no longer matches your airports or aircraft.");
        foreach (var p in Waypoints)
            if (string.IsNullOrWhiteSpace(p.Identifier) || p.Latitude is < -90 or > 90 || p.Longitude is < -180 or > 180
                || p.AltitudeFt is < -2000 or > 100000 || p.SpeedKts is < 0 or > 3000)
                throw new InvalidOperationException($"Check coordinates, altitude and speed at route point {p.Position}.");
        var plan = new SortiePlan(IsPredefined ? "predefined" : "custom", PlanTitle.Trim(), SelectedAircraft.Id,
            FlightCallsign.Trim(), Departure.Id, Arrival.Id, Alternate?.Id, IsPredefined ? SelectedMission?.Id : null,
            RouteText.Trim(), plannedDepartureAt, Waypoints.Select(p => p.ToPlan()).ToList());
        await api.SavePlanAsync(plan, draftId);MarkPlannerSaved();
        await RefreshAsync();SelectedSortie=Sorties.FirstOrDefault(s=>s.Id==draftId);OperationMission=SelectedSortie; Message = "Flight plan saved. Open SimBrief to generate its briefing.";
    }
    private async Task UseMissionAsync()
    {
        var mission = SelectedMission ?? throw new InvalidOperationException("Choose a mission.");
        if (SelectedAircraft is null || !MissionMatches(mission)) throw new InvalidOperationException("Choose a compatible aircraft and recognised mission airports.");
        var route = await api.LoadMissionWaypointsAsync(mission.Id);
        if (route.Count == 0) throw new InvalidOperationException("This mission has no route yet. Ask operations to add its waypoints.");
        IsPredefined = true;
        PlanTitle = mission.Title; Departure = Bases.FirstOrDefault(b => b.Id == mission.DepartureBaseId);
        Arrival = Bases.FirstOrDefault(b => b.Id == mission.ArrivalBaseId);
        Replace(Waypoints, route.Select(w => WaypointRow.FromStored(w))); Message = "Mission route loaded.";
    }
    private async Task AppendElementAsync()
    {
        var element = SelectedElement ?? throw new InvalidOperationException("Choose a mission element.");
        var route = await api.LoadElementWaypointsAsync(element.Id);
        if (route.Count == 0) throw new InvalidOperationException("This element has no waypoints yet.");
        if (route.Count + Waypoints.Count > 300) throw new InvalidOperationException("A route can contain at most 300 points.");
        var instance = Guid.NewGuid();
        foreach (var w in route) Waypoints.Add(WaypointRow.FromStored(w, true, instance));
        Message = "Mission element appended. Editing a point turns it into an independent custom point.";
    }
    private async Task EditDraftAsync()
    {
        var s = SelectedSortie ?? throw new InvalidOperationException("Choose a draft.");
        var route = await api.LoadSortieWaypointsAsync(s.Id);
        refreshing = true;
        try {
        IsPredefined = s.Source == "predefined"; SelectedMission = Missions.FirstOrDefault(m => m.Id == s.MissionId);
        draftId = s.Id; plannedDepartureAt = s.PlannedDepartureAt; PlanTitle = s.Title; FlightCallsign = s.Callsign; RouteText = s.RouteText;
        SelectedAircraft = AvailableAircraft.FirstOrDefault(a => a.Id == s.AircraftId);
        Departure = Bases.FirstOrDefault(b => b.Id == s.DepartureBaseId);
        Arrival = Bases.FirstOrDefault(b => b.Id == s.ArrivalBaseId);
        Alternate = Bases.FirstOrDefault(b => b.Id == s.AlternateBaseId);
        DepartureIcao = Departure?.Icao ?? ""; ArrivalIcao = Arrival?.Icao ?? ""; AlternateIcao = Alternate?.Icao ?? "";
        Replace(Waypoints, route.Select(w => WaypointRow.FromStored(w))); SelectedTab = 1;
        } finally { refreshing = false; }
        UpdateMissionMatches();
        RestoreMissionBuilder(s);PlannerStage=0;MarkPlannerSaved();
        if (!catalogueDraft) Message = "Draft loaded. Saving a predefined draft copies the mission's current revision.";
    }
    private async Task DeleteDraftAsync()
    {
        if (SelectedSortie is null) return;
        var id = SelectedSortie.Id;
        await api.DeleteDraftAsync(id);
        if (draftId == id) NewPlan();
        var deleted = Sorties.FirstOrDefault(s => s.Id == id);
        if (deleted is not null) Sorties.Remove(deleted);
        SelectedSortie = null; SelectedTab = 2; Notify(nameof(DraftCount));
        await RefreshAsync(); Message = "Mission deleted from Saved Missions.";
    }
    private void MovePoint(int delta)
    {
        if (SelectedWaypoint is null) return;
        var index = Waypoints.IndexOf(SelectedWaypoint); Waypoints.Move(index, index + delta);
    }
    private void Renumber() { for (var i = 0; i < Waypoints.Count; i++) Waypoints[i].Position = i + 1; }
    private void ClearSession()
    {
        PersistRecovery(); ClearLiveTracking(false); SimBriefPilotId="";
        StopTrackingTest(); simulator?.Disconnect(); LocalTrackPoints.Clear(); TrackingEvents.Clear();
        pilot = null; catalogue = null; missionData = null;
        BuilderFamilies.Clear(); BuilderOptions.Clear(); BuilderPayloads.Clear(); CatalogueControls.Clear(); SelectedControl = null;
        Bases.Clear(); Fleet.Clear(); AvailableAircraft.Clear(); Missions.Clear(); Elements.Clear(); Waypoints.Clear(); Sorties.Clear(); MissionCards.Clear(); SharedMissions.Clear(); SelectedSharedMission=null;
        MatchingMissions.Clear(); SelectedMission = null;
        NewPlan();OperationMission=null;MissionSearch="";FleetSelection=null;FleetUnit="All squadrons";PreparationError=""; NotifyIdentity();
    }
    private void NotifyIdentity() { LoadPilotSettings(); Notify(nameof(IsSignedIn)); Notify(nameof(IsLoginVisible)); Notify(nameof(PilotLabel)); Notify(nameof(PilotRole)); Notify(nameof(CanManageCatalogue)); }
    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    { var rows = values.ToList(); collection.Clear(); foreach (var row in rows) collection.Add(row); }
}
