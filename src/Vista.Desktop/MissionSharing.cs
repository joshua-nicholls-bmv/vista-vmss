using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Vista.Core;
namespace Vista.Desktop;
public sealed partial class MainViewModel
{
 public ObservableCollection<SharedMission> SharedMissions {get;}=[];
 public ICollectionView SharedMissionView {get;private set;}=null!;
 public event Action? SharedMissionsRequested;
 private string sharedSearch="",sharedSquadron="All squadrons";
 private SharedMission? selectedSharedMission;
 private Guid importRequest=Guid.NewGuid();
 private DateTimeOffset? fleetCheckedAt;
 private readonly DispatcherTimer fleetTimer=new(){Interval=TimeSpan.FromSeconds(30)};
 public string SharedSearch {get=>sharedSearch;set{Set(ref sharedSearch,value);SharedMissionView.Refresh();}}
 public string SharedSquadron {get=>sharedSquadron;set{Set(ref sharedSquadron,value);SharedMissionView.Refresh();}}
 public string[] SharedSquadrons=>new[]{"All squadrons"}.Concat(SharedMissions.Select(s=>s.Squadron).Distinct().Order()).ToArray();
 public SharedMission? SelectedSharedMission {get=>selectedSharedMission;set{if(Set(ref selectedSharedMission,value))importRequest=Guid.NewGuid();Notify(nameof(CanWithdrawShared));Notify(nameof(SharedDetails));}}
 public bool CanWithdrawShared=>SelectedSharedMission?.OwnerId==pilot?.Id&&SelectedSharedMission?.Active==true;
 public string SharedDetails=>SelectedSharedMission is not SharedMission s?"Choose a shared mission.":$"{s.Title}\n{s.Squadron}\n{s.Route}\nShared by {s.Author}\nRevision {s.Revision} · {s.UpdatedAt.UtcDateTime:dd MMM yyyy HH:mm} UTC\n{(s.Active?"Available to import":"Withdrawn")}\n\nImport creates your own editable plan. Generate your own SimBrief briefing before flying.";
 public string FleetAvailabilityUpdated=>fleetCheckedAt is DateTimeOffset at?$"Availability checked {at.UtcDateTime:HH:mm:ss} UTC · refreshes every 30 seconds while this page is open":"Availability not yet checked";
 public ICommand OpenSharedMissionsCommand {get;private set;}=null!;
 public ICommand RefreshSharedMissionsCommand {get;private set;}=null!;
 public ICommand PublishMissionCommand {get;private set;}=null!;
 public ICommand WithdrawSharedMissionCommand {get;private set;}=null!;
 public ICommand ImportSharedMissionCommand {get;private set;}=null!;
 public ICommand RefreshFleetAvailabilityCommand {get;private set;}=null!;
 private void InitializeSharing()
 {
  SharedMissionView=new ListCollectionView(SharedMissions){Filter=o=>o is SharedMission s&&(SharedSquadron=="All squadrons"||s.Squadron==SharedSquadron)&&$"{s.Title} {s.Author} {s.Squadron} {s.Route}".Contains(SharedSearch,StringComparison.OrdinalIgnoreCase)};
  OpenSharedMissionsCommand=new AsyncCommand(()=>RunAsync(async()=>{await RefreshSharedAsync();Message="Choose a shared mission to add to your library.";SharedMissionsRequested?.Invoke();}),()=>!IsBusy&&IsSignedIn);
  RefreshSharedMissionsCommand=new AsyncCommand(()=>RunAsync(RefreshSharedAsync),()=>!IsBusy&&IsSignedIn);
  PublishMissionCommand=new AsyncCommand(()=>RunAsync(async()=>{var s=SelectedSortie??throw new InvalidOperationException("Choose a saved mission.");await api.PublishMissionAsync(s.Id);await RefreshSharedAsync();Message="Saved mission shared with VISTA pilots. Existing imported plans remain independent.";}),()=>!IsBusy&&SelectedSortie is not null);
  WithdrawSharedMissionCommand=new AsyncCommand(()=>RunAsync(async()=>{await api.WithdrawSharedMissionAsync(SelectedSharedMission!.Id);await RefreshSharedAsync();Message="Mission withdrawn. Plans already imported by other pilots remain theirs.";}),()=>!IsBusy&&CanWithdrawShared);
  ImportSharedMissionCommand=new AsyncCommand(()=>RunAsync(async()=>{var id=await api.ImportSharedMissionAsync(SelectedSharedMission!.Id,importRequest);await RefreshAsync();var imported=Sorties.FirstOrDefault(s=>s.Id==id);var card=MissionCards.FirstOrDefault(c=>c.PlanId==(imported?.MissionPlanId??id));LibraryFolder=card?.Folder??"Not yet flown";SelectedMissionCard=card;OperationMission=card?.Archived==false?card.Mission:null;Message="Shared mission added to your library. Review aircraft, callsign and route before activation.";}),()=>!IsBusy&&SelectedSharedMission?.Active==true);
  RefreshFleetAvailabilityCommand=new AsyncCommand(()=>RunAsync(RefreshFleetAvailabilityAsync),()=>!IsBusy&&IsSignedIn);
  fleetTimer.Tick+=async(_,_)=>{if(!IsSignedIn||IsBusy||SelectedTab!=3)return;await RunAsync(RefreshFleetAvailabilityAsync);};fleetTimer.Start();
 }
 private async Task RefreshSharedAsync()
 {
  var owner=pilot?.Id;var rows=await api.LoadSharedMissionsAsync();if(pilot?.Id!=owner)return;var selected=SelectedSharedMission?.Id;Replace(SharedMissions,rows);SelectedSharedMission=SharedMissions.FirstOrDefault(s=>s.Id==selected)??SharedMissions.FirstOrDefault();Notify(nameof(SharedSquadrons));
 }
 private async Task RefreshFleetAvailabilityAsync()
 {
  var owner=pilot?.Id;var rows=await api.LoadFleetReservationsAsync();if(pilot?.Id!=owner)return;ApplyFleetReservations(rows);Message=FleetAvailabilityUpdated;
 }
 private void ApplyFleetReservations(IReadOnlyList<FleetReservation> rows)
 {
  var selected=FleetSelection?.Id;var aircraft=SelectedAircraft?.Id;
  Replace(Fleet,Fleet.Select(f=>f with{Reservation=rows.FirstOrDefault(r=>r.AircraftId==f.Id)}).ToList());
  Replace(AvailableAircraft,Fleet.Where(f=>f.Status=="available"&&f.Reservation is null&&catalogue?.Types.Any(t=>t.Id==f.Aircraft.AircraftTypeId)==true));
  SelectedAircraft=AvailableAircraft.FirstOrDefault(f=>f.Id==aircraft);FleetSelection=Fleet.FirstOrDefault(f=>f.Id==selected);
  fleetCheckedAt=DateTimeOffset.UtcNow;Notify(nameof(FleetAvailabilityUpdated));
 }
}
