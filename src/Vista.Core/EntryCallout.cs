using System.Text.RegularExpressions;
namespace Vista.Core;
public static class EntryCallout
{
    public const double TriggerDistanceNm=10;
    public static string? Kind(string identifier,string elementCode)
    {
        if(Regex.IsMatch(identifier,@"^S[1-5]ENTRY$",RegexOptions.IgnoreCase)&&elementCode.Equals("VISTA-STRIKE-"+identifier[1],StringComparison.OrdinalIgnoreCase))return "strike";
        if(elementCode.Equals("VISTA-DROP-DALTON",StringComparison.OrdinalIgnoreCase)&&identifier.Equals("DALTON-IN",StringComparison.OrdinalIgnoreCase)
            ||elementCode.Equals("VISTA-DROP-NETHERAVON",StringComparison.OrdinalIgnoreCase)&&identifier.Equals("NETHERAVON-IN",StringComparison.OrdinalIgnoreCase)
            ||elementCode.Equals("VISTA-DROP-PEMBREY",StringComparison.OrdinalIgnoreCase)&&identifier.Equals("PEMBREY-IN",StringComparison.OrdinalIgnoreCase))return "airdrop";
        return null;
    }
    public static string Instruction(StoredWaypoint point,string kind)
    {
        var altitude=point.AltitudeFt is int feet?$"Entry altitude, {Words(feet)} feet.":"Entry altitude at pilot discretion.";
        var speed=point.SpeedKts is int knots?$"Entry speed, {Words(knots)} knots.":"Entry speed at pilot discretion.";
        var instructions=point.Instructions.Trim();
        instructions=Regex.Replace(instructions,@"\bNM\b","nautical miles",RegexOptions.IgnoreCase);
        instructions=Regex.Replace(instructions,@"\bft\b","feet",RegexOptions.IgnoreCase);
        instructions=Regex.Replace(instructions,@"\bkt\b","knots",RegexOptions.IgnoreCase);
        return $"Approaching {kind} entry. {altitude} {speed} {instructions}".Trim();
    }
    private static string Words(int value)
    {
        string[] units=["zero","one","two","three","four","five","six","seven","eight","nine","ten","eleven","twelve","thirteen","fourteen","fifteen","sixteen","seventeen","eighteen","nineteen"];
        string[] tens=["","","twenty","thirty","forty","fifty","sixty","seventy","eighty","ninety"];
        if(value<0)return "minus "+Words(-value);if(value<20)return units[value];if(value<100)return tens[value/10]+(value%10==0?"":" "+units[value%10]);if(value<1000)return units[value/100]+" hundred"+(value%100==0?"":" and "+Words(value%100));return Words(value/1000)+" thousand"+(value%1000==0?"":value%1000<100?" and "+Words(value%1000):" "+Words(value%1000));
    }
}
