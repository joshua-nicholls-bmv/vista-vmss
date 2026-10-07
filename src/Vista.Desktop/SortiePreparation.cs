using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;
using Vista.Core;
namespace Vista.Desktop;
public sealed partial class MainViewModel
{
    private SortieBriefingRecord? briefingRecord;
    private Sortie? briefingMission,operationMission;
    private string missionSearch="",missionNotes="",preparationError="",fleetUnit="All squadrons";
    private bool acknowledgementOpen,acknowledgementChecked;
    private int plannerStage;
    private FleetAircraft? fleetSelection;
    public event Action? BriefingRequested;
    public event Action? BriefingCloseRequested;
    public ICollectionView OperationMissions {get;private set;}=null!;
    public string MissionSearch {get=>missionSearch;set{Set(ref missionSearch,value);OperationMissions.Refresh();}}
    public Sortie? OperationMission {get=>operationMission;set{Set(ref operationMission,value);NotifyPreparation();}}
    public string OperationPreview=>OperationMission is null?"Choose a named saved mission to review and activate.":MissionSummary(OperationMission);
    public string OperationTasks=>TaskSummary(OperationMission);
    public string PreparationError {get=>preparationError;private set=>Set(ref preparationError,value);}
    public string PreparationSummary=>activeSortie is null?"Choose a mission to begin":IsTracking?liveTracker?.InTime is not null?"Parked · ready to finish flight":"Flight tracking in progress":!BriefingSigned?"Review and sign your briefing":!SimulatorIsConnected?"Connect the simulator to continue":!AircraftChecked?"Confirm your aircraft and mission":"Ready to start tracking";
    public bool IsTracking=>liveTracker is not null||activeSortie?.Status=="airborne";
    public bool IsPreparing=>!IsTracking;
    public bool ShowMissionSelection=>activeSortie is null;
    public bool BriefingCurrent=>briefingRecord?.SortieId==activeSortie?.Id&&briefingRecord?.IsCurrent==true;
    public bool BriefingSigned=>BriefingCurrent&&briefingRecord?.IsSigned==true&&!NotesDirty;
    public string BriefingState=>briefingRecord?.Document is null?"NOT IMPORTED":!briefingRecord.IsCurrent?"OUT OF DATE":briefingRecord.IsSigned&&!NotesDirty?"SIGNED":"REVIEW REQUIRED";
    public string BriefingSignature=>briefingRecord?.SignedAt is DateTimeOffset at?"Acknowledged by "+briefingRecord.SignedLabel+" · "+at.UtcDateTime.ToString("dd MMM yyyy HH:mm 'UTC'"):"Not yet acknowledged";
    public string BriefingTitle=>briefingMission?.Title??"Sortie briefing";
    public string BriefingOverview=>briefingMission is null?"":MissionSummary(briefingMission)+"\nPilot: "+PilotLabel;
    public ObservableCollection<StoredWaypoint> BriefingPoints {get;}=[];
    public Airfield? BriefingDeparture=>Bases.FirstOrDefault(b=>b.Id==briefingMission?.DepartureBaseId);
    public Airfield? BriefingArrival=>Bases.FirstOrDefault(b=>b.Id==briefingMission?.ArrivalBaseId);
    public string BriefingTasks=>TaskSummary(briefingMission);
    public string BriefingInstructions=>BuildTaskBriefings();
    public string MissionNotes {get=>missionNotes;set{Set(ref missionNotes,value);NotifyPreparation();}}
    public bool NotesDirty=>MissionNotes!=(briefingRecord?.MissionNotes??"");
    public bool CanEditBriefing=>briefingMission?.Status is "planned" or "briefed";
    public bool IsBriefingReadOnly=>!CanEditBriefing;
    public bool CanAcknowledge=>briefingMission?.Id==activeSortie?.Id&&activeSortie?.Status=="briefed"&&BriefingCurrent&&!NotesDirty;
    public bool AcknowledgementOpen {get=>acknowledgementOpen;set=>Set(ref acknowledgementOpen,value);}
    public bool AcknowledgementChecked {get=>acknowledgementChecked;set{Set(ref acknowledgementChecked,value);CommandManager.InvalidateRequerySuggested();}}
    public string BriefingFuelBreakdown=>briefing is null?"Import SimBrief to review the fuel plan.":$"Ramp  {Amount(briefing.RampFuelKg)}\nTaxi  {Amount(briefing.TaxiFuelKg)}\nTrip  {Amount(briefing.TripFuelKg)}\nContingency  {Amount(briefing.ContingencyFuelKg)}\nAlternate  {Amount(briefing.AlternateFuelKg)}\nReserve  {Amount(briefing.ReserveFuelKg)}\nExtra  {Amount(briefing.ExtraFuelKg)}";
    public string BriefingWeights=>briefing is null?"Not imported":$"Payload  {Amount(briefing.PayloadKg)}\nZero fuel  {Amount(briefing.ZeroFuelWeightKg)}\nTakeoff  {Amount(briefing.TakeoffWeightKg)}\nLanding  {Amount(briefing.LandingWeightKg)}";
    public string BriefingWeather=>briefing is null?"Import SimBrief to review available weather.":$"DEPARTURE · {briefing.Departure}\n{Missing(briefing.DepartureWeather)}\n\nARRIVAL · {briefing.Arrival}\n{Missing(briefing.ArrivalWeather)}\n\nALTERNATE · {Missing(briefing.Alternate)}\n{Missing(briefing.AlternateWeather)}";
    public string BriefingOperational=>briefing is null?"Not imported":$"Planned runways: {Missing(briefing.DepartureRunway)} / {Missing(briefing.ArrivalRunway)}\nEstimated block time: {(briefing.BlockSeconds is long t?TimeSpan.FromSeconds(t).ToString(@"hh\:mm"):"Not supplied")}\n{Missing(briefing.OperationalInformation)}";
    private static string Missing(string value)=>string.IsNullOrWhiteSpace(value)?"Not supplied":value;
    public ICommand ActivateAndBriefCommand {get;private set;}=null!;
    public ICommand ViewBriefingCommand {get;private set;}=null!;
    public ICommand ViewSelectedBriefingCommand {get;private set;}=null!;
    public ICommand SaveBriefingNotesCommand {get;private set;}=null!;
    public ICommand BeginAcknowledgementCommand {get;private set;}=null!;
    public ICommand SignAndCloseBriefingCommand {get;private set;}=null!;
    public ICommand CancelAcknowledgementCommand {get;private set;}=null!;
    public ICommand OpenOfpCommand {get;private set;}=null!;
    public ICommand SaveAndDispatchCommand {get;private set;}=null!;
    public int PlannerStage {get=>plannerStage;set{if(Set(ref plannerStage,Math.Clamp(value,0,2))){Notify(nameof(PlannerAircraftStage));Notify(nameof(PlannerTaskStage));Notify(nameof(PlannerReviewStage));}}}
    public bool PlannerAircraftStage=>PlannerStage==0;
    public bool PlannerTaskStage=>PlannerStage==1;
    public bool PlannerReviewStage=>PlannerStage==2;
    public ICommand PlannerAircraftCommand {get;private set;}=null!;
    public ICommand PlannerTasksCommand {get;private set;}=null!;
    public ICommand PlannerReviewCommand {get;private set;}=null!;
    public string LibraryTitle=>SelectedSortie?.Title??"Select a mission";
    public string LibraryDetails=>SelectedSortie is null?"":MissionSummary(SelectedSortie);
    public string LibraryTasks=>TaskSummary(SelectedSortie);
    public string[] FleetUnits=>new[]{"All squadrons"}.Concat(Fleet.Select(a=>a.Unit).Distinct().Order()).ToArray();
    public string FleetUnit {get=>fleetUnit;set{Set(ref fleetUnit,value);FleetView.Refresh();}}
    public FleetAircraft? FleetSelection {get=>fleetSelection;set{Set(ref fleetSelection,value);Notify(nameof(FleetDetails));Notify(nameof(FleetTitle));}}
    public string FleetTitle=>FleetSelection?.Serial??"Select an aircraft";
    public string FleetDetails=>FleetSelection is null?"Select a fleet aircraft to review its assignment.":$"{FleetSelection.Type}\n\n{FleetSelection.Unit}\nHome base: {FleetSelection.Base}\nCurrent base: {Bases.FirstOrDefault(b=>b.Id==FleetSelection.Aircraft.CurrentBaseId)?.Name??"Not supplied"}\nAvailability: {FleetSelection.Availability}\nAssigned pilot: {FleetSelection.AssignedPilot}\n{FleetSelection.Reservation?.Callsign} · {FleetSelection.Reservation?.MissionTitle}";
    private void InitializePreparation()
    {
        OperationMissions=new ListCollectionView(OperationPlans){Filter=v=>v is Sortie s&&(MissionSearch.Length==0||s.Title.Contains(MissionSearch,StringComparison.OrdinalIgnoreCase)||s.Callsign.Contains(MissionSearch,StringComparison.OrdinalIgnoreCase))};
        InitializeLibrary();
        ActivateAndBriefCommand=new AsyncCommand(()=>RunAsync(async()=>{SelectedSortie=OperationMission??throw new InvalidOperationException("Choose a saved mission.");await ActivateMissionAsync(false);}),()=>!IsBusy&&OperationMission is not null&&!IsTracking);
        ViewBriefingCommand=new AsyncCommand(()=>RunAsync(async()=>{await LoadBriefingForAsync(activeSortie??throw new InvalidOperationException("Activate a mission first."));BriefingRequested?.Invoke();}),()=>!IsBusy&&activeSortie is not null);
        ViewSelectedBriefingCommand=new AsyncCommand(()=>RunAsync(async()=>{await LoadBriefingForAsync(SelectedSortie??throw new InvalidOperationException("Select a saved mission."));BriefingRequested?.Invoke();}),()=>!IsBusy&&SelectedSortie is not null);
        SaveBriefingNotesCommand=new AsyncCommand(()=>RunAsync(async()=>{var mission=briefingMission??throw new InvalidOperationException("Open a briefing first.");var saved=await api.SaveBriefingNotesAsync(mission.Id,MissionNotes,briefingRecord?.Revision);ApplyBriefing(saved);Message="Mission briefing notes saved. A fresh acknowledgement is required.";}),()=>!IsBusy&&CanEditBriefing&&NotesDirty);
        BeginAcknowledgementCommand=new RelayCommand(()=>{AcknowledgementChecked=false;AcknowledgementOpen=true;},()=>!IsBusy&&CanAcknowledge);
        CancelAcknowledgementCommand=new RelayCommand(()=>AcknowledgementOpen=false);
        SignAndCloseBriefingCommand=new AsyncCommand(()=>RunAsync(async()=>{if(!CanAcknowledge||!AcknowledgementChecked)throw new InvalidOperationException("Review and acknowledge the current briefing first.");var saved=await api.SignBriefingAsync(briefingMission!.Id,briefingRecord!.Revision!.Value);ApplyBriefing(saved);AcknowledgementOpen=false;Message="Briefing signed. Connect to MSFS and confirm the aircraft to start tracking.";BriefingCloseRequested?.Invoke();}),()=>!IsBusy&&CanAcknowledge&&AcknowledgementChecked);
        OpenOfpCommand=new RelayCommand(()=>LaunchDispatch(briefing!.OfpUrl),()=>briefing is not null&&SimBriefImport.IsSafeOfpUrl(briefing.OfpUrl));
        SaveAndDispatchCommand=new AsyncCommand(()=>RunAsync(async()=>{if(BuilderTasks.Count>0||catalogueDraft)await SaveMissionPlanAsync();else await SaveAsync();var saved=Sorties.FirstOrDefault(s=>s.Id==draftId)??throw new InvalidOperationException("Saved mission is unavailable. Refresh Saved Missions.");await DispatchAsync(saved);}),()=>!IsBusy&&IsSignedIn&&(BuilderTasks.Count>0||Waypoints.Count>0));
        PlannerAircraftCommand=new RelayCommand(()=>PlannerStage=0);PlannerTasksCommand=new RelayCommand(()=>PlannerStage=1);PlannerReviewCommand=new RelayCommand(()=>PlannerStage=2);
    }
    private async Task LoadBriefingForAsync(Sortie mission)
    {
        var owner=pilot?.Id;var saved=await api.LoadBriefingAsync(mission.Id);var route=await api.LoadSortieWaypointsAsync(mission.Id);
        if(pilot?.Id!=owner)return;
        briefingMission=mission;Replace(BriefingPoints,route);ApplyBriefing(saved);
    }
    private void ApplyBriefing(SortieBriefingRecord saved)
    {
        briefingRecord=saved;briefing=saved.Document;missionNotes=saved.MissionNotes;Notify(nameof(MissionNotes));
        BriefingStatus=saved.Document is null?"Generate this mission in SimBrief, then import its briefing.":!saved.IsCurrent?"Out of date: the mission has changed. Regenerate in SimBrief and import again.":"Saved SimBrief briefing · imported "+saved.ImportedAt?.UtcDateTime.ToString("dd MMM yyyy HH:mm 'UTC'");
        NotifyBriefing();NotifyPreparation();
    }
    public async Task BriefingClosedAsync()
    {
        if(activeSortie is not null&&briefingMission?.Id!=activeSortie.Id)
        {
            try{await LoadBriefingForAsync(activeSortie);}catch(VistaApiException e){PreparationError=e.Message;}
        }
    }
    private void NotifyPreparation()
    {
        foreach(var n in new[]{nameof(OperationPreview),nameof(OperationTasks),nameof(PreparationSummary),nameof(IsTracking),nameof(IsPreparing),nameof(ShowMissionSelection),nameof(BriefingCurrent),nameof(BriefingSigned),nameof(BriefingState),nameof(BriefingSignature),nameof(BriefingTitle),nameof(BriefingOverview),nameof(BriefingDeparture),nameof(BriefingArrival),nameof(BriefingTasks),nameof(BriefingInstructions),nameof(CanEditBriefing),nameof(IsBriefingReadOnly),nameof(CanAcknowledge),nameof(BriefingFuelBreakdown),nameof(BriefingWeights),nameof(BriefingWeather),nameof(BriefingOperational),nameof(NotesDirty),nameof(FleetUnits),nameof(LibraryTitle),nameof(LibraryDetails),nameof(LibraryTasks)})Notify(n);
        NotifyLibraryReadiness();
        CommandManager.InvalidateRequerySuggested();
    }
    private string MissionSummary(Sortie mission)=>$"{Bases.FirstOrDefault(b=>b.Id==mission.DepartureBaseId)?.Icao} → {Bases.FirstOrDefault(b=>b.Id==mission.ArrivalBaseId)?.Icao} · {mission.Callsign}\n{Fleet.FirstOrDefault(a=>a.Id==mission.AircraftId)?.Label}\n{mission.SquadronLabel}";
    private string TaskSummary(Sortie? mission)
    {
        if(mission is null)return "Choose a mission to see its tasks.";
        if(mission.PlanSnapshot.ValueKind!=System.Text.Json.JsonValueKind.Object||missionData is null)return "Saved route · see ordered waypoints in the sortie briefing.";
        var snapshot=mission.PlanSnapshot;
        if(!snapshot.TryGetProperty("catalogue_plan",out var plan)||!plan.TryGetProperty("tasks",out var tasks))return "Saved route · see ordered waypoints in the sortie briefing.";
        return string.Join("\n",tasks.EnumerateArray().Select((t,i)=>{var option=missionData.Options.FirstOrDefault(o=>t.TryGetProperty("option_id",out var id)&&o.Id==id.GetGuid());var load=missionData.Payloads.FirstOrDefault(p=>t.TryGetProperty("payload_id",out var id)&&id.ValueKind==System.Text.Json.JsonValueKind.String&&p.Id==id.GetGuid());return $"{i+1:00}  {option?.Title??"Saved task"}"+(load is null?"":" · "+load.Label); }));
    }
    private string BuildTaskBriefings()
    {
        var text=new StringBuilder();
        foreach(var point in BriefingPoints)
        {
            var source=missionData?.ElementWaypoints.FirstOrDefault(w=>w.Id==point.SourceElementWaypointId);
            var element=missionData?.AllElements.FirstOrDefault(e=>e.Id==(point.MissionElementId??source?.MissionElementId));
            var kind=element is null?null:EntryCallout.Kind(point.Identifier,element.Code);
            if(kind=="strike")
            {
                var target=BriefingPoints.FirstOrDefault(p=>p.Position==point.Position+1);if(target is null)continue;
                text.AppendLine($"{element!.Title.ToUpperInvariant()} · SIMULATED EXERCISE BRIEF");
                text.AppendLine($"1  Initial point: {point.Identifier}\n2  Run-in: {MissionProgress.Bearing((double)point.Latitude,(double)point.Longitude,target.Latitude,target.Longitude):000}° TRUE (magnetic not supplied)\n3  Distance: {MissionProgress.DistanceNm((double)point.Latitude,(double)point.Longitude,target.Latitude,target.Longitude):N1} NM\n4  Target elevation: NOT VERIFIED\n5  Target: {element.Title}"+(element.Code=="VISTA-STRIKE-1"?" · large white radar dish":""));
                text.AppendLine($"6  Location: {target.Latitude:F6}, {target.Longitude:F6}\n7  Mark: {(element.Code=="VISTA-STRIKE-1"?"unmarked":"not specified")}\n8  Exercise friendlies: {(element.Code=="VISTA-STRIKE-1"?"none represented":"not specified")}\n9  Egress: pilot discretion\nRestrictions: simulated attack only; no weapons release. Entry {point.AltitudeFt:N0} ft / {point.SpeedKts} kt.");
                if(element.Code=="VISTA-STRIKE-1")text.AppendLine("One pass only. Minimum 2,000 ft: AGL/AMSL reference awaiting confirmation.");
                text.AppendLine();
            }
            else if(kind=="airdrop")text.AppendLine($"{element!.Title.ToUpperInvariant()} · AIRDROP PRACTICE\nFollow entry → drop → exit in saved order. Altitude and speed at pilot discretion unless specified.\n{point.Instructions}\n");
        }
        foreach(var point in BriefingPoints)text.AppendLine($"{point.Position:00}  {point.Identifier} · {(point.AltitudeFt is int alt?$"{alt:N0} ft":"altitude at discretion")} · {(point.SpeedKts is int speed?$"{speed} kt":"speed at discretion")}\n{point.Instructions}\n");
        return text.Length==0?"Direct destination flight. Review the route, load and arrival briefing.":text.ToString();
    }
}


