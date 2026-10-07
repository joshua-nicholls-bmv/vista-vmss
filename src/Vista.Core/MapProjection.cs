namespace Vista.Core;
public static class MapProjection
{
    public static (double X,double Y) Project(double latitude,double longitude)
    {
        var lat=Math.Clamp(latitude,-85.05112878,85.05112878)*Math.PI/180;
        return ((longitude+180)/360,(1-Math.Log(Math.Tan(lat)+1/Math.Cos(lat))/Math.PI)/2);
    }
}
