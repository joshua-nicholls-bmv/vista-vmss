using System.Globalization;
namespace Vista.Core;
public sealed record TrackUpload(long Sequence, DateTimeOffset MeasuredAt, decimal Latitude, decimal Longitude,
    decimal AltitudeFt, decimal GroundspeedKts, decimal HeadingDeg, bool OnGround, decimal? FuelKg);
public sealed record TrackedDebrief(DateTimeOffset EndedAt, long FlightSeconds, decimal DistanceNm,
    decimal? LandingRateFpm, decimal? FuelUsedKg, string PilotNotes, IReadOnlyList<string> Events);
public static class SimBriefDispatch
{
    public static string AircraftCode(string fleetCode,string? configured=null) => fleetCode switch
    {
        "ATLAS" => "A400",
        "TYPHOON-FGR4" or "TYPHOON-T3" => "EUFI",
        _ => configured ?? throw new InvalidOperationException("No SimBrief aircraft code is configured for this aircraft.")
    };
    public static string FlightNumber(string callsign)
    {
        var match=System.Text.RegularExpressions.Regex.Match(callsign.Trim(),@"^(?:[A-Za-z][A-Za-z\s-]*)?([0-9]{1,4})$");
        if(!match.Success)throw new InvalidOperationException("Enter a flight callsign ending in its flight number, such as DREAD 408 or RIDID 321.");
        return match.Groups[1].Value;
    }
    public static string Coordinate(decimal latitude, decimal longitude)
    {
        string Part(decimal value,int digits,char positive,char negative)
        {
            var seconds=(int)Math.Round(Math.Abs(value)*3600,MidpointRounding.AwayFromZero);
            return (seconds/3600).ToString(new string('0',digits),CultureInfo.InvariantCulture)+(seconds%3600/60).ToString("00")+(seconds%60).ToString("00")+(value<0?negative:positive);
        }
        return Part(latitude,2,'N','S')+Part(longitude,3,'E','W');
    }
    public static string BuildUrl(Sortie sortie,IReadOnlyList<StoredWaypoint> points,string departure,string arrival,string registration,string type,string profile,string captain,string alternate="",string planKey="")
    {
        var values=new Dictionary<string,string>{{"airline","RRR"},{"fltnum",FlightNumber(sortie.Callsign)},{"orig",departure},{"dest",arrival},{"reg",registration},{"callsign",sortie.Callsign},{"route",points.Count>0?string.Join(" DCT ",points.OrderBy(p=>p.Position).Select(p=>Coordinate(p.Latitude,p.Longitude))):string.IsNullOrWhiteSpace(sortie.RouteText)?"DCT":sortie.RouteText},{"units","KGS"},{"pounds","0"},{"cpt",captain},{"static_id","VISTA_"+sortie.Id.ToString("N")}};
        values["static_id"]=SimBriefImport.StaticId(sortie.Id,planKey);
        if(!string.IsNullOrWhiteSpace(type))values["type"]=type.Trim();
        if(!string.IsNullOrWhiteSpace(profile))values["type"]=profile.Trim();
        if(sortie.PlanSnapshot.ValueKind==System.Text.Json.JsonValueKind.Object)
        {
            if(sortie.PlanSnapshot.TryGetProperty("payload_kg",out var weight))values["manualpayload"]=(weight.GetDecimal()/1000m).ToString("0.###",CultureInfo.InvariantCulture);
            if(sortie.PlanSnapshot.TryGetProperty("personnel_count",out var people))values["pax"]=people.GetInt32().ToString(CultureInfo.InvariantCulture);
        }
        if(!string.IsNullOrWhiteSpace(alternate))values["altn"]=alternate;
        values["manualrmk"]="VISTA military training route. Task altitude/speed instructions remain in VISTA; review the correct aircraft performance profile before generating.";
        var restrictions=string.Join("; ",points.Where(p=>p.AltitudeFt is not null||p.SpeedKts is not null).Select(p=>$"{p.Identifier}: {p.Instructions}"));
        values["manualrmk"]+=" "+restrictions[..Math.Min(4000,restrictions.Length)];
        return "https://dispatch.simbrief.com/options/custom?"+string.Join("&",values.Select(v=>v.Key+"="+Uri.EscapeDataString(v.Value)));
    }
}
