using System.IO;
using System.Text.Json;
using System.Windows.Input;
using Vista.Core;
namespace Vista.Desktop;
public sealed partial class MainViewModel
{
 public bool SuppressPlannerPrompts {get;set;}
 public event Action<Action>? PlannerLeaveRequested;
 private string plannerBaseline="";
 private HashSet<Guid> favourites=[];
 public bool PlannerDirty=>plannerBaseline.Length>0&&PlannerFingerprint()!=plannerBaseline;
 private string PlannerFingerprint()=>JsonSerializer.Serialize(new{draftId,PlanTitle,FlightCallsign,RouteText,Aircraft=SelectedAircraft?.Id,DepartureIcao,ArrivalIcao,AlternateIcao,ArrivalSelection,CorridorOutbound,CorridorReturn,Tasks=BuilderTasks.Select(t=>new{t.Option.Id,Payload=t.Payload?.Id}),Points=Waypoints.Select(p=>p.ToPlan()).ToList()});
 private void MarkPlannerSaved()=>plannerBaseline=PlannerFingerprint();
 public void RequestPlannerExit(Action continuation)
 {
  if(!SuppressPlannerPrompts&&IsSignedIn&&SelectedTab==1&&PlannerDirty&&PlannerLeaveRequested is not null)PlannerLeaveRequested(continuation);else continuation();
 }
 public void NavigateAfterPlannerPrompt(int tab){Set(ref selectedTab,tab,nameof(SelectedTab));}
 public async Task<bool> SaveBeforeLeavingAsync()
 {
  var success=false;await RunAsync(async()=>{if(BuilderTasks.Count>0||catalogueDraft)await SaveMissionPlanAsync();else await SaveAsync();MarkPlannerSaved();success=true;});return success;
 }
 public void DiscardPlannerChanges()=>NewPlan();
 public bool TaskNeedsLoad=>BuilderFamily is not null&&missionData?.FamilyPayloads.Any(f=>f.FamilyCode==BuilderFamily.Code)==true;
 public bool CanReviewTasks=>BuilderTasks.Count>0&&ValidateBuilder() is null;
 public string ReviewTasksLabel=>$"Review mission — {BuilderTasks.Count} task{(BuilderTasks.Count==1?"":"s")}";
 public string TasksHeading=>$"Tasks in this mission ({BuilderTasks.Count})";
 public string PlannerBriefingReminder=>PlannerDirty?"Plan changed · save, then generate and import a fresh SimBrief briefing before flight.":"Review the current SimBrief briefing before signing for flight.";
 public string PlannerChecklist
 {
  get
  {
   var issues=new List<string>();
   if(SelectedAircraft is null)issues.Add("Choose an available aircraft.");
   if(Departure is null)issues.Add("Recognise the departure airport.");
   if(Arrival is null)issues.Add("Assign or choose the arrival airport.");
   if(string.IsNullOrWhiteSpace(PlanTitle))issues.Add("Enter a mission name.");
   if(string.IsNullOrWhiteSpace(FlightCallsign))issues.Add("Enter a flight callsign.");
   var error=ValidateBuilder();if(error is not null&&!issues.Contains(error))issues.Add(error);
   return issues.Count==0?"✓ Mission ready to save":string.Join("\n",issues.Select(v=>"• "+v));
  }
 }
 public string PlannerDistance
 {
  get
  {
   if(Departure?.Latitude is not decimal dlat||Departure.Longitude is not decimal dlon||Arrival?.Latitude is not decimal alat||Arrival.Longitude is not decimal alon)return "Distance unavailable · airport coordinates missing";
   var points=BuilderTasks.Count>0?BuilderRoute.Select(p=>(p.Latitude,p.Longitude)).ToList():Waypoints.Select(p=>(p.Latitude,p.Longitude)).ToList();
   if(points.Count==0&&Departure.Id==Arrival.Id)return "Add tasks to estimate mission distance";
   points.Insert(0,(dlat,dlon));points.Add((alat,alon));double total=0;
   for(int i=1;i<points.Count;i++){var a=points[i-1];var b=points[i];double la=(double)a.Item1*Math.PI/180,lb=(double)b.Item1*Math.PI/180,dl=(double)(b.Item1-a.Item1)*Math.PI/180,dn=(double)(b.Item2-a.Item2)*Math.PI/180;var h=Math.Pow(Math.Sin(dl/2),2)+Math.Cos(la)*Math.Cos(lb)*Math.Pow(Math.Sin(dn/2),2);total+=3440.065*2*Math.Asin(Math.Sqrt(Math.Clamp(h,0,1)));}
   return $"Estimated route distance: {total:N0} NM\nDirect legs through planned points · SimBrief may differ";
  }
 }
 public ICommand ReturnToBaseCommand {get;private set;}=null!;
 public ICommand ToggleFavouriteCommand {get;private set;}=null!;
 public ICommand FixAircraftCommand {get;private set;}=null!;
 public ICommand FixTasksCommand {get;private set;}=null!;
 public ICommand FixDetailsCommand {get;private set;}=null!;
 public string FavouriteActionLabel=>SelectedMissionCard is not null&&favourites.Contains(SelectedMissionCard.PlanId)?"★ Remove favourite":"☆ Add favourite";
 public bool SelectedMissionFavourite=>SelectedMissionCard is not null&&favourites.Contains(SelectedMissionCard.PlanId);
 private string FavouritePath=>Path.Combine(SettingsDirectory,$"favourites-{pilot?.Id}.json");
 private void InitializePlannerEase()
 {
  ReturnToBaseCommand=new RelayCommand(()=>{if(BuilderTasks.Any(t=>t.Option.DestinationBaseId is not null)){Message="Remove the destination task before choosing Return to base.";return;}ArrivalSelection="Return to departure";Message="Final arrival set to the departure base.";},()=>!IsBusy&&Departure is not null);
  ToggleFavouriteCommand=new AsyncCommand(()=>RunAsync(()=>{var card=SelectedMissionCard??throw new InvalidOperationException("Choose a mission.");if(!favourites.Add(card.PlanId))favourites.Remove(card.PlanId);Directory.CreateDirectory(SettingsDirectory);File.WriteAllText(FavouritePath,JsonSerializer.Serialize(favourites));ReorderFavourites();Notify(nameof(FavouriteActionLabel));return Task.CompletedTask;}),()=>!IsBusy&&SelectedMissionCard is not null);
  FixAircraftCommand=new RelayCommand(()=>PlannerStage=0);FixTasksCommand=new RelayCommand(()=>PlannerStage=1);FixDetailsCommand=new RelayCommand(()=>PlannerStage=2);
  PropertyChanged+=(_,e)=>{if(e.PropertyName is nameof(PlanTitle) or nameof(FlightCallsign) or nameof(SelectedAircraft) or nameof(AlternateIcao))NotifyPlannerEase();};
 }
 private void LoadFavourites(){try{favourites=File.Exists(FavouritePath)?JsonSerializer.Deserialize<HashSet<Guid>>(File.ReadAllText(FavouritePath))??[]:[];}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){favourites=[];}}
 private void ReorderFavourites()
 {
  var selected=SelectedMissionCard?.PlanId;var operation=OperationMission?.Id;
  Replace(MissionCards,MissionCards.Select(c=>c with{Favourite=favourites.Contains(c.PlanId)}).OrderByDescending(c=>favourites.Contains(c.PlanId)).ThenByDescending(c=>c.Active).ThenBy(c=>c.Title).ToList());
  Replace(OperationPlans,MissionCards.Where(c=>!c.Archived).Select(c=>c.Mission));SelectedMissionCard=MissionCards.FirstOrDefault(c=>c.PlanId==selected);OperationMission=OperationPlans.FirstOrDefault(s=>s.Id==operation)??OperationPlans.FirstOrDefault();
 }
 private void NotifyPlannerEase(){foreach(var n in new[]{nameof(TaskNeedsLoad),nameof(CanReviewTasks),nameof(ReviewTasksLabel),nameof(TasksHeading),nameof(PlannerChecklist),nameof(PlannerDistance),nameof(PlannerBriefingReminder),nameof(FavouriteActionLabel)})Notify(n);}
}
