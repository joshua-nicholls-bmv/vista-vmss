using System.Windows.Input;
using Vista.Core;
namespace Vista.Desktop;
public sealed partial class MainViewModel
{
 private string arrivalSelection="Choose airport";
 private bool resolvingMissionArrival;
 public string[] ArrivalSelections {get;}=["Choose airport","Assign from mission","Return to departure"];
 public string ArrivalSelection {get=>arrivalSelection;set{if(!ArrivalSelections.Contains(value)||!Set(ref arrivalSelection,value))return;ResolveMissionArrival();CompileBuilderPreview();NotifyArrivalSelection();}}
 public bool ChooseArrivalAirport=>ArrivalSelection=="Choose airport";
 public string ArrivalAssignment=>Arrival is not null?$"{Arrival.Icao} · {Arrival.Name}":ArrivalSelection=="Assign from mission"?"Destination awaiting mission assignment":"Choose a departure airport.";
 public string GenerateMissionLabel=>BuilderTasks.Count==0?"Generate mission":"Regenerate mission";
 public ICommand GenerateMissionCommand {get;private set;}=null!;
 private void NotifyArrivalSelection(){Notify(nameof(ChooseArrivalAirport));Notify(nameof(ArrivalAssignment));Notify(nameof(GenerateMissionLabel));}
 private void ResolveMissionArrival()
 {
  if(resolvingMissionArrival)return;
  resolvingMissionArrival=true;
  try
  {
   if(ArrivalSelection=="Return to departure")
   {if(Arrival?.Id!=Departure?.Id)ArrivalIcao=Departure?.Icao??"";}
   else if(ArrivalSelection=="Assign from mission")
   {
    var destinations=BuilderTasks.Where(t=>t.Option.DestinationBaseId is not null).ToList();
    var selected=destinations.Count==1?Bases.FirstOrDefault(b=>b.Id==destinations[0].Option.DestinationBaseId):BuilderTasks.Count>0&&destinations.Count==0?Departure:null;
    if(Arrival?.Id!=selected?.Id||selected is null&&ArrivalIcao.Length>0)ArrivalIcao=selected?.Icao??"";
   }
  }
  finally{resolvingMissionArrival=false;NotifyArrivalSelection();}
 }
 private void GenerateMission()
 {
  if(missionData is null||SelectedAircraft is null||Departure is null)throw new InvalidOperationException("Choose an aircraft and a recognised departure first.");
  var requested=BuilderTasks.Count>0?BuilderTasks.ToList():BuilderFamily is not null?new List<MissionTaskRow>{new(BuilderOption??new CatalogueOption(),BuilderFamily,null)}:throw new InvalidOperationException("Choose a mission type first.");
  var generated=new List<MissionTaskRow>();
  foreach(var task in requested)
  {
   var family=missionData.Families.FirstOrDefault(f=>f.Code==task.Family.Code&&f.Enabled);
   if(family is null||!family.DepartureIcaos.Contains(Departure.Icao)||!missionData.AircraftTypes.Any(t=>t.FamilyCode==family.Code&&t.AircraftTypeId==SelectedAircraft.Aircraft.AircraftTypeId))throw new InvalidOperationException("The aircraft or departure does not support this mission type.");
   var options=missionData.Options.Where(o=>o.Enabled&&o.FamilyCode==family.Code&&(o.ElementId is null||Elements.Any(e=>e.Id==o.ElementId&&e.Active))&&(o.DestinationBaseId is null||Bases.Any(b=>b.Id==o.DestinationBaseId&&b.Active))).ToList();
   var option=ChooseDifferent(options,task.Option)??throw new InvalidOperationException("No enabled mission locations or destinations remain.");
   if(ArrivalSelection=="Return to departure"&&option.DestinationBaseId is Guid dest&&dest!=Departure.Id)throw new InvalidOperationException("This mission flies to a destination. Select Assign from mission or Choose airport.");
   var loads=missionData.Payloads.Where(p=>p.Enabled&&missionData.FamilyPayloads.Any(f=>f.FamilyCode==family.Code&&f.PayloadId==p.Id)&&(!missionData.PayloadDestinations.Any(d=>d.PayloadId==p.Id)||missionData.PayloadDestinations.Any(d=>d.PayloadId==p.Id&&d.BaseId==(option.DestinationBaseId??Departure.Id)))).ToList();
   var payload=ChooseDifferent(loads,task.Payload);
   if(missionData.FamilyPayloads.Any(f=>f.FamilyCode==family.Code)&&payload is null)throw new InvalidOperationException("No enabled compatible loads remain for this destination.");
   generated.Add(new(option,family,payload));
  }
  if(generated.Count(t=>t.Option.DestinationBaseId is not null)>1)throw new InvalidOperationException("Use one destination task with any compatible training tasks.");
  Replace(BuilderTasks,generated);BuilderTask=BuilderTasks.FirstOrDefault();
  if(ArrivalSelection=="Choose airport"&&generated.FirstOrDefault(t=>t.Option.DestinationBaseId is not null)?.Option.DestinationBaseId is Guid id)Arrival=Bases.First(b=>b.Id==id);
  ResolveMissionArrival();
  if(string.IsNullOrWhiteSpace(PlanTitle))PlanTitle=generated[0].Family.Title;
  CompileBuilderPreview();PlannerStage=2;Message="Mission generated. Review its assigned destination, load and route before saving.";
 }
}
