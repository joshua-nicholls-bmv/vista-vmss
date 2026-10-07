namespace Vista.Core;
// Combustion indicates a running engine; starter/RPM alone can also indicate windmilling.
public sealed class EngineEventMonitor(bool observed=false)
{
    public bool HasObserved {get;private set;}=observed;
    private SimulatorTelemetry? previous;
    public event Action<string>? Changed;
    public void Interrupted()=>previous=null;
    public void Accept(SimulatorTelemetry sample)
    {
        if(previous is not null&&sample.CapturedAt<=previous.CapturedAt)return;
        var continuous=previous is not null&&sample.CapturedAt-previous.CapturedAt<=TimeSpan.FromSeconds(5);
        if(!HasObserved&&sample.EngineRunning)Changed?.Invoke("ENGINES · Already running when telemetry was first observed");
        else if(continuous&&previous!.EngineRunning!=sample.EngineRunning)
            Changed?.Invoke(sample.EngineRunning?"ENGINE START · Engine combustion detected":"ENGINE SHUTDOWN · All engines stopped");
        HasObserved=true;previous=sample;
    }
}
