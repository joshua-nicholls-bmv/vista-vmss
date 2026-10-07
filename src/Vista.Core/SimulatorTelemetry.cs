namespace Vista.Core;

public sealed record SimulatorTelemetry(DateTimeOffset CapturedAt, string AircraftTitle,
    double AltitudeFt, double GroundSpeedKts, double HeadingDegrees, double Latitude, double Longitude,
    bool OnGround, bool ParkingBrake, bool EngineRunning, double FuelLb, double VerticalSpeedFpm, double RadioAltitudeFt);

// OUT/OFF/ON/IN rules extracted from British Midland's ProcessFlightTelemetry.
// Each instance represents one flight; database transitions are handled separately.
public sealed class FlightTracker
{
    private EngineEventMonitor engines;
    public FlightTracker(bool engineObserved=false){engines=new(engineObserved);engines.Changed+=text=>FlightEvent?.Invoke(text);}
    private SimulatorTelemetry? previous;
    private double lastAirborneVerticalSpeed;
    private DateTimeOffset? parkedSince;
    public DateTimeOffset? OutTime { get; private set; }
    public DateTimeOffset? OffTime { get; private set; }
    public DateTimeOffset? OnTime { get; private set; }
    public DateTimeOffset? InTime { get; private set; }
    public int Takeoffs { get; private set; }
    public int Landings { get; private set; }
    public double? LandingRateFpm { get; private set; }
    public double? StartFuelLb { get; private set; }
    public double? EndFuelLb { get; private set; }
    public double MaxAltitudeFt { get; private set; }
    public double MaxGroundSpeedKts { get; private set; }
    public event Action<string>? FlightEvent;

    public FlightTrackerState Capture() => new(OutTime,OffTime,OnTime,InTime,Takeoffs,Landings,LandingRateFpm,StartFuelLb,EndFuelLb,MaxAltitudeFt,MaxGroundSpeedKts,engines.HasObserved);
    public static FlightTracker Restore(FlightTrackerState state) => new(state.EngineObserved) { OutTime=state.OutTime,OffTime=state.OffTime,OnTime=state.OnTime,InTime=state.InTime,Takeoffs=state.Takeoffs,Landings=state.Landings,LandingRateFpm=state.LandingRateFpm,StartFuelLb=state.StartFuelLb,EndFuelLb=state.EndFuelLb,MaxAltitudeFt=state.MaxAltitudeFt,MaxGroundSpeedKts=state.MaxGroundSpeedKts };
    public void ConnectionInterrupted() { previous = null; parkedSince = null; engines.Interrupted(); }
    public void Accept(SimulatorTelemetry sample)
    {
        if (!double.IsFinite(sample.Latitude) || !double.IsFinite(sample.Longitude)
            || sample.Latitude is < -90 or > 90 || sample.Longitude is < -180 or > 180
            || !double.IsFinite(sample.AltitudeFt) || !double.IsFinite(sample.GroundSpeedKts)
            || !double.IsFinite(sample.VerticalSpeedFpm)) return;
        if (previous is not null && sample.CapturedAt <= previous.CapturedAt) return;
        // Do not infer a takeoff or landing across a telemetry gap.
        if (previous is not null && sample.CapturedAt - previous.CapturedAt > TimeSpan.FromSeconds(5)) ConnectionInterrupted();
        engines.Accept(sample);
        if (sample.FuelLb > 0 && double.IsFinite(sample.FuelLb))
        {
            EndFuelLb = sample.FuelLb;
            if (sample.EngineRunning) StartFuelLb ??= sample.FuelLb;
        }
        MaxAltitudeFt = Math.Max(MaxAltitudeFt, sample.AltitudeFt);
        MaxGroundSpeedKts = Math.Max(MaxGroundSpeedKts, sample.GroundSpeedKts);
        if (!sample.OnGround) lastAirborneVerticalSpeed = sample.VerticalSpeedFpm;
        if (OutTime is null && sample.OnGround && !sample.ParkingBrake && sample.GroundSpeedKts > 1)
        { OutTime = sample.CapturedAt; FlightEvent?.Invoke("OUT · Aircraft left stand"); }
        if (previous is { OnGround: true } && !sample.OnGround)
        {
            Takeoffs++; OffTime ??= sample.CapturedAt; InTime = null;
            FlightEvent?.Invoke($"OFF · Takeoff #{Takeoffs}");
        }
        if (previous is { OnGround: false } && sample.OnGround && Takeoffs > 0)
        {
            Landings++; OnTime = sample.CapturedAt; LandingRateFpm = lastAirborneVerticalSpeed;
            FlightEvent?.Invoke($"ON · Landing #{Landings} · {LandingRateFpm:0} fpm");
        }
        if (Landings > 0 && sample.OnGround && sample.ParkingBrake && sample.GroundSpeedKts < 1)
        {
            parkedSince ??= sample.CapturedAt;
            if (InTime is null && sample.CapturedAt - parkedSince >= TimeSpan.FromSeconds(3))
            { InTime = sample.CapturedAt; FlightEvent?.Invoke("IN · Aircraft parked"); }
        }
        else parkedSince = null;
        previous = sample;
    }
}

public sealed record FlightTrackerState(DateTimeOffset? OutTime,DateTimeOffset? OffTime,DateTimeOffset? OnTime,DateTimeOffset? InTime,int Takeoffs,int Landings,double? LandingRateFpm,double? StartFuelLb,double? EndFuelLb,double MaxAltitudeFt,double MaxGroundSpeedKts,bool EngineObserved=false);
