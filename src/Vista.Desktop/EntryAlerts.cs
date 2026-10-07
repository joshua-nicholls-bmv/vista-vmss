using Vista.Core;
namespace Vista.Desktop;
public sealed partial class MainViewModel
{
    private readonly HashSet<Guid> announcedEntries=[];
    private string waypointCallout="Visual strike and airdrop entry alerts at 10 NM.";
    public string WaypointCallout {get=>waypointCallout;private set=>Set(ref waypointCallout,value);}
    private void CheckEntryCallout(SimulatorTelemetry sample)
    {
        if(activeSortie is null||liveTracker is null||recovery?.Debrief is not null||sample.OnGround||sample.CapturedAt<DateTimeOffset.UtcNow.AddSeconds(-10)||sample.CapturedAt>DateTimeOffset.UtcNow.AddSeconds(5)||nextPoint>=ActiveRoute.Count||!double.IsFinite(sample.Latitude)||!double.IsFinite(sample.Longitude))return;
        var point=ActiveRoute[nextPoint];if(announcedEntries.Contains(point.Id))return;
        var source=missionData?.ElementWaypoints.FirstOrDefault(w=>w.Id==point.SourceElementWaypointId);
        var element=missionData?.AllElements.FirstOrDefault(e=>e.Id==(point.MissionElementId??source?.MissionElementId));
        if(element is null)return;var kind=EntryCallout.Kind(point.Identifier,element.Code);if(kind is null)return;
        var distance=MissionProgress.DistanceNm(sample.Latitude,sample.Longitude,point.Latitude,point.Longitude);if(!double.IsFinite(distance)||distance>EntryCallout.TriggerDistanceNm)return;
        announcedEntries.Add(point.Id);SaveProgress();
        var text=EntryCallout.Instruction(point,kind);WaypointCallout=$"{point.Identifier} · {distance:N1} NM · {text}";
        TrackingEvents.Insert(0,$"{sample.CapturedAt.UtcDateTime:HH:mm:ss}Z · ENTRY ALERT · {point.Identifier}");PersistRecovery();
    }
    private void ClearEntryAlerts(){announcedEntries.Clear();WaypointCallout="Visual strike and airdrop entry alerts at 10 NM.";}
}
