using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Vista.Core;
using Vista.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Vista.Desktop.App();
        application.InitializeComponent();
        if(Environment.GetCommandLineArgs().Contains("--map-smoke"))
        {
            MapTiles.CacheDirectory="work/map-smoke-cache";
            var tile=Task.Run(()=>MapTiles.Load(6,31,20)).GetAwaiter().GetResult();
            Assert(tile is not null&&tile.PixelWidth==256&&tile.PixelHeight==256,"live visible UK map tile loads and decodes");
            var file=Directory.GetFiles(MapTiles.CacheDirectory,"*.png",SearchOption.AllDirectories).Single();var written=File.GetLastWriteTimeUtc(file);
            var cached=Task.Run(()=>MapTiles.Load(6,31,20)).GetAwaiter().GetResult();Assert(cached is not null&&File.GetLastWriteTimeUtc(file)==written,"repeated map tile served from disk cache");
            Console.WriteLine("PASS: one UK map tile downloaded, decoded and reused from cache.");return;
        }
        Directory.CreateDirectory("work");
        MapTiles.Enabled=false;
        Assert(typeof(MainViewModel).Assembly.GetType("Vista.Desktop.MissionVoice") is null,"speech service removed");
        BriefingPdf.Write("work/vista-long-briefing.pdf","Long mission briefing","DRAFT",new[]{new BriefingPdfSection("Exercise notes",string.Join("\n",Enumerable.Range(1,180).Select(i=>$"Task {i} - Review the aircraft, route, fuel and mission instructions before departure."))),new BriefingPdfSection("Long route token",new string('A',500))});
        CheckTracking();
        var store = new WindowsLoginStore("work/test-login-store"); store.Clear();
        store.Save(new("https://example.test", "TEST", "private-fixture-refresh"));
        Assert(store.Load()?.RefreshToken == "private-fixture-refresh", "DPAPI round trip");
        Assert(!Encoding.UTF8.GetString(File.ReadAllBytes("work/test-login-store/login.dat")).Contains("private-fixture-refresh"), "DPAPI contains no plaintext token");
        File.WriteAllBytes("work/test-login-store/login.dat", [1,2,3]);
        Assert(store.Load() is null && !File.Exists("work/test-login-store/login.dat"), "corrupt credential safely removed");
        using var handler = new FakeApi();
        using var api = new SupabaseClient(new() { SupabaseUrl="https://dlqfpbqenqumdlogfrxo.supabase.co",PublishableKey="sb_publishable_test" },handler,store);
        var vm = new MainViewModel(api) {SuppressPlannerPrompts=true,RecoveryDirectory="work/test-flight-recovery", SettingsDirectory="work/test-pilot-settings"};
        var window = new MainWindow(vm);
        if(Directory.Exists("work/test-pilot-settings"))Directory.Delete("work/test-pilot-settings",true);
        window.Show();
        Render(window, "work/wpf-login.png");
        vm.Identifier = "69EAW-001";
        vm.RememberLogin = true;
        vm.SignInAsync("test-password").GetAwaiter().GetResult();
        Assert(vm.IsSignedIn && vm.FleetCount==62,"login/catalogue flow");
        Assert(handler.RefreshCount==1,"refresh token used once before authenticated reads");
        Assert(store.Load()?.RefreshToken == "test-refresh-1", "latest rotated token remembered");
        using (var restoredHandler = new FakeApi())
        using (var restoredApi = new SupabaseClient(new() { SupabaseUrl="https://dlqfpbqenqumdlogfrxo.supabase.co",PublishableKey="sb_publishable_test" },restoredHandler,store))
        {
            var restored = new MainViewModel(restoredApi);
            restored.RestoreLoginAsync().GetAwaiter().GetResult();
            Assert(restored.IsSignedIn && restored.Identifier == "69EAW-001" && restoredHandler.LoginEmail is null, "restart restores via refresh token without password");
            Assert(restoredHandler.ReceivedRefresh == "test-refresh-1", "saved refresh token supplied");
        }
        vm.SelectedTab=0; Render(window,"work/wpf-simulator.png");
        Assert(handler.LoginEmail=="69EAW-001@vista-vmss.com","identifier email normalization");
        Assert(vm.PilotLabel=="Josh - 69EAW-001 - Callsign pending","pilot label");
        vm.FleetSearch="ZM"; Assert(vm.FleetView.Cast<object>().Count()==20,"fleet filtering");
        vm.ClearFleetFilterCommand.Execute(null);
        vm.NewPlanCommand.Execute(null); vm.PlanTitle="Test planning"; vm.FlightCallsign="TEST01";
        vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZM400");
        vm.Departure=vm.Bases.First(b=>b.Icao=="EGVN"); vm.Arrival=vm.Bases.First(b=>b.Icao=="EGXC");
        vm.AddPointCommand.Execute(null); vm.Waypoints[0].Identifier="TEST1"; vm.Waypoints[0].Latitude=52; vm.Waypoints[0].Longitude=-1;
        vm.SaveCommand.Execute(null);
        Assert(handler.LastPlan is not null,"save invoked");
        Assert(handler.LastPlan!.Value.GetProperty("p_plan").GetProperty("aircraft_id").GetGuid()==vm.SelectedAircraft.Id,"RPC plan serialization");
        Assert(vm.Message.Contains("saved"),"save feedback");
        CheckMissionWorkflow(vm,handler,window);
        CheckBuilder(vm,handler,window);
        CheckLiveWorkflow(vm,handler,window);
        CheckArrivalAssignment(vm,handler,window);
        CheckPlannerEase(vm,handler,window);
        CheckLibrary(vm,handler,window);
        CheckSharingAndFleet(vm,handler,window);
        vm.SelectedTab=0; Render(window,"work/wpf-overview.png");
        Render(window,"work/wpf-overview-compact.png",1200,700);
        vm.SelectedTab=1; Render(window,"work/wpf-planner.png");
        vm.SelectedTab=3; Render(window,"work/wpf-fleet.png");
        vm.SelectedTab=2; Render(window,"work/wpf-sorties.png");
        var point=WaypointRow.FromStored(new() { Id=Guid.NewGuid(),Identifier="ELEMENT",Latitude=52,Longitude=-1 },true,Guid.NewGuid());
        Assert(point.ToPlan().SourceElementWaypointId is not null,"element lineage");
        point.Latitude=53; Assert(point.ToPlan().SourceElementWaypointId is null,"edited element becomes custom");
        vm.SignOutCommand.Execute(null); Assert(!vm.IsSignedIn && vm.FleetCount==0,"sign-out clears profile and catalogue");
        Assert(store.Load() is null, "sign-out forgets protected session");
        vm.RememberLogin=false; vm.SignInAsync("test-password").GetAwaiter().GetResult();
        Assert(store.Load() is null, "unchecked login remains in memory only");
        vm.SignOutCommand.Execute(null);
        store.Save(new("https://dlqfpbqenqumdlogfrxo.supabase.co", "69EAW-001", "expired-token"));
        handler.RejectRefresh=true; vm.RestoreLoginAsync().GetAwaiter().GetResult();
        Assert(!vm.IsSignedIn && store.Load() is null && vm.Message.Contains("expired"), "revoked saved login forgotten");
        handler.RejectRefresh=false;
        store.Save(new("https://dlqfpbqenqumdlogfrxo.supabase.co", "69EAW-001", "offline-token"));
        handler.Offline=true; vm.RestoreLoginAsync().GetAwaiter().GetResult();
        Assert(!vm.IsSignedIn && store.Load()?.RefreshToken=="offline-token", "network failure preserves credential for retry");
        handler.Offline=false;
        store.Save(new("https://other-project.test", "TEST", "wrong-project-token"));
        Assert(api.ReadRememberedLogin() is null && store.Load() is null, "project isolation");
        vm.Identifier="69EAW-001"; vm.SignInAsync("test-password").GetAwaiter().GetResult();
        vm.ConnectSimulatorCommand.Execute(null);
        Assert(!string.IsNullOrWhiteSpace(vm.SimulatorStatus), "native SimConnect load and connect attempt");
        vm.DisconnectSimulatorCommand.Execute(null); vm.SignOutCommand.Execute(null);
        try { new SupabaseClient(new() { SupabaseUrl="https://example.test",PublishableKey="sb_secret_invalid" }); throw new Exception("secret key accepted"); }
        catch(ArgumentException) { }
        Console.WriteLine("PASS: ICAO recognition, mission/aircraft filtering, route previews, stale-response protection, explicit adoption, custom conversion and save validation; tracking/login regressions; WPF rendering");
        window.Close(); application.Shutdown();
    }
    private static void CheckMissionWorkflow(MainViewModel vm, FakeApi handler, MainWindow window)
    {
        vm.DepartureIcao=" egvn "; vm.ArrivalIcao="egxc";
        Assert(vm.DepartureIcao=="EGVN" && vm.DepartureRecognition=="RAF Brize Norton" && vm.Arrival?.Icao=="EGXC","typed ICAOs normalised and resolved");
        Assert(vm.MatchingMissions.Count==2,"pair and aircraft type/squadron matching");
        vm.SelectedMission=vm.MatchingMissions.First();vm.MissionPreviewTask.GetAwaiter().GetResult();
        Assert(vm.MissionPreview.Count==2 && vm.MissionPreview[0].Position==1,"ordered mission preview");
        Assert(vm.Waypoints.Count==1 && vm.Waypoints[0].Identifier=="TEST1" && !vm.IsPredefined,"preview does not overwrite custom route");
        vm.SelectedTab=1;Render(window,"work/wpf-icao-planner.png");
        vm.UseMissionCommand.Execute(null);
        Assert(vm.IsPredefined && vm.Waypoints.Count==2,"explicit mission adoption");
        vm.SaveCommand.Execute(null);
        Assert(handler.LastPlan!.Value.GetProperty("p_plan").GetProperty("source").GetString()=="predefined","predefined save source");
        vm.DepartureIcao="EGQ";
        Assert(vm.Departure is null && vm.MatchingMissions.Count==0 && vm.Waypoints.Count==0 && !vm.IsPredefined,"airport change invalidates loaded mission");
        vm.DepartureIcao="EGVN";vm.ArrivalIcao="ZZZZ";
        Assert(vm.Arrival is null && vm.ArrivalRecognition.Contains("catalogue"),"unknown ICAO reported");
        vm.ArrivalIcao="EGXC"; vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZK345");
        Assert(vm.MatchingMissions.Count==2 && vm.MatchingMissions.All(m=>m.AircraftTypeId!=handler.AtlasType),"Typhoon route filtering");
        vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZM400");
        var candidates=vm.MatchingMissions.ToList();
        handler.DelayedMission=candidates[0].Id;
        vm.SelectedMission=candidates[0];var oldTask=vm.MissionPreviewTask;
        vm.SelectedMission=candidates[1];vm.MissionPreviewTask.GetAwaiter().GetResult();
        var latest=vm.MissionPreview[0].Identifier;
        handler.CompleteDelayed();oldTask.GetAwaiter().GetResult();
        Assert(vm.MissionPreview[0].Identifier==latest,"stale background preview discarded");
        handler.DelayedMission=null;
        vm.UseMissionCommand.Execute(null);vm.CustomRouteCommand.Execute(null);
        Assert(vm.IsCustom && vm.Waypoints.Count==2,"mission points can become custom route");
        vm.ArrivalIcao="EGQS";Assert(vm.Waypoints.Count==2,"custom points preserved on airport change");
        vm.AlternateIcao="ZZZZ";var saves=handler.SaveCount;vm.SaveCommand.Execute(null);
        Assert(handler.SaveCount==saves && vm.Message.Contains("alternate"),"unknown alternate blocks save");
        vm.AlternateIcao="";vm.SaveCommand.Execute(null);Assert(handler.SaveCount==saves+1,"custom flight without matching mission saves");
        vm.NewPlanCommand.Execute(null);Assert(vm.DepartureIcao=="" && vm.ArrivalIcao=="" && vm.MatchingMissions.Count==0,"new plan clears typed airports");
        var draft=new Sortie{Id=Guid.NewGuid(),PilotId=Guid.NewGuid(),AircraftId=vm.AvailableAircraft.First(a=>a.Serial=="ZM400").Id,
            Source="predefined",MissionId=candidates[1].Id,Title="Reopened draft",Callsign="TEST02",Status="planned",
            DepartureBaseId=vm.Bases.First(b=>b.Icao=="EGVN").Id,ArrivalBaseId=vm.Bases.First(b=>b.Icao=="EGXC").Id};
        vm.SelectedSortie=draft;vm.EditDraftCommand.Execute(null);
        Assert(vm.DepartureIcao=="EGVN" && vm.ArrivalIcao=="EGXC" && vm.IsPredefined && vm.Waypoints.Count==2 && vm.SelectedMission?.Id==candidates[1].Id,"draft reopen restores typed airports and mission");
        vm.SelectedSortie=draft with {Source="custom",MissionId=null,DepartureBaseId=Guid.NewGuid()};vm.EditDraftCommand.Execute(null);
        Assert(vm.DepartureIcao=="" && vm.Departure is null,"missing draft airport does not retain previous input");
    }
    private static void CheckBuilder(MainViewModel vm,FakeApi handler,MainWindow window)
    {
        vm.NewPlanCommand.Execute(null);vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZM400");
        Assert(vm.BuilderFamilies.Count==6,"Atlas mission families");
        vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="AMF-AIRDROP");
        vm.BuilderOption=vm.BuilderOptions.First();vm.PayloadKind="Personnel";
        Assert(vm.BuilderPayloads.Count==3,"drop personnel load filter");
        var priorOption=vm.BuilderOption;vm.RandomiseAllCommand.Execute(null);
        Assert(vm.BuilderOption!=priorOption&&vm.BuilderPayloads.Contains(vm.BuilderPayload!)&&vm.BuilderTasks.Count==0,"randomise all changes location and compatible load without adding a task");
        vm.PlannerStage=1;vm.SelectedTab=1;foreach(var scroll in Descendants(window).OfType<System.Windows.Controls.ScrollViewer>().Where(v=>v.ScrollableHeight>300))scroll.ScrollToVerticalOffset(360);
        Render(window,"work/wpf-airdrop-randomise.png");
        foreach(var scroll in Descendants(window).OfType<System.Windows.Controls.ScrollViewer>())scroll.ScrollToTop();
        vm.BuilderPayload=vm.BuilderPayloads.First(p=>p.Code=="DROP-PAX-20");vm.AddMissionTaskCommand.Execute(null);
        vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="AMF-LFA7");vm.BuilderOption=vm.BuilderOptions.Single();vm.AddMissionTaskCommand.Execute(null);
        vm.CorridorOutbound=true;vm.CorridorReturn=true;
        Assert(vm.BuilderTasks.Count==2&&vm.BuilderRoute.Count==12,"combined training and corridor preview");
        Assert(vm.BuilderTotals.Contains("2,000")&&vm.BuilderTotals.Contains("20 personnel"),"load total includes people once");
        var lowTask=vm.BuilderTasks[1];var old=vm.BuilderTasks[0].Option.Id;
        vm.BuilderTask=vm.BuilderTasks[0];vm.RerollMissionTaskCommand.Execute(null);
        Assert(vm.BuilderTasks[0].Option.Id!=old&&vm.BuilderTasks[1]==lowTask,"reroll only selected task");
        vm.MoveMissionDownCommand.Execute(null);Assert(vm.BuilderTasks[0]==lowTask,"reorder combined tasks");
        vm.MoveMissionUpCommand.Execute(null);vm.FlightCallsign="TEST69";vm.SaveMissionPlanCommand.Execute(null);
        Assert(handler.LastCataloguePlan.HasValue&&vm.Message.Contains("saved"),"composed save RPC");
        var request=handler.LastCataloguePlan!.Value.GetProperty("p_plan");
        Assert(request.GetProperty("tasks").GetArrayLength()==2&&request.GetProperty("corridor_return").GetBoolean(),"save retains tasks and corridor flags");
        Assert(vm.BuilderOption is not null&&vm.BuilderPayload is not null,"refresh retains mission picker selections");
        var snapshot=JsonSerializer.SerializeToElement(new{planning_mode="catalogue",catalogue_plan=new{corridor_outbound=true,corridor_return=true,tasks=vm.BuilderTasks.Select(t=>new{option_id=t.Option.Id,family_code=t.Family.Code,payload_id=t.Payload?.Id})}},SupabaseClient.JsonOptions);
        vm.SelectedSortie=new Sortie{Id=Guid.NewGuid(),AircraftId=vm.SelectedAircraft!.Id,Source="custom",Title=vm.PlanTitle,Callsign="TEST69",Status="planned",DepartureBaseId=vm.Departure!.Id,ArrivalBaseId=vm.Arrival!.Id,PlanSnapshot=snapshot};
        vm.EditDraftCommand.Execute(null);Assert(vm.BuilderTasks.Count==2&&vm.BuilderRoute.Count==12&&vm.CorridorReturn,"saved draft restores task and corridor selections");
        vm.SaveCommand.Execute(null);Assert(vm.Message.Contains("Save mission plan"),"manual save protects catalogue metadata");
        vm.DepartureIcao="EGXC";Assert(vm.BuilderNotice.Contains("Departure"),"typed airports update validation immediately");
        vm.DepartureIcao="EGVN";vm.SelectedTab=1;Render(window,"work/wpf-mission-builder.png");
        vm.PlannerStage=2;Render(window,"work/wpf-mission-route.png");vm.PlannerStage=0;
        vm.NewPlanCommand.Execute(null);vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZM400");
        vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="AMF-LOGISTICS");vm.BuilderOption=vm.BuilderOptions.First(o=>vm.Bases.First(b=>b.Id==o.DestinationBaseId).Icao=="EGPL");vm.PayloadKind="All loads";
        Assert(vm.BuilderPayloads.Any(p=>p.Code=="CARGO-17")&&!vm.BuilderPayloads.Any(p=>p.Code=="CARGO-01"),"destination-specific logistics loads");
        vm.BuilderPayload=vm.BuilderPayloads.First(p=>p.Code=="CARGO-17");vm.AddMissionTaskCommand.Execute(null);vm.FlightCallsign="TEST70";
        Assert(vm.ArrivalIcao=="EGPL"&&vm.BuilderRoute.Count==0&&vm.BuilderTotals.Contains("4,800"),"direct destination and mixed engineering load");
        vm.SaveMissionPlanCommand.Execute(null);Assert(vm.Message.Contains("saved"),"direct destination save");
        string? directDispatch=null;vm.SimBriefAtlasAirframeId="404036_1780234377464";vm.LaunchDispatch=u=>directDispatch=u;vm.SaveAndDispatchCommand.Execute(null);
        Assert(directDispatch is not null&&directDispatch.Contains("static_id=VISTA_")&&directDispatch.Contains("orig=EGVN")&&directDispatch.Contains("dest=EGPL"),"save and dispatch uses the actual newly saved mission");
        vm.NewPlanCommand.Execute(null);vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZK345");
        Assert(vm.BuilderFamilies.Count==6,"Typhoon mission families");vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="TY-STRIKE");
        vm.BuilderOption=vm.BuilderOptions.First();vm.AddMissionTaskCommand.Execute(null);vm.ArrivalIcao="EGQS";
        Assert(vm.BuilderRoute.Count==2&&vm.BuilderRoute[0].Altitude=="4,000 ft"&&vm.BuilderRoute[0].Speed=="350 kt","strike entry profile");
        vm.SelectedControl=vm.CatalogueControls.First(c=>c.Kind=="option"&&c.Code=="TY-STRIKE-1");vm.ToggleCatalogueCommand.Execute(null);
        Assert(vm.BuilderOptions.All(o=>o.Code!="TY-STRIKE-1")&&vm.CatalogueControls.Any(c=>c.Code=="TY-STRIKE-1"&&!c.Enabled),"catalogue switch refresh excludes disabled options");
        vm.ToggleCatalogueCommand.Execute(null);Assert(vm.BuilderOptions.Count==5,"catalogue switch can re-enable");
        vm.SelectedTab=4;Render(window,"work/wpf-mission-controls.png");
        Console.WriteLine("PASS: mission family/type filtering, payload/destination filtering, combined route order, reroll/reorder, catalogue switches, direct and combined save, typed airport validation.");
    }
    private static void CheckLiveWorkflow(MainViewModel vm,FakeApi handler,MainWindow window)
    {
        vm.SimBriefAtlasAirframeId="";
        var saved=new Sortie{CreatedAt=DateTimeOffset.UtcNow,PlanSnapshot=JsonDocument.Parse("{}").RootElement.Clone(),Id=Guid.NewGuid(),PilotId=handler.PilotId,AircraftId=vm.Fleet.First(a=>a.Serial=="ZM400").Id,Title="Airdrop / low-level sortie",Callsign="TEST69",Status="planned",Source="custom",DepartureBaseId=vm.Bases.First(b=>b.Icao=="EGVN").Id,ArrivalBaseId=vm.Bases.First(b=>b.Icao=="EGVN").Id};
        var picker=new AirportPicker{Airports=vm.Bases};picker.SetBinding(AirportPicker.TextProperty,new System.Windows.Data.Binding("DepartureIcao"){Source=vm,Mode=System.Windows.Data.BindingMode.TwoWay});
        var input=(System.Windows.Controls.TextBox)picker.FindName("Input");input.Text="conin";
        typeof(AirportPicker).GetMethod("Commit",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(picker,null);
        Assert(vm.DepartureIcao=="EGXC"&&vm.Departure?.Icao=="EGXC","airport suggestion commits ICAO through planner binding");
        vm.PlanTitle="Low Level and AARA 8";vm.RandomOptionCommand.Execute(null);Assert(vm.PlanTitle=="Low Level and AARA 8","custom mission name retained through randomisation");
        handler.SetSortie(saved);vm.SelectedSortie=saved;string? url=null;vm.LaunchDispatch=value=>url=value;
        vm.ActivateAndDispatchCommand.Execute(null);
        Assert(vm.ActiveSortie is null&&vm.Message.Contains("Internal ID"),"missing Atlas profile blocks activation");
        vm.SimBriefAtlasAirframeId="404036_1780234377464";
        vm.ActivateAndDispatchCommand.Execute(null);
        Assert(vm.ActiveSortie?.Id==saved.Id&&vm.SelectedTab==0&&url is not null&&url.Contains("route="),"saved mission activates and launches encoded SimBrief route");
        Assert(vm.Sorties.First(s=>s.Id==saved.Id).SquadronLabel==vm.Fleet.First(a=>a.Id==saved.AircraftId).Unit,"saved mission squadron follows selected aircraft");
        var overview=new TrackMap{Route=vm.ActiveRoute,Track=vm.LocalTrackPoints,Departure=vm.ActiveDeparture,Arrival=vm.ActiveArrival,NextPointIndex=0,FollowAircraft=true};
        overview.FitRoute();Assert(!overview.FollowAircraft,"full mission fit exits aircraft follow mode");
        var mapWindow=new Window{Content=overview,Width=1100,Height=700};mapWindow.Show();Render(mapWindow,"work/wpf-full-mission-map.png");mapWindow.Close();
        Assert(SimBriefDispatch.Coordinate(-52.5m,-1.25m)=="523000S0011500W"&&SimBriefDispatch.Coordinate(52.999999m,0)=="530000N0000000E","SimBrief degree/minute/second formatting and carry");
        Assert(SimBriefDispatch.AircraftCode("ATLAS")=="A400"&&SimBriefDispatch.AircraftCode("TYPHOON-FGR4")=="EUFI"&&SimBriefDispatch.AircraftCode("TYPHOON-T3")=="EUFI","automatic fleet aircraft mapping");
        foreach(var pair in new[]{("DREAD 408","408"),("RIDID 321","321"),("DREAD408","408"),("DREAD 008","008")})
        {
            var dispatch=SimBriefDispatch.BuildUrl(saved with{Callsign=pair.Item1},[],"EGVN","EGVN","ZM400","A400","","Josh");
            Assert(dispatch.Contains("airline=RRR")&&dispatch.Contains("fltnum="+pair.Item2),"RRR and callsign numeric flight number");
        }
        try{SimBriefDispatch.FlightNumber("DREAD");throw new Exception("missing flight number accepted");}catch(InvalidOperationException){}
        Assert(url!.Contains("type=404036_1780234377464")&&url.Contains("reg=ZM400")&&url.Contains("airline=RRR")&&url.Contains("fltnum=69"),"actual Atlas dispatch defaults");
        var profileUrl=SimBriefDispatch.BuildUrl(saved,[],"EGVN","EGVN","ZM400","A400","123_456","Josh");
        Assert(profileUrl.Contains("type=123_456")&&!profileUrl.Contains("airframe="),"custom SimBrief internal ID replaces type");
        var typhoon=vm.Fleet.First(a=>a.Serial=="ZK345");
        vm.SelectedSortie=saved with { AircraftId=typhoon.Id, Callsign="RIGID301" };
        vm.OpenSimBriefCommand.Execute(null);
        Assert(vm.Message.Contains("Internal ID"),"missing Typhoon profile gives clear error");
        vm.SimBriefTyphoonAirframeId="404036_1780602917520";
        vm.OpenSimBriefCommand.Execute(null);
        Assert(url!.Contains("type=404036_1780602917520")&&url.Contains("reg="+typhoon.Serial),"Typhoon profile with selected fleet registration");
        vm.SelectedSortie=saved;
        vm.SimBriefPilotId="404036";
        string BriefingJson(string units="kgs",string reg="ZM400",string number="69",bool revision=true)=>JsonSerializer.Serialize(new {fetch=new{status="Success"},origin=new{icao_code="EGVN"},destination=new{icao_code="EGVN"},aircraft=new{reg,name="A400M Atlas"},general=new{flight_number=number,icao_airline="RRR",route="DCT TEST DCT",initial_altitude="14000"},fuel=new{plan_ramp="10000",enroute_burn="2000",taxi="200",reserve="700",alternate_burn="400"},weights=new{est_zfw="60000",est_tow="70000",payload="5000"},weather=new{orig_metar="EGVN TEST METAR",dest_metar="EGVN TEST ARRIVAL"},files=new{directory="https://www.simbrief.com/ofp/flightplans/",pdf=new{link="fixture.pdf"}},times=new{est_time_enroute="3600",est_block="4200"},@params=new{units,static_id=SimBriefImport.StaticId(saved.Id,revision?handler.PlanKey:"")}});
        vm.FetchBriefing=uri=>{Assert(uri.Contains("userid=404036")&&uri.Contains("static_id=VISTA_"+saved.Id.ToString("N")),"mission-specific briefing request");return Task.FromResult(BriefingJson());};
        vm.ImportBriefingCommand.Execute(null);
        Assert(vm.BriefingFlight.Contains("ZM400")&&vm.BriefingFuel.Contains("10,000 kg")&&vm.BriefingRoute=="DCT TEST DCT","matching briefing imported into ACARS");
        Assert(SimBriefImport.Parse(BriefingJson("lbs",revision:false),saved,"EGVN","EGVN","ZM400").RampFuelKg==4535.9m,"briefing pounds converted to kilograms");
        foreach(var json in new[]{BriefingJson(reg:"ZM401"),BriefingJson(number:"70"),BriefingJson("unknown")})
        {try{SimBriefImport.Parse(json,saved,"EGVN","EGVN","ZM400",handler.PlanKey);throw new Exception("mismatched briefing accepted");}catch(InvalidOperationException){}}
        vm.FetchBriefing=uri=>Task.FromResult(BriefingJson(reg:"ZM401"));vm.ImportBriefingCommand.Execute(null);
        Assert(vm.Message.Contains("does not match")&&vm.BriefingFlight.Contains("ZM400"),"wrong briefing rejected while prior match retained");
        vm.FetchBriefing=uri=>Task.FromException<string>(new HttpRequestException("offline briefing"));vm.ImportBriefingCommand.Execute(null);
        Assert(vm.BriefingRoute=="DCT TEST DCT"&&vm.Message.Contains("offline"),"offline refresh preserves briefing");
        Assert(!vm.BriefingSigned&&vm.BriefingCurrent,"import requires fresh acknowledgement");
        Assert(vm.BriefingFuelBreakdown.Contains("700 kg")&&vm.BriefingWeights.Contains("60,000 kg")&&vm.BriefingWeather.Contains("TEST METAR"),"expanded fuel weights and weather");
        vm.MissionNotes="Exercise task notes";Assert(!vm.CanAcknowledge,"unsaved notes block signature");vm.SaveBriefingNotesCommand.Execute(null);
        vm.BeginAcknowledgementCommand.Execute(null);Assert(vm.AcknowledgementOpen,"paperwork confirmation opens");
        Assert(!vm.SignAndCloseBriefingCommand.CanExecute(null),"explicit acknowledgement required");
        var briefingWindow=Application.Current.Windows.OfType<BriefingWindow>().First();Render(briefingWindow,"work/wpf-sign-briefing.png");
        vm.AcknowledgementChecked=true;vm.SignAndCloseBriefingCommand.Execute(null);Assert(vm.BriefingSigned,"signed current briefing enables readiness");
        vm.ViewBriefingCommand.Execute(null);var fullBrief=Application.Current.Windows.OfType<BriefingWindow>().First();vm.ExportBriefingPdf("work/vista-briefing-check.pdf");Assert(File.ReadAllBytes("work/vista-briefing-check.pdf").Take(5).SequenceEqual(Encoding.ASCII.GetBytes("%PDF-")),"PDF export signature");Render(fullBrief,"work/wpf-sortie-briefing.png");fullBrief.Close();
        vm.FetchBriefing=uri=>Task.FromResult(BriefingJson());vm.ImportBriefingCommand.Execute(null);Assert(!vm.BriefingSigned,"reimport requires a new signature");
        vm.BeginAcknowledgementCommand.Execute(null);vm.AcknowledgementChecked=true;vm.SignAndCloseBriefingCommand.Execute(null);
        vm.RefreshCommand.Execute(null);Assert(vm.BriefingSigned&&vm.MissionNotes=="Exercise task notes","signed briefing and notes restore from persisted record");
        var originalKey=handler.PlanKey;handler.PlanKey="abcdef0123456789abcdef0123456789";vm.RefreshCommand.Execute(null);
        Assert(!vm.BriefingCurrent&&vm.BriefingState=="OUT OF DATE"&&!vm.StartLiveTrackingCommand.CanExecute(null),"stale plan disables tracking");handler.PlanKey=originalKey;vm.RefreshCommand.Execute(null);
        Render(window,"work/wpf-simbrief-briefing.png");
        vm.SimBriefPilotId="404036";vm.SavePilotSettingsCommand.Execute(null);
        Assert(File.Exists(Path.Combine(vm.SettingsDirectory,handler.PilotId.ToString("N")+".json")),"pilot-specific settings saved");
        vm.SimBriefPilotId="bad";vm.SavePilotSettingsCommand.Execute(null);Assert(vm.Message.Contains("numeric"),"invalid SimBrief ID rejected");vm.SimBriefPilotId="404036";
        Assert(Math.Abs(MissionProgress.DistanceNm(0,0,1,0)-60.04)<.1&&Math.Abs(MissionProgress.Bearing(0,0,0,1)-90)<.01,"mission distance NM and true bearing");
        if(vm.ActiveRoute.Count>0){var first=vm.ProgressTitle;vm.NextMissionPointCommand.Execute(null);Assert(vm.ProgressCount.StartsWith("1 /"),"manual mission advancement");vm.PreviousMissionPointCommand.Execute(null);Assert(vm.ProgressTitle==first,"previous mission point");}
        var settingsWindow=new Window{Content=new PilotSettingsView(),DataContext=vm};settingsWindow.Show();Render(settingsWindow,"work/wpf-pilot-settings.png");settingsWindow.Close();vm.SelectedTab=0;
        var now=DateTimeOffset.UtcNow;
        SimulatorTelemetry Sample(int sec,bool ground,double speed,bool brake=false,double vs=0)=>new(now.AddSeconds(sec),"Atlas test",ground?50:10000,speed,180,52+sec*.001,-1,ground,brake,true,10000-sec*10,vs,ground?0:9900);
        Assert(vm.SimulatorConnectionLabel=="SIMULATOR DISCONNECTED","disconnected connection badge");
        vm.ProcessSimulatorTelemetry(Sample(-2,true,0,true) with{EngineRunning=false});
        Assert(vm.SimulatorConnectionLabel=="SIMULATOR CONNECTED"&&vm.EngineStatusLabel=="Engines stopped","fresh connected badge and engine state");
        vm.ProcessSimulatorTelemetry(Sample(-1,true,0,true));
        Assert(vm.TrackingEvents.Any(e=>e.Contains("ENGINE START")),"preflight engine start recorded");
        vm.ProcessSimulatorTelemetry(Sample(0,true,0,true));vm.AircraftChecked=true;vm.StartLiveTrackingCommand.Execute(null);
        Render(window,"work/wpf-live-operations.png");
        Assert(vm.TrackingEvents.Any(e=>e.Contains("ENGINE START")),"preflight events preserved when tracking starts");
        Assert(vm.ActiveSortie?.Status=="airborne"&&File.Exists(Path.Combine(vm.RecoveryDirectory,handler.PilotId.ToString("N")+".json")),"tracking start creates durable recovery");
        var missionCatalogue=(MissionCatalogue)typeof(MainViewModel).GetField("missionData",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(vm)!;
        var strikeSource=missionCatalogue.ElementWaypoints.First(p=>p.Identifier=="S1ENTRY");var dropSource=missionCatalogue.ElementWaypoints.First(p=>p.Identifier=="DALTON-IN");
        var originalRoute=vm.ActiveRoute.ToArray();var beforeAlerts=vm.TrackingEvents.Count(e=>e.Contains("ENTRY ALERT"));
        var strikeEntry=strikeSource with{Id=Guid.NewGuid(),SourceElementWaypointId=strikeSource.Id,Latitude=52,Longitude=-1};
        vm.ActiveRoute.Clear();vm.ActiveRoute.Add(strikeEntry);
        var check=typeof(MainViewModel).GetMethod("CheckEntryCallout",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        SimulatorTelemetry Near(bool ground=false,double offset=.15)=>new(DateTimeOffset.UtcNow,"Voice test",4000,350,180,52+offset,-1,ground,false,true,10000,0,4000);
        check.Invoke(vm,[Near(true)]);check.Invoke(vm,[Near() with{CapturedAt=DateTimeOffset.UtcNow.AddMinutes(-1)}]);check.Invoke(vm,[Near(offset:.2)]);
        Assert(vm.TrackingEvents.Count(e=>e.Contains("ENTRY ALERT"))==beforeAlerts,"no ground, stale or outside 10 NM entry alert");
        check.Invoke(vm,[Near()]);check.Invoke(vm,[Near()]);Assert(vm.TrackingEvents.Count(e=>e.Contains("ENTRY ALERT"))==beforeAlerts+1&&vm.WaypointCallout.Contains("strike entry"),"once-only visual entry inside 10 NM");
        var progressFile=Directory.GetFiles(vm.SettingsDirectory,"*-progress.json").Single();Assert(File.ReadAllText(progressFile).Contains(strikeEntry.Id.ToString()),"spoken entry checkpoint persisted");
        vm.ActiveRoute.Clear();foreach(var point in originalRoute)vm.ActiveRoute.Add(point);
        vm.ProcessSimulatorTelemetry(Sample(1,true,2));vm.ProcessSimulatorTelemetry(Sample(2,false,150,vs:700));vm.ProcessSimulatorTelemetry(Sample(6,false,300,vs:-250));
        handler.Offline=true;vm.RetryTrackUploadCommand.Execute(null);Assert(vm.Message.Contains("network"),"offline upload reports error without losing queue");handler.Offline=false;
        handler.FailUploadOnce=true;vm.RetryTrackUploadCommand.Execute(null);Assert(vm.Message.Contains("acknowledgement"),"lost upload acknowledgement retains retryable points");
        vm.RetryTrackUploadCommand.Execute(null);Assert(handler.Uploaded.Count==2,"five-second queue uploads after connection recovers");
        vm.SignOutCommand.Execute(null);vm.Identifier="69EAW-001";vm.SignInAsync("test-password").GetAwaiter().GetResult();
        Assert(vm.SimBriefPilotId=="404036","saved SimBrief ID restores on login");
        Assert(vm.BriefingSigned,"persisted briefing acknowledgement restores on login during flight recovery");
        Assert(vm.ActiveSortie?.Id==saved.Id&&vm.OffClock!="—"&&vm.LocalTrackPoints.Count==2,"same-pilot restart restores tracker checkpoint and track map");
        vm.ProcessSimulatorTelemetry(Sample(7,false,150,vs:-220));vm.ProcessSimulatorTelemetry(Sample(8,true,70));vm.ProcessSimulatorTelemetry(Sample(9,true,0,true));vm.ProcessSimulatorTelemetry(Sample(12,true,0,true));
        Assert(vm.SubmitDebriefCommand.CanExecute(null)&&vm.LandingRate.Contains("220"),"landing and parked confirmation enable completion");
        vm.SelectedTab=0;Render(window,"work/wpf-live-acars.png");handler.FailFinishOnce=true;vm.SubmitDebriefCommand.Execute(null);Assert(vm.ActiveSortie is not null&&vm.Message.Contains("acknowledgement"),"lost completion acknowledgement retains pending debrief");vm.SubmitDebriefCommand.Execute(null);
        Assert(vm.BriefingRoute=="—","briefing cleared with completed mission");
        Assert(vm.ActiveSortie is null&&handler.FinishCount==1&&handler.LastDebrief.HasValue&&vm.Message.Contains("completed"),"completion submits debrief and clears active mission");
        Assert(!File.Exists(Path.Combine(vm.RecoveryDirectory,handler.PilotId.ToString("N")+".json")),"successful completion clears local recovery");
        vm.SelectedSortie=handler.CurrentSortie;vm.ActivateMissionCommand.Execute(null);
        Assert(vm.ActiveSortie is not null&&vm.ActiveSortie.Id!=saved.Id&&handler.DuplicateCount==1,"fly again creates a new sortie instead of modifying completed history");
        vm.CancelActiveMissionCommand.Execute(null);Assert(vm.ActiveSortie is null,"cancel active mission releases tracking");
        handler.ClearSorties();handler.SetSortie(saved with{Id=Guid.NewGuid()});vm.SelectedSortie=handler.CurrentSortie;
        vm.EditDraftCommand.Execute(null);vm.DeleteDraftCommand.Execute(null);
        Assert(handler.DeleteCount==1&&vm.Sorties.Count==0&&vm.PlanTitle==""&&vm.SelectedTab==2,"deletion removes draft and resets its open editor");
        handler.SetSortie(saved with{Id=Guid.NewGuid(),Status="cancelled"});vm.RefreshCommand.Execute(null);vm.SelectedSortie=vm.Sorties.Single();
        Assert(vm.CanDeleteSelectedDraft&&vm.DeleteDraftCommand.CanExecute(null),"cancelled unflown mission enables deletion");vm.DeleteDraftCommand.Execute(null);Assert(handler.DeleteCount==2&&vm.Sorties.Count==0,"cancelled mission deletion removes row");
        vm.SelectedSortie=saved with{Status="cancelled",StartedAt=DateTimeOffset.UtcNow};Assert(!vm.CanDeleteSelectedDraft,"cancelled tracking history remains protected");
        vm.SelectedSortie=saved with{Status="completed"};Assert(!vm.DeleteDraftCommand.CanExecute(null)&&!vm.CanDeleteSelectedDraft,"completed history cannot be deleted");vm.SelectedSortie=null;
        Console.WriteLine("PASS: saved activation/SimBrief, coordinate/profile encoding, tracking cadence, offline retention, same-pilot recovery, completion/debrief and fly-again lifecycle.");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);yield return child;foreach(var nested in Descendants(child))yield return nested;}
    }
    private static void CheckTracking()
    {
        var now=DateTimeOffset.UtcNow;
        SimulatorTelemetry Sample(int second,bool ground,double speed,bool brake=false,double vs=0,double fuel=1000) =>
            new(now.AddSeconds(second),"Test Atlas",1000,speed,0,52,-1,ground,brake,true,fuel,vs,100);
        var airports=new[]{new Airfield{Icao="EGXC",Name="RAF Coningsby",Active=true},new Airfield{Icao="EGQS",Name="RAF Lossiemouth",Active=true},new Airfield{Icao="EGVN",Name="RAF Brize Norton",Active=false}};
        Assert(AirportSearch.Find(airports,"conin").Single().Icao=="EGXC"&&AirportSearch.Find(airports,"egq").Single().Icao=="EGQS"&&AirportSearch.Find(airports,"brize").Count==0,"airport partial name/ICAO search excludes inactive fields");
        Assert(new Sortie{Status="planned"}.DisplayStatus=="Not yet flown"&&new Sortie{Status="airborne"}.DisplayStatus=="ACTIVE"&&new Sortie{Status="completed"}.DisplayStatus=="Previously flown","saved mission ACTIVE/Archived presentation");
        Assert(EntryCallout.Kind("S1ENTRY","VISTA-STRIKE-1")=="strike"&&EntryCallout.Kind("DALTON-IN","VISTA-DROP-DALTON")=="airdrop"&&EntryCallout.Kind("DALTON-DROP","VISTA-DROP-DALTON") is null&&EntryCallout.Kind("WASH-NORTH","VISTA-BVR-WASH") is null,"entry-only strike/airdrop classification");
        Assert(EntryCallout.Instruction(new(){AltitudeFt=4000,SpeedKts=350},"strike").Contains("four thousand feet")&&EntryCallout.Instruction(new(){AltitudeFt=4000,SpeedKts=350},"strike").Contains("three hundred and fifty knots"),"British number phrasing");
        Assert(EntryCallout.Instruction(new(),"airdrop").Contains("pilot discretion"),"unspecified airdrop restrictions remain discretionary");
        var origin=MapProjection.Project(0,0);var north=MapProjection.Project(53,-1);
        Assert(Math.Abs(origin.X-.5)<.000001&&Math.Abs(origin.Y-.5)<.000001&&north.Y<.5,"Web Mercator map coordinate alignment");
        var engineEvents=new List<string>();var monitor=new EngineEventMonitor();monitor.Changed+=engineEvents.Add;
        monitor.Accept(Sample(0,true,0) with{EngineRunning=false});monitor.Accept(Sample(1,true,0));monitor.Accept(Sample(2,true,0));
        monitor.Accept(Sample(3,true,0) with{EngineRunning=false});
        Assert(engineEvents.Count==2&&engineEvents[0].StartsWith("ENGINE START")&&engineEvents[1].StartsWith("ENGINE SHUTDOWN"),"engine start/shutdown transitions without duplicates");
        monitor.Interrupted();monitor.Accept(Sample(4,true,0));Assert(engineEvents.Count==2,"reconnect does not invent engine start");
        var running=new EngineEventMonitor();running.Changed+=engineEvents.Add;running.Accept(Sample(0,true,0));
        Assert(engineEvents.Last().Contains("Already running"),"already-running first sample distinguished from start");
        var tracker=new FlightTracker();
        tracker.Accept(Sample(0,true,2));tracker.Accept(Sample(1,false,120,vs:800));
        tracker.Accept(Sample(2,false,120,vs:-300));tracker.Accept(Sample(3,true,90));
        Assert(tracker.Takeoffs==1 && tracker.Landings==1 && tracker.LandingRateFpm==-300,"BM takeoff/landing rules");
        tracker.Accept(Sample(4,true,0,true,fuel:0));tracker.Accept(Sample(5,true,0,true));
        tracker.Accept(Sample(6,true,0,true));Assert(tracker.InTime is null,"park confirmation not premature");
        tracker.Accept(Sample(7,true,0,true));Assert(tracker.InTime is not null && tracker.EndFuelLb==1000,"park confirmation and zero fuel rejection");
        tracker.Accept(Sample(8,false,100));Assert(tracker.Takeoffs==2 && tracker.InTime is null,"additional circuit takeoff");
        tracker.ConnectionInterrupted();tracker.Accept(Sample(9,true,10));
        Assert(tracker.Landings==1,"reconnect does not invent landing");
        var midair=new FlightTracker();midair.Accept(Sample(0,false,100));midair.Accept(Sample(1,true,20));
        Assert(midair.Takeoffs==0 && midair.Landings==0,"midair first sample does not invent takeoff");
    }
    private static void Assert(bool test,string name) { if(!test) throw new Exception("FAIL: "+name); }
    private static void CheckArrivalAssignment(MainViewModel vm,FakeApi handler,MainWindow window)
    {
        vm.NewPlanCommand.Execute(null);vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZM400");vm.DepartureIcao="EGVN";vm.ArrivalSelection="Assign from mission";
        Assert(vm.Arrival is null&&!vm.ChooseArrivalAirport&&vm.BuilderNotice.Contains("awaiting"),"mission arrival can remain unknown before assignment");
        vm.PlannerStage=0;vm.SelectedTab=1;Render(window,"work/wpf-arrival-assignment.png");
        vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="AMF-LOGISTICS");
        Assert(vm.Arrival is null,"choosing family does not assign destination");
        vm.GenerateMissionCommand.Execute(null);
        Assert(vm.BuilderTasks.Count==1&&vm.Arrival?.Id==vm.BuilderTasks[0].Option.DestinationBaseId&&vm.BuilderTasks[0].Payload is not null,"generation assigns destination and compatible load");
        var chosen=vm.Arrival!.Id;var option=vm.BuilderTasks[0].Option.Id;vm.PlannerStage=0;vm.PlannerStage=2;vm.RefreshCommand.Execute(null);
        Assert(vm.Arrival?.Id==chosen&&vm.BuilderTasks[0].Option.Id==option,"refresh and page changes preserve assignment");
        vm.GenerateMissionCommand.Execute(null);Assert(vm.BuilderTasks[0].Option.Id!=option,"explicit regeneration changes destination choice");
        vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="AMF-LFA7");vm.BuilderOption=vm.BuilderOptions.Single();vm.AddMissionTaskCommand.Execute(null);
        Assert(vm.BuilderTasks.Count==2&&vm.Arrival?.Id==vm.BuilderTasks[0].Option.DestinationBaseId&&!vm.BuilderNotice.Contains("support"),"training combines with destination without changing arrival");
        vm.FlightCallsign="TEST71";vm.SaveMissionPlanCommand.Execute(null);Assert(vm.Message.Contains("saved"),"generated combined plan saves resolved destination");
        Render(window,"work/wpf-generated-arrival.png");
        vm.NewPlanCommand.Execute(null);vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZM400");vm.DepartureIcao="EGVN";vm.ArrivalSelection="Return to departure";
        Assert(vm.ArrivalIcao=="EGVN","return mode resolves departure");
        vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="AMF-LFA7");vm.GenerateMissionCommand.Execute(null);Assert(vm.ArrivalIcao=="EGVN"&&vm.BuilderTasks.Count==1,"training generation returns to departure");
        vm.NewPlanCommand.Execute(null);vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZM400");vm.DepartureIcao="EGVN";vm.ArrivalSelection="Return to departure";vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="AMF-PERSONNEL");vm.GenerateMissionCommand.Execute(null);
        Assert(vm.BuilderTasks.Count==0&&vm.Message.Contains("Assign from mission"),"return mode rejects outbound destination assignment");
        vm.NewPlanCommand.Execute(null);Assert(vm.ArrivalSelection=="Choose airport","new plan resets arrival policy");
        Console.WriteLine("PASS: unknown arrival, generated destination/load, stable assignment, explicit regeneration, combined tasks, return mode and resolved save.");
    }
    private static void CheckPlannerEase(MainViewModel vm,FakeApi handler,MainWindow window)
    {
        vm.NewPlanCommand.Execute(null);Assert(!vm.PlannerDirty,"new planner clean baseline");vm.PlanTitle="Exercise patrol";Assert(vm.PlannerDirty&&vm.PlannerBriefingReminder.Contains("fresh"),"edit marks dirty and warns briefing refresh");
        Assert(vm.PlannerChecklist.Contains("callsign")&&vm.PlannerChecklist.Contains("aircraft"),"validation checklist exposes missing details");
        vm.SelectedAircraft=vm.AvailableAircraft.First(a=>a.Serial=="ZK345");vm.DepartureIcao="EGXC";vm.ArrivalIcao="EGQS";
        Assert(vm.PlannerDistance.Contains("NM"),"route distance uses airport endpoints");
        vm.BuilderFamily=vm.BuilderFamilies.First(f=>f.Code=="TY-BVR");Assert(!vm.TaskNeedsLoad,"fighter training hides loads");
        vm.BuilderOption=vm.BuilderOptions.First();vm.AddMissionTaskCommand.Execute(null);Assert(vm.CanReviewTasks&&vm.TasksHeading.Contains("1")&&vm.Message.Contains("added"),"task count review gating and specific feedback");
        vm.ReturnToBaseCommand.Execute(null);Assert(vm.ArrivalIcao=="EGXC","return to base sets final arrival");
        vm.FlightCallsign="TEST88";vm.SaveMissionPlanCommand.Execute(null);Assert(!vm.PlannerDirty,"saved plan clears dirty state");
        vm.PlanTitle="Changed exercise";vm.DiscardPlannerChanges();Assert(!vm.PlannerDirty&&vm.BuilderTasks.Count==0,"discard resets planner");
        using var otherHandler=new FakeApi();using var otherApi=new SupabaseClient(new(){SupabaseUrl="https://dlqfpbqenqumdlogfrxo.supabase.co",PublishableKey="sb_publishable_test"},otherHandler);
        var other=new MainViewModel(otherApi){SettingsDirectory="work/test-prompt-settings"};other.Identifier="69EAW-001";other.SignInAsync("test-password").GetAwaiter().GetResult();other.NewPlanCommand.Execute(null);other.PlanTitle="Unsaved test";
        var prompted=false;other.PlannerLeaveRequested+=_=>prompted=true;other.SelectedTab=0;Assert(prompted&&other.SelectedTab==1,"navigation waits for explicit unsaved decision");
        Assert(!other.SaveBeforeLeavingAsync().GetAwaiter().GetResult()&&other.PlannerDirty,"invalid save keeps unsaved edits");
        other.DiscardPlannerChanges();other.NavigateAfterPlannerPrompt(0);Assert(other.SelectedTab==0&&!other.PlannerDirty,"discard allows navigation");
        Console.WriteLine("PASS: dirty baseline, navigation guard, invalid-save retention, checklist, distance, hidden loads, task feedback and return to base.");
    }
    private static void CheckSharingAndFleet(MainViewModel vm,FakeApi handler,MainWindow window)
    {
        vm.SelectedMissionCard=vm.MissionCards.First();vm.PublishMissionCommand.Execute(null);vm.OpenSharedMissionsCommand.Execute(null);
        Assert(vm.SharedMissions.Count==1&&vm.SelectedSharedMission is not null,"publish and browse shared plans");
        var shareWindow=Application.Current.Windows.OfType<SharedMissionsWindow>().First();Render(shareWindow,"work/wpf-shared-missions.png");
        vm.ImportSharedMissionCommand.Execute(null);Assert(vm.Message.Contains("added to your library"),"shared import refreshes pilot library");
        vm.WithdrawSharedMissionCommand.Execute(null);Assert(vm.SharedMissions.Single().Active==false,"owner withdrawal updates browser");shareWindow.Close();
        var plane=vm.Fleet.First();handler.Reservations=new[]{new FleetReservation(plane.Id,"Mark - 69EAW-002","TEST22","Training","Preparing",null)};
        vm.RefreshFleetAvailabilityCommand.Execute(null);
        Assert(vm.Fleet.First(f=>f.Id==plane.Id).AssignedPilot.Contains("Mark")&&!vm.AvailableAircraft.Any(f=>f.Id==plane.Id),"other pilot reservation removes aircraft from planner");
        vm.FleetSelection=vm.Fleet.First(f=>f.Id==plane.Id);vm.SelectedTab=3;Render(window,"work/wpf-fleet-reservations.png");
        handler.Reservations=Array.Empty<FleetReservation>();vm.RefreshFleetAvailabilityCommand.Execute(null);Assert(vm.AvailableAircraft.Any(f=>f.Id==plane.Id),"reservation release restores availability");
        Console.WriteLine("PASS: shared publication/browser/import/withdrawal and cross-pilot fleet reservation display/filtering.");
    }
    private static void CheckLibrary(MainViewModel vm,FakeApi handler,MainWindow window)
    {
        var plane=vm.Fleet.First();var dep=vm.Bases.First(b=>b.Icao=="EGXC");var arr=vm.Bases.First(b=>b.Icao=="EGQS");
        var root=Guid.NewGuid();var current=new Sortie{Id=root,AircraftId=plane.Id,PilotId=handler.PilotId,DepartureBaseId=dep.Id,ArrivalBaseId=arr.Id,Title="Northern patrol",Callsign="RIGID301",Status="completed",PlanSnapshot=JsonSerializer.SerializeToElement(new{}),EndedAt=DateTimeOffset.UtcNow};
        handler.SetLibrary(current,current with{Id=Guid.NewGuid(),MissionPlanId=root,Status="planned"},current with{Id=Guid.NewGuid(),Title="Strike practice",Status="planned"});
        vm.RefreshCommand.Execute(null);
        Assert(vm.MissionCards.Count==2&&vm.MissionCards.Single(c=>c.PlanId==root).TimesFlown==1,"replay attempts grouped into one previously flown card");
        vm.FlownFolderCommand.Execute(null);Assert(vm.LibraryView.Cast<MissionCard>().Count()==1,"flown folder filter");
        vm.SelectedMissionCard=vm.MissionCards.Single(c=>c.PlanId==root);vm.ArchiveMissionCommand.Execute(null);
        Assert(vm.LibraryFolder=="Archived"&&vm.OperationPlans.Count==1&&!vm.CanFlyLibraryMission,"archive removes plan from flight selector");
        Render(window,"work/wpf-library-archived.png");
        vm.RestoreMissionCommand.Execute(null);Assert(vm.LibraryFolder=="Previously flown"&&vm.OperationPlans.Count==2,"restore returns previously flown mission");
        vm.LibrarySearch="unmatched";Assert(vm.LibraryView.IsEmpty,"library search filters cards");vm.LibrarySearch="";
        vm.SelectedMissionCard=vm.MissionCards.Single(c=>c.Title=="Strike practice");vm.ToggleFavouriteCommand.Execute(null);
        Assert(vm.MissionCards.First().Title=="Strike practice"&&vm.OperationPlans.First().Title=="Strike practice"&&vm.MissionCards.First().Favourite,"favourite pins both selectors");
        vm.RefreshCommand.Execute(null);Assert(vm.MissionCards.First().Favourite,"favourite persists refresh");
        vm.LibraryFolder="Not yet flown";vm.SelectedMissionCard=vm.MissionCards.First();vm.ToggleFavouriteCommand.Execute(null);
        Assert(!vm.MissionCards.Any(c=>c.Favourite),"unfavourite removes pin");
        vm.LibraryFolder="Previously flown";vm.SelectedTab=2;Render(window,"work/wpf-library-cards.png");Render(window,"work/wpf-library-compact.png",1200,700);
        vm.SelectedTab=0;Assert(vm.PreparationSummary=="Choose a mission to begin","quiet preparation guidance");
        Console.WriteLine("PASS: grouped cards, folder filters/counts, archive/restore, selector exclusion and mission search.");
    }
    private static void Render(System.Windows.Window window,string path,int width=1440,int height=920)
    {
        window.Width=width;window.Height=height;
        var content=(FrameworkElement)window.Content;
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { },System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        content.Measure(new Size(width,height)); content.Arrange(new Rect(0,0,width,height)); content.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(path); encoder.Save(stream);
    }
}
internal sealed class FakeApi : HttpMessageHandler
{
    public string PlanKey {get;set;}="0123456789abcdef0123456789abcdef";
    private readonly Dictionary<Guid,SortieBriefingRecord> briefings=new();
    public Guid PilotId => ((Pilot[])tables["pilots"])[0].Id;
    public Sortie? CurrentSortie => ((Sortie[])tables["sorties"]).LastOrDefault();
    public FleetReservation[] Reservations {get;set;}=Array.Empty<FleetReservation>();
    private readonly Dictionary<Guid,Sortie> sharedSources=new();
    public void SetLibrary(params Sortie[] values)=>tables["sorties"]=values;
    public void SetSortie(Sortie value)=>tables["sorties"]=new[]{value};
    public void ClearSorties()=>tables["sorties"]=Array.Empty<Sortie>();
    public Dictionary<long,JsonElement> Uploaded {get;}=new();
    public JsonElement? LastDebrief {get;private set;}
    public int FinishCount {get;private set;}
    public bool FailUploadOnce {get;set;}
    public bool FailFinishOnce {get;set;}
    public int DeleteCount {get;private set;}
    public int DuplicateCount {get;private set;}
    public int RefreshCount {get;private set;}
    public string? LoginEmail {get;private set;}
    public string? ReceivedRefresh {get;private set;}
    public bool RejectRefresh {get;set;}
    public bool Offline {get;set;}
    public JsonElement? LastCataloguePlan {get;private set;}
    public JsonElement? LastPlan {get;private set;}
    public int SaveCount {get;private set;}
    public Guid AtlasType {get;private set;}
    public Guid? DelayedMission {get;set;}
    private TaskCompletionSource<HttpResponseMessage>? delayed;
    private readonly Dictionary<Guid,StoredWaypoint[]> missionRoutes=new();
    public void CompleteDelayed() => delayed!.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(missionRoutes[DelayedMission!.Value],SupabaseClient.JsonOptions),Encoding.UTF8,"application/json") });
    private readonly Dictionary<string,object> tables=new();
    private readonly Guid userId=Guid.Parse("11111111-1111-4111-8111-111111111111");
    public FakeApi()
    {
        using var manifest=JsonDocument.Parse(File.ReadAllText("docs/fleet-manifest.json"));
        var coordinateData=JsonDocument.Parse(File.ReadAllText("docs/airfield-coordinates.json")).RootElement.GetProperty("airfields").EnumerateArray().ToDictionary(c=>c.GetProperty("icao").GetString()!,c=>c.Clone());
        var bases=new[]{new Airfield{Id=Guid.NewGuid(),Icao="EGVN",Name="RAF Brize Norton",Active=true},new Airfield{Id=Guid.NewGuid(),Icao="EGXC",Name="RAF Coningsby",Active=true},new Airfield{Id=Guid.NewGuid(),Icao="EGQS",Name="RAF Lossiemouth",Active=true}};
        using var catalogue=JsonDocument.Parse(File.ReadAllText("docs/mission-catalogue.json"));
        bases=bases.Concat(catalogue.RootElement.GetProperty("bases").EnumerateArray().Where(b=>!bases.Any(v=>v.Icao==b.GetProperty("icao").GetString())).Select(b=>new Airfield{Id=Guid.NewGuid(),Icao=b.GetProperty("icao").GetString()!,Name=b.GetProperty("name").GetString()!,Latitude=coordinateData[b.GetProperty("icao").GetString()!].GetProperty("latitude").GetDecimal(),Longitude=coordinateData[b.GetProperty("icao").GetString()!].GetProperty("longitude").GetDecimal(),Active=true})).ToArray();
        bases=bases.Select(b=>b with{Latitude=coordinateData[b.Icao].GetProperty("latitude").GetDecimal(),Longitude=coordinateData[b.Icao].GetProperty("longitude").GetDecimal()}).ToArray();
        var types=manifest.RootElement.GetProperty("aircraft_types").EnumerateArray().Select(t=>new AircraftType{Id=Guid.NewGuid(),Code=t.GetProperty("code").GetString()!,Name=t.GetProperty("name").GetString()!,Active=true}).ToList();
        var units=manifest.RootElement.GetProperty("units").EnumerateArray().Select(t=>new Squadron{Id=Guid.NewGuid(),Code=t.GetProperty("code").GetString()!,Name=t.GetProperty("name").GetString()!,BaseId=bases.First(b=>b.Icao==t.GetProperty("base_icao").GetString()).Id,Active=true}).ToList();
        var aircraft=manifest.RootElement.GetProperty("aircraft").EnumerateArray().Select(t=>new Aircraft{Id=Guid.NewGuid(),Serial=t.GetProperty("serial").GetString()!,AircraftTypeId=types.First(b=>b.Code==t.GetProperty("aircraft_type_code").GetString()).Id,SquadronId=units.First(b=>b.Code==t.GetProperty("unit_code").GetString()).Id,HomeBaseId=bases.First(b=>b.Icao==t.GetProperty("base_icao").GetString()).Id,Status="available"}).ToList();
        tables["bases"]=bases;tables["aircraft_types"]=types;tables["squadrons"]=units;tables["aircraft"]=aircraft;
        tables["pilots"]=new[]{new Pilot{Id=Guid.NewGuid(),AuthUserId=userId,PilotNumber="69EAW-001",DisplayName="Josh",DisplayLabel="Josh - 69EAW-001 - Callsign pending",Role="admin",Status="active"}};
        AtlasType=types.First(t=>t.Code=="ATLAS").Id;
        var missions=new[]{
            new Mission{Id=Guid.NewGuid(),Title="A Atlas transfer",DepartureBaseId=bases[0].Id,ArrivalBaseId=bases[1].Id,AircraftTypeId=AtlasType,Active=true,Revision=1},
            new Mission{Id=Guid.NewGuid(),Title="B General training",DepartureBaseId=bases[0].Id,ArrivalBaseId=bases[1].Id,Active=true,Revision=2},
            new Mission{Id=Guid.NewGuid(),Title="C Typhoon mission",DepartureBaseId=bases[0].Id,ArrivalBaseId=bases[1].Id,AircraftTypeId=types.First(t=>t.Code=="TYPHOON-FGR4").Id,Active=true},
            new Mission{Id=Guid.NewGuid(),Title="D Wrong squadron",DepartureBaseId=bases[0].Id,ArrivalBaseId=bases[1].Id,SquadronId=Guid.NewGuid(),Active=true},
            new Mission{Id=Guid.NewGuid(),Title="E Reverse airports",DepartureBaseId=bases[1].Id,ArrivalBaseId=bases[0].Id,Active=true}};
        foreach(var mission in missions) missionRoutes[mission.Id]=[new(){Id=Guid.NewGuid(),Position=1,Identifier=mission.Title[..1]+"1",Latitude=52,Longitude=-1},new(){Id=Guid.NewGuid(),Position=2,Identifier=mission.Title[..1]+"2",Latitude=53,Longitude=-1}];
        BuildMissionTables(catalogue.RootElement,bases,types);
        tables["shared_missions"]=Array.Empty<SharedMission>();tables["mission_library_archives"]=Array.Empty<MissionArchive>();tables["missions"]=missions;tables["sorties"]=Array.Empty<Sortie>();tables["pilot_statistics"]=new[]{new PilotStatistics()};
    }
    private void BuildMissionTables(JsonElement root,Airfield[] bases,List<AircraftType> types)
    {
        string Text(JsonElement e,string key)=>e.GetProperty(key).GetString()!;
        var families=root.GetProperty("families").EnumerateArray().Select(f=>new MissionFamily{Code=Text(f,"code"),Title=Text(f,"title"),Enabled=true,DepartureIcaos=f.GetProperty("departure").EnumerateArray().Select(v=>v.GetString()!).ToArray(),ReturnIcaos=f.GetProperty("arrival").EnumerateArray().Select(v=>v.GetString()!).ToArray()}).ToArray();
        tables["mission_families"]=families;
        tables["mission_family_aircraft_types"]=root.GetProperty("families").EnumerateArray().SelectMany(f=>f.GetProperty("types").EnumerateArray().Select(t=>new FamilyAircraftType(Text(f,"code"),types.First(v=>v.Code==t.GetString()).Id))).ToArray();
        var elements=root.GetProperty("elements").EnumerateArray().Select(e=>new MissionElement{Id=Guid.NewGuid(),Code="VISTA-"+Text(e,"code"),Title=Text(e,"title"),Active=true}).ToArray();
        tables["mission_elements"]=elements;
        tables["mission_element_waypoints"]=root.GetProperty("elements").EnumerateArray().SelectMany(e=>e.GetProperty("points").EnumerateArray().Select((v,i)=>JsonSerializer.Deserialize<StoredWaypoint>(v.GetRawText(),SupabaseClient.JsonOptions)! with {Id=Guid.NewGuid(),Position=i+1,MissionElementId=elements.First(el=>el.Code=="VISTA-"+Text(e,"code")).Id})).ToArray();
        tables["mission_catalogue_options"]=root.GetProperty("options").EnumerateArray().Select(o=>new CatalogueOption{Id=Guid.NewGuid(),Code=Text(o,"code"),FamilyCode=Text(o,"family"),Title=Text(o,"title"),Enabled=true,ElementId=o.GetProperty("element").ValueKind==JsonValueKind.String?elements.First(e=>e.Code=="VISTA-"+Text(o,"element")).Id:null,DestinationBaseId=o.GetProperty("destination").ValueKind==JsonValueKind.String?bases.First(b=>b.Icao==Text(o,"destination")).Id:null}).ToArray();
        var payloads=root.GetProperty("payloads").EnumerateArray().Select(p=>new PayloadPreset{Id=Guid.NewGuid(),Code=Text(p,"code"),Title=Text(p,"title"),Description=Text(p,"description"),Kind=Text(p,"kind"),PayloadKg=p.GetProperty("kg").GetInt32(),PersonnelCount=p.GetProperty("people").GetInt32(),Enabled=true}).ToArray();
        tables["mission_payload_presets"]=payloads;
        tables["mission_payload_destinations"]=root.GetProperty("payloads").EnumerateArray().SelectMany(p=>p.GetProperty("destinations").EnumerateArray().Select(d=>new PayloadDestination(payloads.First(v=>v.Code==Text(p,"code")).Id,bases.First(b=>b.Icao==d.GetString()).Id))).ToArray();
        tables["mission_family_payloads"]=root.GetProperty("payloads").EnumerateArray().SelectMany(p=>p.GetProperty("families").EnumerateArray().Select(f=>new FamilyPayload(f.GetString()!,payloads.First(v=>v.Code==Text(p,"code")).Id))).ToArray();
    }
    private void StoreSaved(JsonElement request)
    {
        var plan=request.GetProperty("p_plan");var id=request.GetProperty("p_sortie_id").GetGuid();
        SetSortie(new Sortie{Id=id,PilotId=PilotId,AircraftId=plan.GetProperty("aircraft_id").GetGuid(),DepartureBaseId=plan.GetProperty("departure_base_id").GetGuid(),ArrivalBaseId=plan.GetProperty("arrival_base_id").GetGuid(),Title=plan.GetProperty("title").GetString()!,Callsign=plan.GetProperty("callsign").GetString()!,Source="custom",Status="planned",PlanSnapshot=JsonSerializer.SerializeToElement(new{}),CreatedAt=DateTimeOffset.UtcNow});
    }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        if(Offline) throw new HttpRequestException("Test network unavailable");
        var path=request.RequestUri!.AbsolutePath;
        if(path=="/auth/v1/token")
        {
            var refresh=request.RequestUri.Query.Contains("refresh_token");
            if(refresh)
            {
                using var body=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());
                ReceivedRefresh=body.RootElement.GetProperty("refresh_token").GetString();
                if(RejectRefresh) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content=new StringContent("{\"message\":\"Invalid refresh token\"}") });
                RefreshCount++;
            }
            else {using var body=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult()); LoginEmail=body.RootElement.GetProperty("email").GetString();}
            return Response(new {access_token="test-access",refresh_token=$"test-refresh-{RefreshCount}",expires_in=refresh?3600:1,user=new{id=userId}});
        }
        if(request.Headers.Authorization?.Parameter!="test-access") throw new Exception("Authenticated request missing JWT");
        if(path.Contains("logout"))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        if(path.EndsWith("publish_mission"))
        {
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());var source=((Sortie[])tables["sorties"]).First(s=>s.Id==json.RootElement.GetProperty("p_sortie_id").GetGuid());var id=Guid.NewGuid();sharedSources[id]=source;
            tables["shared_missions"]=new[]{new SharedMission{Id=id,OwnerId=PilotId,Title=source.Title,Squadron="Test squadron",Author="Josh - 69EAW-001",DepartureIcao="EGXC",ArrivalIcao="EGQS",Revision=1,Active=true,UpdatedAt=DateTimeOffset.UtcNow}};return Response(id);
        }
        if(path.EndsWith("import_shared_mission"))
        {
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());var b=json.RootElement;var id=b.GetProperty("p_request_id").GetGuid();SetSortie(sharedSources[b.GetProperty("p_shared_id").GetGuid()] with{Id=id,Status="planned",MissionPlanId=null});return Response(id);
        }
        if(path.EndsWith("withdraw_shared_mission"))
        {
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());var id=json.RootElement.GetProperty("p_shared_id").GetGuid();tables["shared_missions"]=((SharedMission[])tables["shared_missions"]).Select(s=>s.Id==id?s with{Active=false}:s).ToArray();return Response(id);
        }
        if(path.EndsWith("fleet_reservations"))return Response(Reservations);
        if(path.EndsWith("set_mission_archived"))
        {
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());var b=json.RootElement;var mission=((Sortie[])tables["sorties"]).First(s=>s.Id==b.GetProperty("p_sortie_id").GetGuid());var plan=mission.MissionPlanId??mission.Id;
            tables["mission_library_archives"]=b.GetProperty("p_archived").GetBoolean()?new[]{new MissionArchive(PilotId,plan)}:Array.Empty<MissionArchive>();return Response(plan);
        }
        if(path.Contains("briefing"))
        {
            using var body=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());var b=body.RootElement;var id=b.GetProperty("p_sortie_id").GetGuid();
            var savedBrief=briefings.GetValueOrDefault(id)??new SortieBriefingRecord{SortieId=id,CurrentPlanKey=PlanKey};savedBrief=savedBrief with{CurrentPlanKey=PlanKey};
            if(path.EndsWith("save_sortie_briefing"))savedBrief=savedBrief with{Document=b.GetProperty("p_document").Deserialize<SimBriefBriefing>(SupabaseClient.JsonOptions),PlanKey=PlanKey,Revision=Guid.NewGuid(),ImportedAt=DateTimeOffset.UtcNow,SignedAt=null};
            if(path.EndsWith("save_briefing_notes"))savedBrief=savedBrief with{MissionNotes=b.GetProperty("p_notes").GetString()!,Revision=Guid.NewGuid(),SignedAt=null};
            if(path.EndsWith("sign_sortie_briefing"))savedBrief=savedBrief with{SignedAt=DateTimeOffset.UtcNow,SignedBy=PilotId,SignedLabel="Josh · 69EAW-001"};
            briefings[id]=savedBrief;return Response(savedBrief);
        }
        if(path.EndsWith("rpc/save_sortie_plan"))
        {
            SaveCount++;
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());
            LastPlan=json.RootElement.Clone();StoreSaved(json.RootElement);return Response(json.RootElement.GetProperty("p_sortie_id").GetGuid());
        }
        if(path.EndsWith("rpc/save_catalogue_plan"))
        {
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());
            LastCataloguePlan=json.RootElement.Clone();StoreSaved(json.RootElement);return Response(json.RootElement.GetProperty("p_sortie_id").GetGuid());
        }
        if(path.EndsWith("rpc/set_mission_catalogue_enabled"))
        {
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());
            var code=json.RootElement.GetProperty("p_code").GetString();var enabled=json.RootElement.GetProperty("p_enabled").GetBoolean();
            tables["mission_catalogue_options"]=((CatalogueOption[])tables["mission_catalogue_options"]).Select(o=>o.Code==code?o with{Enabled=enabled}:o).ToArray();
            return Response((object?)null!);
        }
        if(path.EndsWith("rpc/delete_planned_sortie"))
        {
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());DeleteCount++;ClearSorties();return Response(json.RootElement.GetProperty("p_sortie_id").GetGuid());
        }
        if(path.Contains("rpc/activate_saved_sortie")||path.Contains("rpc/start_sortie_tracking")||path.Contains("rpc/cancel_active_sortie")||path.Contains("rpc/duplicate_saved_sortie")||path.Contains("rpc/finish_tracked_sortie")||path.Contains("rpc/upload_sortie_track"))
        {
            using var json=JsonDocument.Parse(request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult());var id=json.RootElement.GetProperty("p_sortie_id").GetGuid();
            var current=CurrentSortie!;
            if(path.Contains("upload_sortie_track")){var pts=json.RootElement.GetProperty("p_points");foreach(var point in pts.EnumerateArray())Uploaded[point.GetProperty("sequence").GetInt64()]=point.Clone();if(FailUploadOnce){FailUploadOnce=false;return Failure();}return Response(pts.GetArrayLength());}
            if(path.Contains("finish_tracked_sortie")){if(current.Status!="completed")FinishCount++;LastDebrief=json.RootElement.GetProperty("p_debrief").Clone();SetSortie(current with{Status="completed"});if(FailFinishOnce){FailFinishOnce=false;return Failure();}}
            else if(path.Contains("duplicate_saved_sortie")){DuplicateCount++;id=Guid.NewGuid();SetSortie(current with{Id=id,Status="planned"});}
            else SetSortie(current with{Status=path.Contains("start_sortie_tracking")?"airborne":path.Contains("cancel_active_sortie")?"cancelled":"briefed"});
            return Response(id);
        }
        if(path.EndsWith("mission_waypoints"))
        {
            var id=Guid.Parse(request.RequestUri.Query.Split("eq.")[1].Split('&')[0]);
            if(id==DelayedMission) { delayed=new(); return delayed.Task; }
            return Response(missionRoutes[id]);
        }
        if(path.EndsWith("sortie_waypoints")) return Response(missionRoutes.Values.First());
        return Response(tables[path.Split('/').Last()]);
    }
    private static Task<HttpResponseMessage> Failure()=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){Content=new StringContent("{\"message\":\"Test acknowledgement lost\"}")});
    private static Task<HttpResponseMessage> Response(object value)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(value,SupabaseClient.JsonOptions),Encoding.UTF8,"application/json")});
}






