using System.Net.Http;
using System.Windows.Input;
using Vista.Core;
namespace Vista.Desktop;
public sealed partial class MainViewModel
{
    private static readonly HttpClient briefingHttp=new(){Timeout=TimeSpan.FromSeconds(25)};
    private string simBriefPilotId="",briefingStatus="Generate your briefing in SimBrief, then import it here.";
    private SimBriefBriefing? briefing;
    public string SimBriefPilotId {get=>simBriefPilotId;set=>Set(ref simBriefPilotId,value);}
    public string BriefingStatus {get=>briefingStatus;private set=>Set(ref briefingStatus,value);}
    public string BriefingFlight => briefing is null?"No briefing imported":$"{briefing.Departure} → {briefing.Arrival} · RRR {briefing.FlightNumber} · {briefing.Registration} · {briefing.Aircraft}";
    public string BriefingRoute => briefing?.Route??"—";
    public string BriefingFuel => briefing is null?"—":$"Ramp fuel: {Amount(briefing.RampFuelKg)} · Trip fuel: {Amount(briefing.TripFuelKg)}";
    public string BriefingDetails => briefing is null?"—":$"Cruise: {(briefing.CruiseAltitudeFt is decimal alt?$"{alt:N0} ft":"—")} · Estimated flight: {(briefing.FlightSeconds is long sec?TimeSpan.FromSeconds(sec).ToString(@"hh\:mm"):"—")} · Imported {briefing.ImportedAt:HH:mm} UTC";
    private static string Amount(decimal? n)=>n is decimal value?$"{value:N0} kg":"—";
    public ICommand OpenActiveBriefingCommand {get;private set;}=null!;
    public ICommand ImportBriefingCommand {get;private set;}=null!;
    public Func<string,Task<string>> FetchBriefing {get;set;}=async url=>{using var response=await briefingHttp.GetAsync(url);if(!response.IsSuccessStatusCode)throw new InvalidOperationException("SimBrief could not retrieve this briefing. Check your Pilot ID and generate the mission first.");if(response.Content.Headers.ContentLength>2_000_000)throw new InvalidOperationException("SimBrief returned an unexpectedly large briefing.");return await response.Content.ReadAsStringAsync();};
    private void InitializeBriefing()
    {
        OpenActiveBriefingCommand=new AsyncCommand(()=>RunAsync(()=>DispatchAsync(briefingMission??activeSortie??throw new InvalidOperationException("Activate a mission first."))),()=>!IsBusy&&(briefingMission??activeSortie) is not null);
        ImportBriefingCommand=new AsyncCommand(()=>RunAsync(ImportBriefingAsync),()=>!IsBusy&&CanEditBriefing&&!NotesDirty);
    }
    private async Task ImportBriefingAsync()
    {
        var mission=briefingMission??activeSortie??throw new InvalidOperationException("Open the mission briefing before importing.");var owner=pilot?.Id;
        var plane=Fleet.First(a=>a.Id==mission.AircraftId);
        var dep=Bases.First(b=>b.Id==mission.DepartureBaseId).Icao;var arr=Bases.First(b=>b.Id==mission.ArrivalBaseId).Icao;
        var id=string.IsNullOrWhiteSpace(SimBriefPilotId)?pilot?.SimbriefUserId??"":SimBriefPilotId;
        var current=await api.LoadBriefingAsync(mission.Id);
        BriefingStatus="Importing SimBrief briefing…";
        try
        {
            var json=await FetchBriefing(SimBriefImport.FetchUrl(id,mission.Id,current.CurrentPlanKey));
            var imported=SimBriefImport.Parse(json,mission,dep,arr,plane.Serial,current.CurrentPlanKey);
            if((briefingMission??activeSortie)?.Id!=mission.Id||pilot?.Id!=owner)return;
            var saved=await api.SaveBriefingAsync(mission.Id,current.CurrentPlanKey,imported);
            if((briefingMission??activeSortie)?.Id!=mission.Id||pilot?.Id!=owner)return;
            ApplyBriefing(saved);Message="SimBrief briefing imported and saved to this mission.";
        }
        catch { BriefingStatus=briefing is null?"Import failed. Generate the active mission in SimBrief and check your Pilot ID.":"Refresh failed. The previously imported briefing remains displayed.";throw; }
    }
    private void ClearBriefing(){briefing=null;briefingRecord=null;briefingMission=null;missionNotes="";AcknowledgementOpen=false;AcknowledgementChecked=false;BriefingPoints.Clear();Notify(nameof(MissionNotes));NotifyPreparation();BriefingStatus="Generate your briefing in SimBrief, then import it here.";NotifyBriefing();}
    private void NotifyBriefing(){foreach(var name in new[]{nameof(BriefingFlight),nameof(BriefingRoute),nameof(BriefingFuel),nameof(BriefingDetails)})Notify(name);}
}

