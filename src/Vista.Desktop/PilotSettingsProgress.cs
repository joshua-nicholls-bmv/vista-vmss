using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Vista.Core;
namespace Vista.Desktop;
public sealed record LocalPilotSettings(string SimBriefId);
public sealed record LocalMissionProgress(Guid MissionId,string RouteKey,int NextIndex,Guid[]? AnnouncedEntries=null);
public sealed partial class MainViewModel
{
    public string SettingsDirectory {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VISTA","settings");
    private Guid? settingsPilot,progressMission;private int nextPoint;
    public string SettingsName=>pilot?.DisplayName??"";
    public string SettingsIdentifier=>pilot?.PilotNumber??"";
    public string SettingsCallsign=>string.IsNullOrWhiteSpace(pilot?.Callsign)?"Callsign pending":pilot.Callsign;
    public string SettingsStatus=>pilot?.Status??"";
    public ICommand SavePilotSettingsCommand {get;private set;}=null!;
    public ICommand NextMissionPointCommand {get;private set;}=null!;
    public ICommand PreviousMissionPointCommand {get;private set;}=null!;
    public int CurrentMissionPointIndex=>nextPoint;
    public string ProgressTitle=>activeSortie is null?"Activate a mission":ActiveRoute.Count==0?"Direct flight · proceed to arrival":nextPoint>=ActiveRoute.Count?"Mission route complete · proceed to arrival":$"Next: {ActiveRoute[nextPoint].Identifier}";
    public string ProgressCount=>$"{Math.Min(nextPoint,ActiveRoute.Count)} / {ActiveRoute.Count} mission points passed";
    public string ProgressInstructions=>nextPoint<ActiveRoute.Count?$"Altitude: {(ActiveRoute[nextPoint].AltitudeFt is int alt?$"{alt:N0} ft":"Pilot discretion")} · Speed: {(ActiveRoute[nextPoint].SpeedKts is int kts?$"{kts} kt":"Pilot discretion")}\n{ActiveRoute[nextPoint].Instructions}":"Follow your flight plan to the selected arrival airfield.";
    public string ProgressNavigation
    {
        get
        {
            if(nextPoint>=ActiveRoute.Count)return "—";
            if(telemetry is null||DateTimeOffset.UtcNow-telemetry.CapturedAt>TimeSpan.FromSeconds(10))return "Connect to MSFS for live distance and bearing";
            var point=ActiveRoute[nextPoint];var distance=MissionProgress.DistanceNm(telemetry.Latitude,telemetry.Longitude,point.Latitude,point.Longitude);var bearing=MissionProgress.Bearing(telemetry.Latitude,telemetry.Longitude,point.Latitude,point.Longitude);
            return $"{distance:N1} NM · {bearing:000}° true · {(telemetry.GroundSpeedKts>30?$"ETA {TimeSpan.FromHours(distance/telemetry.GroundSpeedKts):hh\\:mm\\:ss}":"ETA —")}";
        }
    }
    private string SettingsPath=>Path.Combine(SettingsDirectory,(pilot?.Id??Guid.Empty).ToString("N")+".json");
    private string ProgressPath=>Path.Combine(SettingsDirectory,(pilot?.Id??Guid.Empty).ToString("N")+"-"+(activeSortie?.Id??Guid.Empty).ToString("N")+"-progress.json");
    private string RouteKey=>string.Join("|",ActiveRoute.Select(p=>$"{p.Id:N}:{p.Position}"));
    private void InitializePilotProgress()
    {
        SavePilotSettingsCommand=new AsyncCommand(()=>RunAsync(()=>{if(pilot is null)throw new InvalidOperationException("Sign in first.");var id=SimBriefPilotId.Trim();if(id.Length>0)SimBriefImport.FetchUrl(id,Guid.NewGuid());WriteLocal(SettingsPath,new LocalPilotSettings(SimBriefPilotId.Trim()));Message="Pilot settings saved on this computer.";return Task.CompletedTask;}),()=>!IsBusy&&pilot is not null);
        NextMissionPointCommand=new AsyncCommand(()=>RunAsync(()=>{nextPoint=Math.Min(nextPoint+1,ActiveRoute.Count);SaveProgress();NotifyProgress();return Task.CompletedTask;}),()=>!IsBusy&&activeSortie is not null&&nextPoint<ActiveRoute.Count);
        PreviousMissionPointCommand=new AsyncCommand(()=>RunAsync(()=>{nextPoint=Math.Max(0,nextPoint-1);SaveProgress();NotifyProgress();return Task.CompletedTask;}),()=>!IsBusy&&activeSortie is not null&&nextPoint>0);
    }
    private void LoadPilotSettings()
    {
        if(settingsPilot==pilot?.Id)return;settingsPilot=pilot?.Id;SimBriefPilotId=pilot?.SimbriefUserId??"";
        if(pilot is not null&&File.Exists(SettingsPath))try{var saved=JsonSerializer.Deserialize<LocalPilotSettings>(File.ReadAllText(SettingsPath));if(saved is not null){SimBriefPilotId=saved.SimBriefId;}}catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){Message="Pilot settings could not be read; enter your SimBrief ID again.";}
        foreach(var n in new[]{nameof(SettingsName),nameof(SettingsIdentifier),nameof(SettingsCallsign),nameof(SettingsStatus)})Notify(n);
    }
    private static void WriteLocal<T>(string path,T value){Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value));File.Move(path+".tmp",path,true);}
    private void SyncProgress()
    {
        if(progressMission==activeSortie?.Id)return;progressMission=activeSortie?.Id;nextPoint=0;announcedEntries.Clear();
        if(activeSortie is not null&&File.Exists(ProgressPath))try{var saved=JsonSerializer.Deserialize<LocalMissionProgress>(File.ReadAllText(ProgressPath));if(saved?.MissionId==activeSortie.Id&&saved.RouteKey==RouteKey){nextPoint=Math.Clamp(saved.NextIndex,0,ActiveRoute.Count);foreach(var id in saved.AnnouncedEntries??[])if(ActiveRoute.Any(p=>p.Id==id))announcedEntries.Add(id);}}catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){Message="Mission progress could not be restored. Select the next point manually.";}
        NotifyProgress();
    }
    private void SaveProgress(){if(activeSortie is not null)WriteLocal(ProgressPath,new LocalMissionProgress(activeSortie.Id,RouteKey,nextPoint,announcedEntries.ToArray()));}
    private void UpdateProgress(SimulatorTelemetry sample)
    {
        CheckEntryCallout(sample);
        if(activeSortie is not null&&liveTracker is not null&&!sample.OnGround&&DateTimeOffset.UtcNow-sample.CapturedAt<=TimeSpan.FromSeconds(10)&&sample.CapturedAt<=DateTimeOffset.UtcNow.AddSeconds(5)&&nextPoint<ActiveRoute.Count&&MissionProgress.DistanceNm(sample.Latitude,sample.Longitude,ActiveRoute[nextPoint].Latitude,ActiveRoute[nextPoint].Longitude)<=1)
        {nextPoint++;SaveProgress();}
        NotifyProgress();
    }
    private void NotifyProgress(){foreach(var n in new[]{nameof(CurrentMissionPointIndex),nameof(ProgressTitle),nameof(ProgressCount),nameof(ProgressInstructions),nameof(ProgressNavigation)})Notify(n);CommandManager.InvalidateRequerySuggested();}
}
