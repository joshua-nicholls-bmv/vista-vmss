using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using Vista.Core;
namespace Vista.Desktop;
public sealed record MissionCard(Sortie Mission,Guid PlanId,string Route,string Tasks,int TimesFlown,DateTimeOffset? LastFlown,bool Archived,bool Active)
{
 public bool Favourite {get;init;}
 public string DisplayTitle=>(Favourite?"★ ":"")+Title;
 public string Title=>Mission.Title;
 public string Squadron=>Mission.SquadronLabel;
 public string Folder=>Archived?"Archived":TimesFlown>0?"Previously flown":"Not yet flown";
 public string Activity=>Active?"ACTIVE":TimesFlown>0?$"Flown {TimesFlown} · Last flown {LastFlown?.LocalDateTime:dd MMM yyyy}":"Not yet flown";
}
public sealed partial class MainViewModel
{
 public ObservableCollection<MissionCard> MissionCards {get;}=[];
 public ObservableCollection<Sortie> OperationPlans {get;}=[];
 public ICollectionView LibraryView {get;private set;}=null!;
 private string libraryFolder="Not yet flown",librarySearch="";
 private MissionCard? selectedMissionCard;
 public string LibraryFolder {get=>libraryFolder;set{Set(ref libraryFolder,value);LibraryView.Refresh();Notify(nameof(LibraryFolderTitle));SelectedMissionCard=LibraryView.Cast<MissionCard>().FirstOrDefault();}}
 public string LibraryFolderTitle=>LibraryFolder;
 public string LibrarySearch {get=>librarySearch;set{Set(ref librarySearch,value);LibraryView.Refresh();SelectedMissionCard=LibraryView.Cast<MissionCard>().FirstOrDefault();}}
 public MissionCard? SelectedMissionCard {get=>selectedMissionCard;set{Set(ref selectedMissionCard,value);SelectedSortie=value?.Mission;Notify(nameof(CanArchiveMission));Notify(nameof(CanRestoreMission));Notify(nameof(CanFlyLibraryMission));Notify(nameof(LibraryActivity));Notify(nameof(FavouriteActionLabel));}}
 public string LibraryActivity=>SelectedMissionCard?.Activity??"";
 public string NewFolderLabel=>$"Not yet flown ({MissionCards.Count(c=>c.Folder=="Not yet flown")})";
 public string FlownFolderLabel=>$"Previously flown ({MissionCards.Count(c=>c.Folder=="Previously flown")})";
 public string ArchivedFolderLabel=>$"Archived ({MissionCards.Count(c=>c.Archived)})";
 public bool CanFlyLibraryMission=>SelectedMissionCard is {Archived:false} && !HasActiveSortie;
 public bool CanArchiveMission=>SelectedMissionCard is {Archived:false,Active:false};
 public bool CanRestoreMission=>SelectedMissionCard is {Archived:true,Active:false};
 public ICommand NewFolderCommand {get;private set;}=null!;
 public ICommand FlownFolderCommand {get;private set;}=null!;
 public ICommand ArchivedFolderCommand {get;private set;}=null!;
 public ICommand ArchiveMissionCommand {get;private set;}=null!;
 public ICommand RestoreMissionCommand {get;private set;}=null!;
 public ICommand SimulatorToggleCommand {get;private set;}=null!;
 public ICommand NextFlightActionCommand {get;private set;}=null!;
 public string SimulatorActionLabel=>SimulatorIsConnected?"Disconnect":"Connect simulator";
 public string NextFlightActionLabel=>IsTracking?"Finish flight":!HasActiveSortie?"Activate & brief":!BriefingSigned?"Sign briefing":"Start tracking";
 public bool ShowNextFlightAction=>HasActiveSortie;
 public bool ShowAircraftCheck=>HasActiveSortie&&!IsTracking&&BriefingSigned;
 public bool ShowRetrySync=>recovery is not null&&recovery.Uploaded<recovery.Points.Count&&!uploading&&UploadStatus.StartsWith("Upload paused");
 public string DeactivateLabel=>IsTracking?"Abort flight":"Deactivate mission";
 public string SimulatorReadyLabel=>SimulatorIsConnected?"● Simulator connected":"○ Simulator disconnected";
 public string BriefingReadyLabel=>BriefingSigned?"● Briefing signed":"○ Briefing unsigned";
 public string AircraftReadyLabel=>AircraftChecked?"● Aircraft checked":"○ Aircraft unchecked";
 private void InitializeLibrary()
 {
  LibraryView=new ListCollectionView(MissionCards){Filter=o=>o is MissionCard c&&c.Folder==LibraryFolder&&(LibrarySearch.Length==0||$"{c.Title} {c.Squadron} {c.Route} {c.Tasks}".Contains(LibrarySearch,StringComparison.OrdinalIgnoreCase))};
  NewFolderCommand=new RelayCommand(()=>LibraryFolder="Not yet flown");FlownFolderCommand=new RelayCommand(()=>LibraryFolder="Previously flown");ArchivedFolderCommand=new RelayCommand(()=>LibraryFolder="Archived");
  ArchiveMissionCommand=new AsyncCommand(()=>RunAsync(()=>ChangeArchiveAsync(true)),()=>!IsBusy&&CanArchiveMission);
  RestoreMissionCommand=new AsyncCommand(()=>RunAsync(()=>ChangeArchiveAsync(false)),()=>!IsBusy&&CanRestoreMission);
  SimulatorToggleCommand=new RelayCommand(()=>{var c=SimulatorIsConnected?DisconnectSimulatorCommand:ConnectSimulatorCommand;if(c.CanExecute(null))c.Execute(null);},()=>!IsBusy&&(SimulatorIsConnected?DisconnectSimulatorCommand:ConnectSimulatorCommand).CanExecute(null));
  NextFlightActionCommand=new RelayCommand(()=>{var c=NextFlightCommand();if(c.CanExecute(null))c.Execute(null);},()=>!IsBusy&&NextFlightCommand().CanExecute(null));
 }
 private ICommand NextFlightCommand()=>IsTracking?SubmitDebriefCommand:!HasActiveSortie?ActivateAndBriefCommand:!BriefingSigned?ViewBriefingCommand:StartLiveTrackingCommand;
 private async Task ChangeArchiveAsync(bool archived)
 {
  var c=SelectedMissionCard??throw new InvalidOperationException("Choose a mission.");
  await api.SetMissionArchivedAsync(c.Mission.Id,archived);await RefreshAsync();LibraryFolder=archived?"Archived":c.TimesFlown>0?"Previously flown":"Not yet flown";
  SelectedMissionCard=MissionCards.FirstOrDefault(x=>x.PlanId==c.PlanId);Message=archived?"Mission archived. Restore it whenever you want to fly it again.":"Mission restored.";
 }
 private void RebuildLibrary(IReadOnlyList<MissionArchive> archives)
 {
  LoadFavourites();
  var selected=SelectedMissionCard?.PlanId;var operation=OperationMission?.MissionPlanId??OperationMission?.Id;
  var archived=archives.Select(a=>a.PlanId).ToHashSet();
  var cards=Sorties.GroupBy(s=>s.MissionPlanId??s.Id).Select(g=>{
   var mission=g.OrderByDescending(s=>s.Status is "briefed" or "airborne").ThenByDescending(s=>s.Status=="planned").ThenByDescending(s=>s.CreatedAt).First();
   var flown=g.Where(s=>s.Status=="completed").ToList();
   return new MissionCard(mission,g.Key,$"{Bases.FirstOrDefault(b=>b.Id==mission.DepartureBaseId)?.Icao} → {Bases.FirstOrDefault(b=>b.Id==mission.ArrivalBaseId)?.Icao}",TaskSummary(mission),flown.Count,flown.Select(s=>s.EndedAt??s.CreatedAt).Cast<DateTimeOffset?>().Max(),archived.Contains(g.Key),g.Any(s=>s.Status is "briefed" or "airborne")){Favourite=favourites.Contains(g.Key)};
  }).OrderByDescending(c=>favourites.Contains(c.PlanId)).ThenByDescending(c=>c.Active).ThenBy(c=>c.Title).ToList();
  Replace(MissionCards,cards);Replace(OperationPlans,cards.Where(c=>!c.Archived).Select(c=>c.Mission));
  OperationMission=OperationPlans.FirstOrDefault(s=>(s.MissionPlanId??s.Id)==operation)??OperationPlans.FirstOrDefault();
  LibraryView.Refresh();SelectedMissionCard=cards.FirstOrDefault(c=>c.PlanId==selected&&c.Folder==LibraryFolder)??LibraryView.Cast<MissionCard>().FirstOrDefault();
  foreach(var n in new[]{nameof(NewFolderLabel),nameof(FlownFolderLabel),nameof(ArchivedFolderLabel)})Notify(n);
 }
 private void NotifyLibraryReadiness()
 {
  foreach(var n in new[]{nameof(CanFlyLibraryMission),nameof(SimulatorActionLabel),nameof(NextFlightActionLabel),nameof(ShowNextFlightAction),nameof(ShowAircraftCheck),nameof(ShowRetrySync),nameof(DeactivateLabel),nameof(SimulatorReadyLabel),nameof(BriefingReadyLabel),nameof(AircraftReadyLabel)})Notify(n);
 }
}
