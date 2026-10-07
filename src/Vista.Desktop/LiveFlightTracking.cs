using System.IO;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
using Vista.Core;
namespace Vista.Desktop;
public sealed record FlightRecovery(Guid PilotId,Guid SortieId,List<TrackUpload> Points,int Uploaded,FlightTrackerState State,List<string> Events,TrackedDebrief? Debrief);
public sealed partial class MainViewModel
{
    public string SimBriefAtlasAirframeId { get; set; } = "";
    public string SimBriefTyphoonAirframeId { get; set; } = "";
    private EngineEventMonitor? preflightEngines;
    private Sortie? activeSortie;
    private FlightTracker? liveTracker;
    private FlightRecovery? recovery;
    private bool uploading;
    private string uploadStatus="No active mission",notes="";
    private readonly DispatcherTimer uploadTimer=new(){Interval=TimeSpan.FromSeconds(5)};
    public string RecoveryDirectory {get;set;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VISTA","tracking");
    public Sortie? ActiveSortie => activeSortie;
    public Airfield? ActiveDeparture => Bases.FirstOrDefault(b=>b.Id==activeSortie?.DepartureBaseId);
    public Airfield? ActiveArrival => Bases.FirstOrDefault(b=>b.Id==activeSortie?.ArrivalBaseId);
    public bool HasActiveSortie => activeSortie is not null;
    public string ActiveMissionLabel => activeSortie?.Title ?? "Choose a saved mission to begin";
    public string ActiveMissionRoute => activeSortie is null?"Mission Planner → Saved Missions → Activate":$"{Bases.FirstOrDefault(b=>b.Id==activeSortie.DepartureBaseId)?.Icao} → {Bases.FirstOrDefault(b=>b.Id==activeSortie.ArrivalBaseId)?.Icao} · {activeSortie.Callsign} · {Fleet.FirstOrDefault(a=>a.Id==activeSortie.AircraftId)?.Serial}";
    public string UploadStatus {get=>uploadStatus;private set{Set(ref uploadStatus,value);NotifyLibraryReadiness();}}
    public string DebriefNotes {get=>notes;set=>Set(ref notes,value);}
    private bool aircraftChecked;
    public bool AircraftChecked {get=>aircraftChecked;set{Set(ref aircraftChecked,value);NotifyPreparation();}}
    public string FlightPhase => liveTracker is null?activeSortie is null?"STANDBY":"MISSION ACTIVE":liveTracker.InTime is not null?"PARKED · READY TO SUBMIT":telemetry is null?"TRACKING PAUSED":telemetry.OnGround?liveTracker.Landings>0?"TAXI IN":"TAXI OUT":"AIRBORNE";
    public bool SimulatorIsConnected => telemetry is not null && DateTimeOffset.UtcNow-telemetry.CapturedAt<TimeSpan.FromSeconds(10);
    public string SimulatorConnectionLabel => SimulatorIsConnected ? "SIMULATOR CONNECTED" : SimulatorStatus.StartsWith("Connecting",StringComparison.OrdinalIgnoreCase) ? "CONNECTING TO SIMULATOR" : SimulatorStatus.Contains("waiting for aircraft telemetry",StringComparison.OrdinalIgnoreCase) ? "WAITING FOR TELEMETRY" : "SIMULATOR DISCONNECTED";
    public string EngineStatusLabel => SimulatorIsConnected ? telemetry!.EngineRunning ? "Engines running" : "Engines stopped" : "Engine status unavailable";
    public string ConnectionStatusTone => SimulatorIsConnected ? "good" : SimulatorConnectionLabel.Contains("CONNECTING")||SimulatorConnectionLabel.StartsWith("WAITING") ? "warning" : "danger";
    public string FlightStatusTone => liveTracker is not null ? ConnectionStatusTone : HasActiveSortie ? "accent" : "neutral";
    public string UploadStatusTone => recovery is null ? "neutral" : recovery.Uploaded<recovery.Points.Count ? "warning" : "good";
    public string OutClock => Clock(liveTracker?.OutTime);
    public string OffClock => Clock(liveTracker?.OffTime);
    public string OnClock => Clock(liveTracker?.OnTime);
    public string InClock => Clock(liveTracker?.InTime);
    public string FlightCounters => $"{liveTracker?.Takeoffs??0} takeoffs · {liveTracker?.Landings??0} landings · {recovery?.Points.Count??0} track points";
    public string FuelRemaining => telemetry is null||!double.IsFinite(telemetry.FuelLb)?"—":$"{telemetry.FuelLb*.45359237:N0} kg fuel remaining";
    public string BlockTime => liveTracker?.OutTime is DateTimeOffset start?TimeSpan.FromSeconds(Math.Max(0,((liveTracker.InTime??telemetry?.CapturedAt??start)-start).TotalSeconds)).ToString(@"hh\:mm\:ss"):"00:00:00";
    public string LandingRate => liveTracker?.LandingRateFpm is double rate?$"{rate:N0} fpm":"—";
    public System.Collections.ObjectModel.ObservableCollection<StoredWaypoint> ActiveRoute {get;}=[];
    public ICommand ActivateMissionCommand {get;private set;}=null!;
    public ICommand ActivateAndDispatchCommand {get;private set;}=null!;
    public ICommand OpenSimBriefCommand {get;private set;}=null!;
    public ICommand StartLiveTrackingCommand {get;private set;}=null!;
    public ICommand SubmitDebriefCommand {get;private set;}=null!;
    public ICommand RetryTrackUploadCommand {get;private set;}=null!;
    public ICommand CancelActiveMissionCommand {get;private set;}=null!;
    public Action<string> LaunchDispatch {get;set;}=url=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});
    private string RecoveryPath => Path.Combine(RecoveryDirectory,(pilot?.Id??Guid.Empty).ToString("N")+".json");
    private static string Clock(DateTimeOffset? time)=>time?.UtcDateTime.ToString("HH:mm:ss 'Z'")??"—";
    public void SaveTrackingOnClose() { fleetTimer.Stop();uploadTimer.Stop(); try { PersistRecovery(); } catch(Exception error) when(error is IOException or UnauthorizedAccessException) { UploadStatus="Local recovery could not be saved: "+error.Message; } }
    private void InitializeLiveTracking()
    {
        InitializeBriefing(); InitializePilotProgress(); InitializePreparation();
        ActivateMissionCommand=new AsyncCommand(()=>RunAsync(()=>ActivateMissionAsync(false)),()=>!IsBusy&&SelectedSortie is not null);
        ActivateAndDispatchCommand=new AsyncCommand(()=>RunAsync(()=>ActivateMissionAsync(true)),()=>!IsBusy&&SelectedSortie is not null);
        OpenSimBriefCommand=new AsyncCommand(()=>RunAsync(()=>DispatchAsync(SelectedSortie??activeSortie??throw new InvalidOperationException("Select a saved mission."))),()=>!IsBusy&&(SelectedSortie is not null||activeSortie is not null));
        StartLiveTrackingCommand=new AsyncCommand(()=>RunAsync(StartLiveAsync),()=>!IsBusy&&activeSortie?.Status=="briefed"&&telemetry is not null&&liveTracker is null&&BriefingSigned&&AircraftChecked&&SimulatorIsConnected);
        SubmitDebriefCommand=new AsyncCommand(()=>RunAsync(SubmitLiveAsync),()=>!IsBusy&&liveTracker?.InTime is not null&&activeSortie is not null);
        RetryTrackUploadCommand=new AsyncCommand(()=>RunAsync(async()=>{await FlushTrackAsync();Message=UploadStatus;}),()=>!IsBusy&&recovery is not null&&!uploading);
        CancelActiveMissionCommand=new AsyncCommand(()=>RunAsync(async()=>{if(activeSortie is null)return;await api.CancelActiveSortieAsync(activeSortie.Id);ClearLiveTracking(true);await RefreshAsync();Message="Mission deactivated. Unflown plans can be reused; recorded flights retain their history.";}),()=>!IsBusy&&activeSortie is not null&&!uploading);
        uploadTimer.Tick+=async (_,_)=>{if(recovery is null||uploading||IsBusy)return;try{await FlushTrackAsync();}catch(Exception e) when(e is VistaApiException or System.Net.Http.HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException){UploadStatus="Upload paused · points retained locally · "+e.Message;}};
    }
    private async Task ActivateMissionAsync(bool dispatch)
    {
        var selected=SelectedSortie??throw new InvalidOperationException("Select a saved mission.");
        if(dispatch) { SimBriefDispatch.FlightNumber(selected.Callsign); DispatchProfile(selected); }
        if(activeSortie is not null && activeSortie.Id!=selected.Id) throw new InvalidOperationException("Finish or cancel the current active mission first.");
        var id=selected.Status is "completed" or "cancelled"?await api.DuplicateSortieAsync(selected.Id):selected.Id;
        await api.ActivateSortieAsync(id);await RefreshAsync();
        SelectedTab=0;Message="Mission active. Review SimBrief, connect to MSFS, then start tracking before taxi.";
        if(activeSortie is not null){await LoadBriefingForAsync(activeSortie);BriefingRequested?.Invoke();}
        if(dispatch)await DispatchAsync(activeSortie??selected);
    }
    private async Task DispatchAsync(Sortie sortie)
    {
        var prepared=await api.LoadBriefingAsync(sortie.Id);
        var points=await api.LoadSortieWaypointsAsync(sortie.Id);
        var plane=Fleet.FirstOrDefault(a=>a.Id==sortie.AircraftId)??throw new InvalidOperationException("The saved aircraft is unavailable in the catalogue.");
        var type=catalogue?.Types.FirstOrDefault(t=>t.Id==plane.Aircraft.AircraftTypeId);
        var dep=Bases.FirstOrDefault(b=>b.Id==sortie.DepartureBaseId)?.Icao??throw new InvalidOperationException("Departure unavailable.");
        var arr=Bases.FirstOrDefault(b=>b.Id==sortie.ArrivalBaseId)?.Icao??throw new InvalidOperationException("Arrival unavailable.");
        var profile = DispatchProfile(sortie);
        LaunchDispatch(SimBriefDispatch.BuildUrl(sortie,points,dep,arr,plane.Serial,SimBriefDispatch.AircraftCode(type?.Code??"",type?.SimbriefAircraftCode),profile,pilot?.DisplayName??"",Bases.FirstOrDefault(b=>b.Id==sortie.AlternateBaseId)?.Icao??"",prepared.CurrentPlanKey));
        Message="SimBrief opened with the saved route. Select the correct aircraft/profile and generate your briefing there.";
    }
    private string DispatchProfile(Sortie sortie)
    {
        var plane=Fleet.FirstOrDefault(a=>a.Id==sortie.AircraftId)??throw new InvalidOperationException("The saved aircraft is unavailable in the catalogue.");
        var type=catalogue?.Types.FirstOrDefault(t=>t.Id==plane.Aircraft.AircraftTypeId);
        if(type?.Code is not ("ATLAS" or "TYPHOON-FGR4" or "TYPHOON-T3"))return "";
        var atlas=type.Code=="ATLAS";
        var profile=(atlas?SimBriefAtlasAirframeId:SimBriefTyphoonAirframeId).Trim();
        if(!System.Text.RegularExpressions.Regex.IsMatch(profile,@"^\d+_\d+$"))
            throw new InvalidOperationException($"The {(atlas?"Atlas":"Eurofighter")} SimBrief profile needs its saved airframe Internal ID. Set {(atlas?"simbrief_atlas_airframe_id":"simbrief_typhoon_airframe_id")} in appsettings.json, then restart VISTA.");
        return profile;
    }
    private async Task SyncActiveMissionAsync()
    {
        var next=Sorties.FirstOrDefault(s=>s.Status is "briefed" or "airborne");
        if(next?.Id!=activeSortie?.Id){ClearLiveTracking(false);activeSortie=next;}
        else activeSortie=next;
        if(next is not null)
        {
            Replace(ActiveRoute,await api.LoadSortieWaypointsAsync(next.Id));
            if(next.Status=="airborne"&&recovery is null&&File.Exists(RecoveryPath))
            {
                FlightRecovery? saved=null;
                try
                {
                    saved=JsonSerializer.Deserialize<FlightRecovery>(File.ReadAllText(RecoveryPath),SupabaseClient.JsonOptions);
                    if(saved is not null&&(saved.Points is null||saved.State is null||saved.Events is null||saved.Uploaded<0||saved.Uploaded>saved.Points.Count))saved=null;
                }
                catch(Exception error) when(error is JsonException or IOException or UnauthorizedAccessException)
                {UploadStatus="Local recovery could not be read. The stored file has been preserved.";}
                if(saved is not null&&saved.PilotId==pilot!.Id&&saved.SortieId==next.Id)
                {recovery=saved;liveTracker=FlightTracker.Restore(saved.State);WireFlightEvents();Replace(TrackingEvents,saved.Events);lastTrackAt=saved.Points.LastOrDefault()?.MeasuredAt;
                    Replace(LocalTrackPoints,saved.Points.TakeLast(1800).Select(p=>new SimulatorTelemetry(p.MeasuredAt,"Recovered aircraft",(double)p.AltitudeFt,(double)p.GroundspeedKts,(double)p.HeadingDeg,(double)p.Latitude,(double)p.Longitude,p.OnGround,false,false,(double)(p.FuelKg??0)/.45359237,0,0)));
                    uploadTimer.Start();UploadStatus="Flight recovered · reconnect MSFS to continue";}
            }
            if(next.Status=="airborne"&&recovery is null)UploadStatus="Active tracking has no local recovery on this computer. Resume from the original computer or cancel this mission.";
            else if(next.Status=="briefed")UploadStatus="Mission reserved · tracking starts before taxi";
        }
        if(next is not null&&liveTracker is null&&preflightEngines is null){preflightEngines=new();preflightEngines.Changed+=text=>TrackingEvents.Insert(0,$"{telemetry?.CapturedAt.UtcDateTime:HH:mm:ss}Z · {text}");if(telemetry is not null)preflightEngines.Accept(telemetry);}
        if(next is not null)
        {
            try{await LoadBriefingForAsync(next);PreparationError="";}
            catch(VistaApiException e){PreparationError=e.Message;}
        }
        SyncProgress(); NotifyFlight();
    }
    private async Task StartLiveAsync()
    {
        if(activeSortie is null||telemetry is null||DateTimeOffset.UtcNow-telemetry.CapturedAt>TimeSpan.FromSeconds(5))throw new InvalidOperationException("Connect to fresh MSFS telemetry first.");
        if(!telemetry.OnGround||telemetry.GroundSpeedKts>=1||!telemetry.ParkingBrake)throw new InvalidOperationException("Start tracking parked on the ground with the parking brake set.");
        if(!BriefingSigned)throw new InvalidOperationException("Import and sign the current briefing before tracking.");
        if(!AircraftChecked)throw new InvalidOperationException("Confirm the simulator aircraft and mission match your saved plan.");
        await api.StartSortieAsync(activeSortie.Id);
        activeSortie=activeSortie with {Status="airborne"};liveTracker=new FlightTracker(preflightEngines?.HasObserved??false);
        recovery=new(pilot!.Id,activeSortie.Id,[],0,liveTracker.Capture(),[],null);
        LocalTrackPoints.Clear();DebriefNotes="";WireFlightEvents();lastTrackAt=null;
        PersistRecovery();uploadTimer.Start();AcceptLiveSample(telemetry);NotifyFlight();Message="Live sortie tracking started. Position points are queued every five seconds.";
    }
    private void WireFlightEvents()=>liveTracker!.FlightEvent+=text=>{TrackingEvents.Insert(0,$"{telemetry?.CapturedAt.UtcDateTime:HH:mm:ss}Z · {text}");if(TrackingEvents.Count>500)TrackingEvents.RemoveAt(500);PersistRecovery();};
    private void AcceptLiveSample(SimulatorTelemetry sample)
    {
        if(liveTracker is null||recovery is null||recovery.Debrief is not null)return;
        if(!double.IsFinite(sample.AltitudeFt)||sample.AltitudeFt is < -2000 or > 100000||!double.IsFinite(sample.GroundSpeedKts)||sample.GroundSpeedKts is < 0 or > 3000||!double.IsFinite(sample.HeadingDegrees))return;
        liveTracker.Accept(sample);
        if(lastTrackAt is null||sample.CapturedAt-lastTrackAt>=TimeSpan.FromSeconds(5))
        {
            QueueTrack(sample);NotifyFlight();
        }
        else NotifyFlight();
    }
    private void QueueTrack(SimulatorTelemetry sample)
    {
        if(recovery is null)return;
        var point=new TrackUpload(recovery.Points.Count,sample.CapturedAt,(decimal)sample.Latitude,(decimal)sample.Longitude,(decimal)sample.AltitudeFt,(decimal)sample.GroundSpeedKts,(decimal)((sample.HeadingDegrees%360+360)%360),sample.OnGround,double.IsFinite(sample.FuelLb)&&sample.FuelLb>0?(decimal)(sample.FuelLb*.45359237):null);
        recovery.Points.Add(point);lastTrackAt=sample.CapturedAt;
        LocalTrackPoints.Add(sample);if(LocalTrackPoints.Count>1800)LocalTrackPoints.RemoveAt(0);
        PersistRecovery();UploadStatus=$"{recovery.Uploaded}/{recovery.Points.Count} points uploaded · local recovery saved";
    }
    private void PersistRecovery()
    {
        if(recovery is null||liveTracker is null)return;
        recovery=recovery with {State=liveTracker.Capture(),Events=TrackingEvents.ToList()};
        Directory.CreateDirectory(RecoveryDirectory);var temp=RecoveryPath+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(recovery,SupabaseClient.JsonOptions));File.Move(temp,RecoveryPath,true);
    }
    private async Task FlushTrackAsync()
    {
        if(recovery is null||uploading)return;
        uploading=true;var current=recovery;
        try
        {
            while(recovery?.SortieId==current.SortieId&&recovery.Uploaded<recovery.Points.Count)
            {
                var batch=recovery.Points.Skip(recovery.Uploaded).Take(100).ToList();
                await api.UploadTrackAsync(current.SortieId,batch);
                if(recovery?.SortieId!=current.SortieId) return;
                recovery=recovery with {Uploaded=recovery.Uploaded+batch.Count};PersistRecovery();
            }
            if(recovery is not null)UploadStatus=$"{recovery.Uploaded}/{recovery.Points.Count} points uploaded";
        }
        finally{uploading=false;NotifyFlight();}
    }
    private async Task SubmitLiveAsync()
    {
        if(recovery is null||liveTracker?.InTime is null||activeSortie is null)throw new InvalidOperationException("Land and park before submitting.");
        if(recovery.Debrief is null)
        {
            if(telemetry is null||!telemetry.OnGround||!telemetry.ParkingBrake||telemetry.GroundSpeedKts>=1||DateTimeOffset.UtcNow-telemetry.CapturedAt>TimeSpan.FromSeconds(5))throw new InvalidOperationException("Fresh ground telemetry is required to finish.");
            QueueTrack(telemetry);
            decimal distance=0;TrackUpload? previous=null;
            foreach(var point in recovery.Points){if(previous is not null&&point.MeasuredAt-previous.MeasuredAt<=TimeSpan.FromSeconds(15))distance+=(decimal)DistanceNm(previous,point);previous=point;}
            var seconds=liveTracker.OffTime is not null&&liveTracker.OnTime is not null?Math.Max(0,(long)(liveTracker.OnTime.Value-liveTracker.OffTime.Value).TotalSeconds):0;
            var fuel=liveTracker.StartFuelLb is double start&&liveTracker.EndFuelLb is double end?(decimal?)Math.Max(0,(start-end)*.45359237):null;
            recovery=recovery with {Debrief=new(telemetry.CapturedAt,seconds,distance,(decimal?)liveTracker.LandingRateFpm,fuel,DebriefNotes,TrackingEvents.ToList())};PersistRecovery();
        }
        await FlushTrackAsync();
        if(uploading)throw new InvalidOperationException("Wait for the current upload, then submit again.");
        await api.FinishSortieAsync(activeSortie.Id,recovery.Debrief!);ClearLiveTracking(true);await RefreshAsync();Message="Sortie completed. Tracking and debrief saved; pilot statistics updated.";
    }
    private static double DistanceNm(TrackUpload a,TrackUpload b)
    {
        double r=Math.PI/180,lat1=(double)a.Latitude*r,lat2=(double)b.Latitude*r,dlat=lat2-lat1,dlon=(double)(b.Longitude-a.Longitude)*r;
        var h=Math.Pow(Math.Sin(dlat/2),2)+Math.Cos(lat1)*Math.Cos(lat2)*Math.Pow(Math.Sin(dlon/2),2);return 3440.065*2*Math.Asin(Math.Sqrt(Math.Clamp(h,0,1)));
    }
    private void ClearLiveTracking(bool delete)
    {
        uploadTimer.Stop();if(delete&&pilot is not null&&File.Exists(RecoveryPath))File.Delete(RecoveryPath);
        ClearEntryAlerts();preflightEngines=null;TrackingEvents.Clear();progressMission=null;nextPoint=0;NotifyProgress();ClearBriefing();liveTracker=null;recovery=null;activeSortie=null;ActiveRoute.Clear();AircraftChecked=false;NotifyFlight();
    }
    private void NotifyFlight()
    {
        NotifyProgress(); NotifyPreparation();
        foreach(var name in new[]{nameof(SimulatorIsConnected),nameof(SimulatorConnectionLabel),nameof(EngineStatusLabel),nameof(ActiveDeparture),nameof(ActiveArrival),nameof(ActiveSortie),nameof(HasActiveSortie),nameof(ActiveMissionLabel),nameof(ActiveMissionRoute),nameof(FlightPhase),nameof(OutClock),nameof(OffClock),nameof(OnClock),nameof(InClock),nameof(FlightCounters),nameof(LandingRate),nameof(FuelRemaining),nameof(BlockTime),nameof(ConnectionStatusTone),nameof(FlightStatusTone),nameof(UploadStatusTone)})Notify(name);
        CommandManager.InvalidateRequerySuggested();
    }
}

