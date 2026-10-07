namespace Vista.Core;
public static class MissionProgress
{
    public static double DistanceNm(double lat,double lon,decimal targetLat,decimal targetLon)
    {
        const double rad=Math.PI/180;var a=lat*rad;var b=(double)targetLat*rad;var d=b-a;var e=((double)targetLon-lon)*rad;
        var h=Math.Pow(Math.Sin(d/2),2)+Math.Cos(a)*Math.Cos(b)*Math.Pow(Math.Sin(e/2),2);
        return 3440.065*2*Math.Asin(Math.Sqrt(Math.Clamp(h,0,1)));
    }
    public static double Bearing(double lat,double lon,decimal targetLat,decimal targetLon)
    {
        const double rad=Math.PI/180;var a=lat*rad;var b=(double)targetLat*rad;var d=((double)targetLon-lon)*rad;
        return (Math.Atan2(Math.Sin(d)*Math.Cos(b),Math.Cos(a)*Math.Sin(b)-Math.Sin(a)*Math.Cos(b)*Math.Cos(d))/rad+360)%360;
    }
}
