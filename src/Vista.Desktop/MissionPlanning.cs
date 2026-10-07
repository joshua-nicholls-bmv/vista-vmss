using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using Vista.Core;

namespace Vista.Desktop;

public sealed record MissionTaskRow(CatalogueOption Option, MissionFamily Family, PayloadPreset? Payload)
{
    public Guid RowId { get; } = Guid.NewGuid();
    public string Task => Family.Title;
    public string Location => Option.Title;
    public string Load => Payload?.Label ?? "No payload prescribed";
}
public sealed record RoutePreviewRow(int Position, string Segment, string Identifier, decimal Latitude,
    decimal Longitude, string Altitude, string Speed);
public sealed record CatalogueControl(string Kind, string Code, string Title, bool Enabled)
{
    public string Category => Kind switch { "family" => "Mission type", "option" => "Task / destination", "payload" => "Load preset", _ => "Route element" };
}

public sealed partial class MainViewModel
{
    private MissionCatalogue? missionData;
    private MissionFamily? builderFamily;
    private CatalogueOption? builderOption;
    private PayloadPreset? builderPayload;
    private MissionTaskRow? builderTask;
    private CatalogueControl? selectedControl;
    private bool corridorOutbound, corridorReturn, catalogueDraft, updatingBuilder;
    private string builderNotice = "Choose an aircraft, then select a mission type.", payloadKind = "All loads";
    public ObservableCollection<MissionFamily> BuilderFamilies { get; } = [];
    public ObservableCollection<CatalogueOption> BuilderOptions { get; } = [];
    public ObservableCollection<PayloadPreset> BuilderPayloads { get; } = [];
    public ObservableCollection<MissionTaskRow> BuilderTasks { get; } = [];
    public ObservableCollection<RoutePreviewRow> BuilderRoute { get; } = [];
    public ObservableCollection<CatalogueControl> CatalogueControls { get; } = [];
    public string[] PayloadKinds { get; } = ["All loads", "Personnel", "Cargo"];
    public bool CanManageCatalogue => pilot?.Role == "admin";
    public string BuilderNotice { get => builderNotice; private set => Set(ref builderNotice, value); }
    public string PayloadKind { get => payloadKind; set { Set(ref payloadKind, value); UpdateBuilderLoads(); } }
    public string PayloadDescription => BuilderPayload?.Description ?? "Select a load if this mission requires one.";
    public string BuilderTotals => $"{BuilderTasks.Sum(t => t.Payload?.PayloadKg ?? 0):N0} kg · {BuilderTasks.Sum(t => t.Payload?.PersonnelCount ?? 0)} personnel";
    public string BuilderRouteLabel => $"{Departure?.Icao ?? "----"} → {string.Join(" → ", BuilderTasks.Where(t=>t.Option.ElementId is not null).Select(t => t.Option.Title))} → {Arrival?.Icao ?? "----"}";
    public bool CorridorOutbound { get => corridorOutbound; set { Set(ref corridorOutbound, value); CompileBuilderPreview(); } }
    public bool CorridorReturn { get => corridorReturn; set { Set(ref corridorReturn, value); CompileBuilderPreview(); } }
    public MissionFamily? BuilderFamily
    {
        get => builderFamily;
        set
        {
            if (!Set(ref builderFamily, value)) return;
            if (!updatingBuilder && value is not null && ArrivalSelection=="Choose airport")
            {
                if (Departure is null || !value.DepartureIcaos.Contains(Departure.Icao))
                    DepartureIcao = value.DepartureIcaos.Contains(SelectedAircraft?.Aircraft.HomeBaseId is Guid id ? Bases.FirstOrDefault(b => b.Id == id)?.Icao : "")
                        ? Bases.First(b => b.Id == SelectedAircraft!.Aircraft.HomeBaseId).Icao : value.DepartureIcaos.FirstOrDefault() ?? "";
                if (value.ReturnIcaos.Length > 0 && (Arrival is null || !value.ReturnIcaos.Contains(Arrival.Icao))) ArrivalIcao = DepartureIcao;
            }
            UpdateBuilderOptions();NotifyPlannerEase();
        }
    }
    public CatalogueOption? BuilderOption
    {
        get => builderOption;
        set
        {
            if (!Set(ref builderOption, value)) return;
            if (!updatingBuilder && ArrivalSelection=="Choose airport" && value?.DestinationBaseId is Guid id) Arrival = Bases.FirstOrDefault(b => b.Id == id);
            UpdateBuilderLoads();
        }
    }
    public PayloadPreset? BuilderPayload { get => builderPayload; set { Set(ref builderPayload, value); Notify(nameof(PayloadDescription)); } }
    public MissionTaskRow? BuilderTask { get => builderTask; set => Set(ref builderTask, value); }
    public CatalogueControl? SelectedControl { get => selectedControl; set { Set(ref selectedControl, value); Notify(nameof(CatalogueToggleLabel)); } }
    public string CatalogueToggleLabel => SelectedControl is null ? "Select an item" : SelectedControl.Enabled ? "Disable selected item" : "Enable selected item";
    public ICommand RandomOptionCommand { get; private set; } = null!;
    public ICommand RandomiseAllCommand {get;private set;}=null!;
    public ICommand RandomPayloadCommand { get; private set; } = null!;
    public ICommand AddMissionTaskCommand { get; private set; } = null!;
    public ICommand RemoveMissionTaskCommand { get; private set; } = null!;
    public ICommand MoveMissionUpCommand { get; private set; } = null!;
    public ICommand MoveMissionDownCommand { get; private set; } = null!;
    public ICommand RerollMissionTaskCommand { get; private set; } = null!;
    public ICommand SaveMissionPlanCommand { get; private set; } = null!;
    public ICommand ToggleCatalogueCommand { get; private set; } = null!;

