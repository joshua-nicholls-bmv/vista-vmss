using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Vista.Core;
namespace Vista.Desktop;
public sealed class TrackMap:FrameworkElement
{
    public static readonly DependencyProperty RouteProperty=DependencyProperty.Register(nameof(Route),typeof(IEnumerable),typeof(TrackMap),new FrameworkPropertyMetadata(null,Changed));
    public static readonly DependencyProperty TrackProperty=DependencyProperty.Register(nameof(Track),typeof(IEnumerable),typeof(TrackMap),new FrameworkPropertyMetadata(null,Changed));
    public IEnumerable? Route {get=>(IEnumerable?)GetValue(RouteProperty);set=>SetValue(RouteProperty,value);}
    public IEnumerable? Track {get=>(IEnumerable?)GetValue(TrackProperty);set=>SetValue(TrackProperty,value);}
    public static readonly DependencyProperty DepartureProperty=DependencyProperty.Register(nameof(Departure),typeof(Airfield),typeof(TrackMap),new FrameworkPropertyMetadata(null,Changed));
    public static readonly DependencyProperty ArrivalProperty=DependencyProperty.Register(nameof(Arrival),typeof(Airfield),typeof(TrackMap),new FrameworkPropertyMetadata(null,Changed));
    public static readonly DependencyProperty NextPointIndexProperty=DependencyProperty.Register(nameof(NextPointIndex),typeof(int),typeof(TrackMap),new FrameworkPropertyMetadata(0,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FollowAircraftProperty=DependencyProperty.Register(nameof(FollowAircraft),typeof(bool),typeof(TrackMap),new FrameworkPropertyMetadata(false,FollowChanged));
    public Airfield? Departure {get=>(Airfield?)GetValue(DepartureProperty);set=>SetValue(DepartureProperty,value);}
    public Airfield? Arrival {get=>(Airfield?)GetValue(ArrivalProperty);set=>SetValue(ArrivalProperty,value);}
    public int NextPointIndex {get=>(int)GetValue(NextPointIndexProperty);set=>SetValue(NextPointIndexProperty,value);}
    public bool FollowAircraft {get=>(bool)GetValue(FollowAircraftProperty);set=>SetValue(FollowAircraftProperty,value);}
    private static void FollowChanged(DependencyObject d,DependencyPropertyChangedEventArgs e){var map=(TrackMap)d;if((bool)e.NewValue){map.manual=true;map.zoom=11;}map.InvalidateVisual();}
    private readonly Dictionary<string,ImageSource> tiles=[];
    private readonly HashSet<string> pending=[];private readonly Dictionary<string,DateTimeOffset> failed=[];
    private readonly DispatcherTimer retry=new(){Interval=TimeSpan.FromSeconds(30)};
    private double centerX=.495,centerY=.325;private int zoom=6;private bool fitted,manual;private Point? drag;
    public TrackMap(){ClipToBounds=true;Focusable=true;Cursor=Cursors.Hand;ToolTip="Scroll to zoom · drag to pan · double-click to fit the route";retry.Tick+=(_,_)=>InvalidateVisual();Loaded+=(_,_)=>{retry.Start();InvalidateVisual();};Unloaded+=(_,_)=>retry.Stop();SizeChanged+=(_,_)=>{if(!manual)fitted=false;InvalidateVisual();};}
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e)
    {var map=(TrackMap)d;if(e.OldValue is INotifyCollectionChanged old)old.CollectionChanged-=map.CollectionChanged;if(e.NewValue is INotifyCollectionChanged next)next.CollectionChanged+=map.CollectionChanged;map.fitted=false;map.InvalidateVisual();}
    private void CollectionChanged(object? sender,NotifyCollectionChangedEventArgs e){if(ReferenceEquals(sender,Route)){manual=false;fitted=false;}InvalidateVisual();}
    public void FitRoute(){SetCurrentValue(FollowAircraftProperty,false);manual=false;fitted=false;InvalidateVisual();}
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var p=e.GetPosition(this);var old=256*Math.Pow(2,zoom);var x=centerX+(p.X-ActualWidth/2)/old;var y=centerY+(p.Y-ActualHeight/2)/old;
        zoom=Math.Clamp(zoom+(e.Delta>0?1:-1),2,18);var size=256*Math.Pow(2,zoom);centerX=x-(p.X-ActualWidth/2)/size;centerY=Math.Clamp(y-(p.Y-ActualHeight/2)/size,0,1);SetCurrentValue(FollowAircraftProperty,false);manual=true;InvalidateVisual();e.Handled=true;
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){if(e.ClickCount==2){FitRoute();return;}drag=e.GetPosition(this);CaptureMouse();e.Handled=true;}
    protected override void OnMouseMove(MouseEventArgs e){if(drag is not Point old)return;var p=e.GetPosition(this);var size=256*Math.Pow(2,zoom);centerX-=(p.X-old.X)/size;centerY=Math.Clamp(centerY-(p.Y-old.Y)/size,0,1);drag=p;SetCurrentValue(FollowAircraftProperty,false);manual=true;InvalidateVisual();}
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){drag=null;ReleaseMouseCapture();}
    protected override void OnLostMouseCapture(MouseEventArgs e){drag=null;}
    private async void Request(int z,int x,int y,string key)
    {
        if(!MapTiles.Enabled||pending.Count>=24||!IsLoaded||pending.Contains(key)||failed.TryGetValue(key,out var at)&&DateTimeOffset.UtcNow-at<TimeSpan.FromSeconds(30))return;
        pending.Add(key);
        var image=await MapTiles.Load(z,x,y);pending.Remove(key);
        if(image is not null){if(tiles.Count>=192)tiles.Remove(tiles.Keys.First());tiles[key]=image;failed.Remove(key);}else failed[key]=DateTimeOffset.UtcNow;
        if(IsLoaded)InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(ActualWidth<1||ActualHeight<1)return;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(32,43,52)),null,new Rect(0,0,ActualWidth,ActualHeight));
        var route=Route?.Cast<StoredWaypoint>().OrderBy(p=>p.Position).ToList()??[];
        var track=Track?.Cast<SimulatorTelemetry>().Where(p=>double.IsFinite(p.Latitude)&&double.IsFinite(p.Longitude)&&Math.Abs(p.Latitude)<=90&&Math.Abs(p.Longitude)<=180).ToList()??[];
        Point World(double lat,double lon){var p=MapProjection.Project(lat,lon);return new(p.X,p.Y);}
        var journey=route.Select(p=>World((double)p.Latitude,(double)p.Longitude)).ToList();
        if(Departure?.Latitude is decimal depLat&&Departure.Longitude is decimal depLon)journey.Insert(0,World((double)depLat,(double)depLon));
        if(Arrival?.Latitude is decimal arrLat&&Arrival.Longitude is decimal arrLon)journey.Add(World((double)arrLat,(double)arrLon));
        var all=journey.Concat(track.Select(p=>World(p.Latitude,p.Longitude))).ToList();
        if(FollowAircraft&&track.Count>0){var current=World(track[^1].Latitude,track[^1].Longitude);centerX=current.X;centerY=current.Y;}
        if(!manual&&(!fitted||track.Count>0))
        {
            if(all.Count>0){var minX=all.Min(p=>p.X);var maxX=all.Max(p=>p.X);var minY=all.Min(p=>p.Y);var maxY=all.Max(p=>p.Y);centerX=(minX+maxX)/2;centerY=(minY+maxY)/2;var scale=Math.Min(Math.Max(1,ActualWidth-90)/Math.Max(.0004,maxX-minX),Math.Max(1,ActualHeight-90)/Math.Max(.0004,maxY-minY));zoom=Math.Clamp((int)Math.Floor(Math.Log2(scale/256)),2,14);}fitted=true;
        }
        var size=256*Math.Pow(2,zoom);var left=centerX*size-ActualWidth/2;var top=centerY*size-ActualHeight/2;var n=1<<zoom;var visible=0;var unavailable=0;var failedVisible=0;
        for(var x=(int)Math.Floor(left/256);x<=(int)Math.Floor((left+ActualWidth)/256);x++)for(var y=(int)Math.Floor(top/256);y<=(int)Math.Floor((top+ActualHeight)/256);y++)
        {
            if(y<0||y>=n)continue;var wrapped=(x%n+n)%n;var key=$"{zoom}/{wrapped}/{y}";var rect=new Rect(x*256-left,y*256-top,256,256);
            if(tiles.TryGetValue(key,out var image)){dc.DrawImage(image,rect);visible++;}else{unavailable++;if(failed.ContainsKey(key))failedVisible++;Request(zoom,wrapped,y,key);}
        }
        // A subtle tint improves route contrast while retaining place labels.
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(24,24,33,41)),null,new Rect(0,0,ActualWidth,ActualHeight));
        Point Screen(double lat,double lon){var p=World(lat,lon);return new(p.X*size-left,p.Y*size-top);}
        var routePen=new Pen(new SolidColorBrush(Color.FromRgb(38,112,145)),3);var trackPen=new Pen(new SolidColorBrush(Color.FromRgb(219,70,18)),3);
        for(var i=1;i<journey.Count;i++)dc.DrawLine(routePen,new(journey[i-1].X*size-left,journey[i-1].Y*size-top),new(journey[i].X*size-left,journey[i].Y*size-top));
        for(var i=1;i<track.Count;i++)if(track[i].CapturedAt-track[i-1].CapturedAt<=TimeSpan.FromSeconds(15))dc.DrawLine(trackPen,Screen(track[i-1].Latitude,track[i-1].Longitude),Screen(track[i].Latitude,track[i].Longitude));
        for(var i=0;i<route.Count;i++)
        {
            var p=route[i];var point=Screen((double)p.Latitude,(double)p.Longitude);var color=i<NextPointIndex?Brushes.MediumSeaGreen:i==NextPointIndex?Brushes.Gold:Brushes.White;
            dc.DrawEllipse(color,routePen,point,i==NextPointIndex?7:4,i==NextPointIndex?7:4);Label(dc,p.Identifier,new(point.X+9,point.Y-10));
        }
        void Airport(Airfield? field,string role,Brush color)
        {
            if(field?.Latitude is not decimal lat||field.Longitude is not decimal lon)return;
            var point=Screen((double)lat,(double)lon);dc.DrawEllipse(color,new Pen(Brushes.White,2),point,7,7);Label(dc,field.Icao+" · "+role,new(point.X+10,point.Y+7));
        }
        var returnToBase=Departure is not null&&Arrival is not null&&Departure.Icao==Arrival.Icao;
        Airport(Departure,returnToBase?"Departure / RTB":"Departure",Brushes.MediumSeaGreen);
        if(!returnToBase)Airport(Arrival,"Arrival",Brushes.OrangeRed);
        if(track.Count>0)
        {
            var aircraft=track[^1];var point=Screen(aircraft.Latitude,aircraft.Longitude);dc.PushTransform(new RotateTransform(double.IsFinite(aircraft.HeadingDegrees)?aircraft.HeadingDegrees:0,point.X,point.Y));
            var shape=new StreamGeometry();using(var context=shape.Open()){context.BeginFigure(new(point.X,point.Y-13),true,true);context.LineTo(new(point.X+8,point.Y+9),true,false);context.LineTo(new(point.X,point.Y+5),true,false);context.LineTo(new(point.X-8,point.Y+9),true,false);}shape.Freeze();dc.DrawGeometry(Brushes.White,trackPen,shape);dc.Pop();
        }
        if(Departure is not null&&(Departure.Latitude is null||Departure.Longitude is null)||Arrival is not null&&(Arrival.Latitude is null||Arrival.Longitude is null))Label(dc,"Airfield coordinates missing · apply VISTA migration 008",new(10,34));
        if(unavailable>0)Label(dc,failedVisible>0?(visible==0?"Map unavailable · check internet connection":"Some map tiles unavailable · retrying"):"Loading map…",new(10,10));
        Label(dc,"© OpenStreetMap contributors",new(Math.Max(5,ActualWidth-205),Math.Max(5,ActualHeight-26)));
    }
    private void Label(DrawingContext dc,string value,Point position)
    {var text=new FormattedText(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(210,24,33,41)),null,new Rect(position.X-3,position.Y-2,text.Width+6,text.Height+4),2,2);dc.DrawText(text,position);}
}
