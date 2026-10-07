namespace Vista.Core;
public static class AirportSearch
{
    public static IReadOnlyList<Airfield> Find(IEnumerable<Airfield> airports,string? query)
    {
        var text=(query??"").Trim();if(text.Length<2)return [];
        return airports.Where(a=>a.Active&&(a.Icao.Contains(text,StringComparison.OrdinalIgnoreCase)||a.Name.Contains(text,StringComparison.OrdinalIgnoreCase))).OrderBy(a=>a.Icao.Equals(text,StringComparison.OrdinalIgnoreCase)?0:1).ThenBy(a=>a.Name).Take(8).ToList();
    }
}
