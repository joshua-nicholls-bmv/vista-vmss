using System.Globalization;
using System.Text.Json;
namespace Vista.Core;
public sealed record SimBriefBriefing(string Departure,string Arrival,string Registration,string Aircraft,string FlightNumber,string Route,decimal? RampFuelKg,decimal? TripFuelKg,decimal? CruiseAltitudeFt,long? FlightSeconds,DateTimeOffset ImportedAt)
{
    public string StaticId {get;init;}="";
    public string PlanKey {get;init;}="";
    public string Alternate {get;init;}="";
    public string DepartureRunway {get;init;}="";
    public string ArrivalRunway {get;init;}="";
    public decimal? TaxiFuelKg {get;init;}
    public decimal? ContingencyFuelKg {get;init;}
    public decimal? AlternateFuelKg {get;init;}
    public decimal? ReserveFuelKg {get;init;}
    public decimal? ExtraFuelKg {get;init;}
    public decimal? ZeroFuelWeightKg {get;init;}
    public decimal? TakeoffWeightKg {get;init;}
    public decimal? LandingWeightKg {get;init;}
    public decimal? PayloadKg {get;init;}
    public long? BlockSeconds {get;init;}
    public string DepartureWeather {get;init;}="";
    public string ArrivalWeather {get;init;}="";
    public string AlternateWeather {get;init;}="";
    public string OperationalInformation {get;init;}="";
    public string OfpUrl {get;init;}="";
}
public static class SimBriefImport
{
    public static string StaticId(Guid sortieId,string planKey="")=>"VISTA_"+sortieId.ToString("N")+(planKey.Length>=12?"_"+planKey[..12]:"");
    public static string FetchUrl(string userId,Guid sortieId,string planKey="")
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(userId.Trim(),@"^\d+$"))throw new InvalidOperationException("Enter your numeric SimBrief Pilot ID from SimBrief Account Settings.");
        return "https://www.simbrief.com/api/xml.fetcher.php?userid="+Uri.EscapeDataString(userId.Trim())+"&static_id="+StaticId(sortieId,planKey)+"&json=1";
    }
    public static SimBriefBriefing Parse(string json,Sortie sortie,string departure,string arrival,string registration,string planKey="")
    {
        using var doc=JsonDocument.Parse(json);var root=doc.RootElement;
        string Value(string section,string key)=>root.TryGetProperty(section,out var s)&&s.ValueKind==JsonValueKind.Object&&s.TryGetProperty(key,out var v)&&v.ValueKind is JsonValueKind.String or JsonValueKind.Number?v.ToString().Trim():"";
        if(Value("fetch","status").Equals("Error",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("SimBrief has no briefing for this mission. Generate it in SimBrief, then import again.");
        var dep=Value("origin","icao_code").ToUpperInvariant();var arr=Value("destination","icao_code").ToUpperInvariant();var reg=Value("aircraft","reg");if(reg.Length==0)reg=Value("aircraft","registration");
        var number=Value("general","flight_number");var airline=Value("general","icao_airline");
        if(!dep.Equals(departure,StringComparison.OrdinalIgnoreCase)||!arr.Equals(arrival,StringComparison.OrdinalIgnoreCase)||!reg.Equals(registration,StringComparison.OrdinalIgnoreCase)||number!=SimBriefDispatch.FlightNumber(sortie.Callsign)||!airline.Equals("RRR",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This SimBrief briefing does not match the active mission's airports, RRR flight number and aircraft registration. Open SimBrief from VISTA and generate that mission first.");
        var staticId=Value("params","static_id");if((staticId.Length>0||planKey.Length>0)&&staticId!=StaticId(sortie.Id,planKey))throw new InvalidOperationException("The SimBrief briefing belongs to another mission revision. Open SimBrief from VISTA and regenerate it.");
        var units=Value("params","units").ToLowerInvariant();
        decimal? Number(string section,string key){var text=Value(section,key);if(text.Length==0)return null;if(!decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var n)||n<0)throw new InvalidOperationException("SimBrief returned an invalid "+key+" value.");return n;}
        decimal? Weight(string section,string key){var n=Number(section,key);if(n is null)return null;return units switch{"kgs" or "kg"=>n,"lbs" or "lb"=>decimal.Round(n.Value*0.45359237m,1),_=>throw new InvalidOperationException("SimBrief returned fuel without recognised weight units.")};}
        decimal? Fuel(string key)=>Weight("fuel",key);
        var route=Value("general","route");if(route.Length==0)throw new InvalidOperationException("The SimBrief briefing has no generated route.");
        var seconds=Number("times","est_time_enroute");
        var block=Number("times","est_block");
        var directory=Value("files","directory");string pdf="";
        if(root.TryGetProperty("files",out var files)&&files.TryGetProperty("pdf",out var pdfNode)&&pdfNode.ValueKind==JsonValueKind.Object&&pdfNode.TryGetProperty("link",out var link))pdf=link.GetString()??"";
        var ofp=Uri.TryCreate(directory,UriKind.Absolute,out var baseUri)&&Uri.TryCreate(baseUri,pdf,out var uri)&&pdf.Length>0?uri.ToString():"";
        if(!IsSafeOfpUrl(ofp))ofp="";
        return new(dep,arr,reg.ToUpperInvariant(),Value("aircraft","name"),number,route,Fuel("plan_ramp"),Fuel("enroute_burn"),Number("general","initial_altitude"),seconds is null?null:checked((long)seconds.Value),DateTimeOffset.UtcNow)
        {
            StaticId=staticId,PlanKey=planKey,Alternate=Value("alternate","icao_code"),DepartureRunway=Value("origin","plan_rwy"),ArrivalRunway=Value("destination","plan_rwy"),
            TaxiFuelKg=Fuel("taxi"),ContingencyFuelKg=Fuel("contingency"),AlternateFuelKg=Fuel("alternate_burn"),ReserveFuelKg=Fuel("reserve"),ExtraFuelKg=Fuel("extra"),
            ZeroFuelWeightKg=Weight("weights","est_zfw"),TakeoffWeightKg=Weight("weights","est_tow"),LandingWeightKg=Weight("weights","est_ldw"),PayloadKg=Weight("weights","payload"),BlockSeconds=block is null?null:checked((long)block.Value),
            DepartureWeather=Value("weather","orig_metar"),ArrivalWeather=Value("weather","dest_metar"),AlternateWeather=Value("weather","altn_metar"),
            OperationalInformation=Value("general","sys_rmk"),OfpUrl=ofp
        };
    }
    public static bool IsSafeOfpUrl(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var u)&&u.Scheme=="https"&&u.UserInfo.Length==0&&(u.Host=="simbrief.com"||u.Host.EndsWith(".simbrief.com",StringComparison.OrdinalIgnoreCase));
}