    private void InitializeMissionBuilder()
    {
        InitializePlannerEase();InitializeSharing();
        GenerateMissionCommand=new AsyncCommand(()=>RunAsync(()=>{GenerateMission();return Task.CompletedTask;}),()=>!IsBusy&&SelectedAircraft is not null&&Departure is not null&&(BuilderFamily is not null||BuilderTasks.Count>0));
        RandomOptionCommand = new RelayCommand(() => BuilderOption = ChooseDifferent(BuilderOptions, BuilderOption), () => !IsBusy && BuilderOptions.Count > 0);
        RandomiseAllCommand=new RelayCommand(()=>{BuilderOption=ChooseDifferent(BuilderOptions,BuilderOption);BuilderPayload=ChooseDifferent(BuilderPayloads,BuilderPayload);},()=>!IsBusy&&BuilderOptions.Count>0);
        RandomPayloadCommand = new RelayCommand(() => BuilderPayload = ChooseDifferent(BuilderPayloads, BuilderPayload), () => !IsBusy && BuilderPayloads.Count > 0);
        AddMissionTaskCommand = new AsyncCommand(() => RunAsync(() => { AddBuilderTask(); return Task.CompletedTask; }), () => !IsBusy && BuilderOption is not null && BuilderTasks.Count < 12);
        RemoveMissionTaskCommand = new RelayCommand(() => { if (BuilderTask is not null) { BuilderTasks.Remove(BuilderTask); BuilderTask = null; } }, () => !IsBusy && BuilderTask is not null);
        MoveMissionUpCommand = new RelayCommand(() => MoveBuilderTask(-1), () => !IsBusy && BuilderTask is not null && BuilderTasks.IndexOf(BuilderTask) > 0);
        MoveMissionDownCommand = new RelayCommand(() => MoveBuilderTask(1), () => !IsBusy && BuilderTask is not null && BuilderTasks.IndexOf(BuilderTask) < BuilderTasks.Count - 1);
        RerollMissionTaskCommand = new AsyncCommand(() => RunAsync(() => { RerollBuilderTask(); return Task.CompletedTask; }), () => !IsBusy && BuilderTask is not null);
        SaveMissionPlanCommand = new AsyncCommand(() => RunAsync(SaveMissionPlanAsync), () => IsSignedIn && !IsBusy && BuilderTasks.Count > 0);
        ToggleCatalogueCommand = new AsyncCommand(() => RunAsync(async () =>
        {
            var selected = SelectedControl ?? throw new InvalidOperationException("Choose a catalogue item.");
            await api.SetCatalogueEnabledAsync(selected.Kind, selected.Code, !selected.Enabled);
            await RefreshAsync(); Message = "Catalogue switch saved. Existing sortie snapshots are unchanged.";
        }), () => CanManageCatalogue && !IsBusy && SelectedControl is not null);
        BuilderTasks.CollectionChanged += (_, _) => CompileBuilderPreview();
    }
    private static T? ChooseDifferent<T>(IEnumerable<T> choices, T? current) where T : class
    {
        var values = choices.Where(v => !Equals(v, current)).ToList();
        if (values.Count == 0) return current ?? choices.FirstOrDefault();
        return values[System.Security.Cryptography.RandomNumberGenerator.GetInt32(values.Count)];
    }
    private void SetMissionCatalogue(MissionCatalogue value)
    {
        missionData = value; UpdateBuilderChoices();
        var selected = SelectedControl;
        Replace(CatalogueControls, value.Families.Select(f => new CatalogueControl("family", f.Code, f.Title, f.Enabled))
            .Concat(value.Options.Select(o => new CatalogueControl("option", o.Code, o.Title, o.Enabled)))
            .Concat(value.Payloads.Select(p => new CatalogueControl("payload", p.Code, p.Label, p.Enabled)))
            .Concat(value.AllElements.Where(e => e.Code.StartsWith("VISTA-")).Select(e => new CatalogueControl("element", e.Code, e.Title, e.Active)))
            .OrderBy(c => c.Kind).ThenBy(c => c.Title));
        SelectedControl = CatalogueControls.FirstOrDefault(c => selected is not null && c.Kind == selected.Kind && c.Code == selected.Code);
        Notify(nameof(CanManageCatalogue)); CompileBuilderPreview();
    }
    private void UpdateBuilderChoices()
    {
        if (updatingBuilder || refreshing || missionData is null) return;
        var optionId = BuilderOption?.Id; var payloadId = BuilderPayload?.Id;
        updatingBuilder = true;
        try
        {
            var code = BuilderFamily?.Code;
            Replace(BuilderFamilies, missionData.Families.Where(f => f.Enabled && SelectedAircraft is not null
                && missionData.AircraftTypes.Any(t => t.FamilyCode == f.Code && t.AircraftTypeId == SelectedAircraft.Aircraft.AircraftTypeId)));
            BuilderFamily = BuilderFamilies.FirstOrDefault(f => f.Code == code);
        }
        finally { updatingBuilder = false; }
        UpdateBuilderOptions();
        BuilderOption = BuilderOptions.FirstOrDefault(o => o.Id == optionId);
        BuilderPayload = BuilderPayloads.FirstOrDefault(p => p.Id == payloadId);
        CompileBuilderPreview();
    }
    private void UpdateBuilderOptions()
    {
        if (missionData is null) return;
        var id = BuilderOption?.Id; var payloadId = BuilderPayload?.Id;
        var candidates = missionData.Options.Where(o => o.Enabled && o.FamilyCode == BuilderFamily?.Code
            && (o.ElementId is null || Elements.Any(e => e.Id == o.ElementId && e.Active))
            && (o.DestinationBaseId is null || Bases.Any(b => b.Id == o.DestinationBaseId && b.Active))).ToList();
        Replace(BuilderOptions, candidates); BuilderOption = candidates.FirstOrDefault(o => o.Id == id);
        UpdateBuilderLoads(); BuilderPayload = BuilderPayloads.FirstOrDefault(p => p.Id == payloadId);
    }
    private void UpdateBuilderLoads()
    {
        if (missionData is null) return;
        var id = BuilderPayload?.Id;
        Replace(BuilderPayloads, missionData.Payloads.Where(p => p.Enabled
            && missionData.FamilyPayloads.Any(f => f.FamilyCode == BuilderFamily?.Code && f.PayloadId == p.Id)
            && (BuilderOption?.DestinationBaseId is not Guid destination || !missionData.PayloadDestinations.Any(d => d.PayloadId == p.Id)
                || missionData.PayloadDestinations.Any(d => d.PayloadId == p.Id && d.BaseId == destination))
            && (PayloadKind == "All loads" || PayloadKind == "Personnel" && p.Kind == "personnel" || PayloadKind == "Cargo" && p.Kind is "cargo" or "mixed")));
        BuilderPayload = BuilderPayloads.FirstOrDefault(p => p.Id == id);
    }
    private void AddBuilderTask()
    {
        if (missionData is null || BuilderFamily is null || BuilderOption is null) throw new InvalidOperationException("Choose a mission type and option.");
        if (BuilderOption.DestinationBaseId is not null && BuilderTasks.Any(t=>t.Option.DestinationBaseId is not null))
            throw new InvalidOperationException("Use one destination task with any compatible training tasks.");
        if (ArrivalSelection=="Return to departure" && BuilderOption.DestinationBaseId is Guid destination && destination!=Departure?.Id)
            throw new InvalidOperationException("Select Assign from mission for this destination task.");
        if (missionData.FamilyPayloads.Any(f => f.FamilyCode == BuilderFamily.Code) && BuilderPayload is null)
            throw new InvalidOperationException("Choose or randomise a compatible load before adding this task.");
        BuilderTasks.Add(new(BuilderOption, BuilderFamily, BuilderPayload));
        if (string.IsNullOrWhiteSpace(PlanTitle)) PlanTitle = (BuilderFamily.Title + " — " + BuilderOption.Title)[..Math.Min(120, (BuilderFamily.Title + " — " + BuilderOption.Title).Length)];
        Message = $"{BuilderFamily.Title} at {BuilderOption.Title} added.";
    }
    private void MoveBuilderTask(int offset)
    {
        if (BuilderTask is null) return;
        var index = BuilderTasks.IndexOf(BuilderTask); BuilderTasks.Move(index, index + offset);
    }
    private void RerollBuilderTask()
    {
        var old = BuilderTask ?? throw new InvalidOperationException("Select a task to reroll.");
        BuilderFamily = BuilderFamilies.FirstOrDefault(f => f.Code == old.Family.Code) ?? throw new InvalidOperationException("This task is no longer available for your aircraft.");
        BuilderOption = ChooseDifferent(BuilderOptions, old.Option) ?? throw new InvalidOperationException("No enabled options remain.");
        BuilderPayload = ChooseDifferent(BuilderPayloads, old.Payload);
        if (missionData!.FamilyPayloads.Any(f => f.FamilyCode == BuilderFamily.Code) && BuilderPayload is null) throw new InvalidOperationException("No enabled compatible loads remain.");
        var index = BuilderTasks.IndexOf(old); var next = new MissionTaskRow(BuilderOption, BuilderFamily, BuilderPayload);
        BuilderTasks[index] = next; BuilderTask = next;
    }
    private string? ValidateBuilder()
    {
        if (SelectedAircraft is null) return "Choose an aircraft.";
        if (Departure is null) return "Enter a recognised departure ICAO.";
        if (Arrival is null) return ArrivalSelection=="Assign from mission"?"Destination awaiting mission assignment. Choose a mission type and Add task.":"Enter recognised departure and arrival ICAOs.";
        if (!string.IsNullOrEmpty(AlternateIcao) && Alternate is null) return "Recognise the alternate ICAO or clear it.";
        if (BuilderTasks.Count == 0) return "Choose a mission type and add a task.";
        if (missionData is null) return "Refresh the mission catalogue.";
        foreach (var task in BuilderTasks)
        {
            var family = missionData.Families.FirstOrDefault(f => f.Code == task.Family.Code && f.Enabled);
            var option = missionData.Options.FirstOrDefault(o => o.Id == task.Option.Id && o.Enabled);
            if (family is null || option is null) return "A selected task is disabled. Remove or replace it.";
            if (!missionData.AircraftTypes.Any(t => t.FamilyCode == family.Code && t.AircraftTypeId == SelectedAircraft.Aircraft.AircraftTypeId)) return "Your aircraft does not support a selected task.";
            if (!family.DepartureIcaos.Contains(Departure.Icao)) return "Departure does not support a selected task.";
            if (option.DestinationBaseId is Guid destination)
            { if (BuilderTasks.Count(t=>t.Option.DestinationBaseId is not null)!=1 || Arrival.Id != destination) return "Use one destination task and its assigned arrival."; }
            else if (!BuilderTasks.Any(t=>t.Option.DestinationBaseId is not null) && !family.ReturnIcaos.Contains(Arrival.Icao)) return "Arrival does not support a selected training task.";
            if (option.ElementId is Guid element && !Elements.Any(e => e.Id == element && e.Active)) return "A selected route element is disabled.";
            if (missionData.FamilyPayloads.Any(f => f.FamilyCode == family.Code))
            {
                var payload = missionData.Payloads.FirstOrDefault(p => p.Id == task.Payload?.Id && p.Enabled);
                if (payload is null || !missionData.FamilyPayloads.Any(f => f.FamilyCode == family.Code && f.PayloadId == payload.Id)) return "A selected load is disabled or incompatible.";
                if (missionData.PayloadDestinations.Any(d => d.PayloadId == payload.Id) && !missionData.PayloadDestinations.Any(d => d.PayloadId == payload.Id && d.BaseId == Arrival.Id)) return "Load does not match the arrival.";
            }
        }
        return null;
    }
    private void CompileBuilderPreview()
    {
        ResolveMissionArrival();
        BuilderRoute.Clear();
        var routes = new List<(Guid Id, string Label)>();
        void Corridor(string code, string label, bool selected)
        {
            if (!selected) return;
            var element = Elements.FirstOrDefault(e => e.Code == code && e.Active);
            if (element is not null) routes.Add((element.Id, label));
        }
        Corridor("VISTA-LICHFIELD-OUT", "Corridor outbound", CorridorOutbound);
        routes.AddRange(BuilderTasks.Where(t => t.Option.ElementId is not null).Select(t => (t.Option.ElementId!.Value, t.Option.Title)));
        Corridor("VISTA-LICHFIELD-RETURN", "Corridor return", CorridorReturn);
        foreach (var route in routes)
            foreach (var point in (missionData?.ElementWaypoints ?? []).Where(p => p.MissionElementId == route.Id).OrderBy(p => p.Position))
                BuilderRoute.Add(new(BuilderRoute.Count + 1, route.Label, point.Identifier, point.Latitude, point.Longitude,
                    point.Instructions.Contains("FL140") ? "FL140" : point.AltitudeFt is int altitude ? $"{altitude:N0} ft" : "Discretion",
                    point.SpeedKts is int speed ? $"{speed} kt" : "Discretion"));
        BuilderNotice = ValidateBuilder() ?? (BuilderRoute.Count == 0 ? "Direct airport-to-airport plan · ready to save." : $"{BuilderTasks.Count} task(s) · {BuilderRoute.Count} ordered waypoints · ready to save.");
        if (CorridorOutbound && !Elements.Any(e => e.Code == "VISTA-LICHFIELD-OUT" && e.Active)
            || CorridorReturn && !Elements.Any(e => e.Code == "VISTA-LICHFIELD-RETURN" && e.Active)) BuilderNotice = "A selected corridor transit is disabled.";
        NotifyPlannerEase();
        Notify(nameof(BuilderTotals)); Notify(nameof(BuilderRouteLabel)); CommandManager.InvalidateRequerySuggested();
    }
    private async Task SaveMissionPlanAsync()
    {
        var error = ValidateBuilder(); if (error is not null) throw new InvalidOperationException(error);
        var plan = new CataloguePlan(PlanTitle.Trim(), FlightCallsign.Trim(), SelectedAircraft!.Id, Departure!.Id, Arrival!.Id,
            Alternate?.Id, CorridorOutbound, CorridorReturn, BuilderTasks.Select(t => new CatalogueTask(t.Option.Id, t.Payload?.Id)).ToList(), plannedDepartureAt);
        await api.SaveCataloguePlanAsync(plan, draftId); catalogueDraft = true;MarkPlannerSaved();
        await RefreshAsync();SelectedSortie=Sorties.FirstOrDefault(s=>s.Id==draftId);OperationMission=SelectedSortie; Message = "Mission plan saved with its chosen tasks, load and ordered route.";
    }
    private void ResetMissionBuilder()
    {
        ArrivalSelection="Choose airport";
        catalogueDraft = false; BuilderTasks.Clear(); BuilderTask = null; BuilderFamily = null; BuilderOption = null;
        BuilderPayload = null; CorridorOutbound = false; CorridorReturn = false;
    }
    private void RestoreMissionBuilder(Sortie sortie)
    {
        ResetMissionBuilder();
        if (sortie.PlanSnapshot.ValueKind != JsonValueKind.Object || !sortie.PlanSnapshot.TryGetProperty("planning_mode", out var mode) || mode.GetString() != "catalogue") return;
        if (!sortie.PlanSnapshot.TryGetProperty("catalogue_plan", out var saved) || missionData is null) return;
        catalogueDraft = true;
        foreach (var task in saved.GetProperty("tasks").EnumerateArray())
        {
            var option = missionData.Options.FirstOrDefault(o => o.Id == task.GetProperty("option_id").GetGuid());
            var family = missionData.Families.FirstOrDefault(f => f.Code == task.GetProperty("family_code").GetString());
            var payload = task.TryGetProperty("payload_id", out var p) && p.ValueKind == JsonValueKind.String ? missionData.Payloads.FirstOrDefault(v => v.Id == p.GetGuid()) : null;
            if (option is null || family is null) throw new InvalidOperationException("A saved catalogue task is missing. Your draft remains stored; ask operations to restore the catalogue item.");
            BuilderTasks.Add(new(option, family, payload));
        }
        CorridorOutbound = saved.GetProperty("corridor_outbound").GetBoolean(); CorridorReturn = saved.GetProperty("corridor_return").GetBoolean();
        CompileBuilderPreview(); Message = "Saved mission choices restored. Review before saving changes.";
    }
}
