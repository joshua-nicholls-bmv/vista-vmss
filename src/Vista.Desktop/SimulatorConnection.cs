using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.FlightSimulator.SimConnect;
using Vista.Core;

namespace Vista.Desktop;

// Extracted from BM ACARS: window-message transport, SECOND data requests,
// packed aircraft definition, handshake timeout and safe disconnect handling.
public sealed class SimulatorConnection : IDisposable
{
    private const int MessageId = 0x0402;
    private enum Definitions { Aircraft }
    private enum Requests { Aircraft }
    private SimConnect? connection;
    private HwndSource? source;
    private readonly DispatcherTimer timeout = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer freshness = new() { Interval = TimeSpan.FromSeconds(2) };
    private DateTimeOffset lastSample;
    public event Action<string>? StatusChanged;
    public event Action<SimulatorTelemetry>? TelemetryReceived;
    public event Action? Interrupted;

    public SimulatorConnection()
    {
        timeout.Tick += (_, _) => Disconnect("Connection timed out. Open a flight in MSFS and try again.");
        freshness.Tick += (_, _) =>
        {
            if (DateTimeOffset.UtcNow - lastSample > TimeSpan.FromSeconds(10))
                Disconnect("Telemetry stopped. Reconnect when the simulator is ready.");
        };
    }
    public void Attach(IntPtr handle)
    {
        source = HwndSource.FromHwnd(handle) ?? throw new InvalidOperationException("VISTA window is not ready.");
        source.AddHook(WindowMessage);
    }
    public void Connect()
    {
        Disconnect("Connecting to MSFS…");
        if (source is null) { StatusChanged?.Invoke("VISTA window is not ready."); return; }
        try
        {
            connection = new SimConnect("VISTA · 69th Expeditionary Air Wing", source.Handle, MessageId, null, 0);
            connection.OnRecvOpen += (_, _) =>
            {
                timeout.Stop(); RegisterAircraftData(); lastSample = DateTimeOffset.UtcNow;
                freshness.Start(); StatusChanged?.Invoke("MSFS connected · waiting for aircraft telemetry");
            };
            connection.OnRecvQuit += (_, _) => Disconnect("MSFS disconnected. Reconnect to resume telemetry.");
            connection.OnRecvException += (_, _) => Disconnect("MSFS rejected a telemetry request. Reconnect and try again.");
            connection.OnRecvSimobjectData += (_, data) =>
            {
                if (data.dwRequestID != (uint)Requests.Aircraft) return;
                var aircraft = (AircraftData)data.dwData[0]; lastSample = DateTimeOffset.UtcNow;
                if (!double.IsFinite(aircraft.Latitude) || !double.IsFinite(aircraft.Longitude)
                    || aircraft.Latitude is < -90 or > 90 || aircraft.Longitude is < -180 or > 180
                    || !double.IsFinite(aircraft.Altitude) || !double.IsFinite(aircraft.GroundSpeed)
                    || !double.IsFinite(aircraft.VerticalSpeed)) return;
                TelemetryReceived?.Invoke(new(lastSample, aircraft.Title, aircraft.Altitude, aircraft.GroundSpeed,
                    aircraft.Heading, aircraft.Latitude, aircraft.Longitude, aircraft.OnGround == 1,
                    aircraft.ParkingBrake > .5, aircraft.Engine1Combustion != 0 || aircraft.Engine2Combustion != 0
                        || aircraft.Engine3Combustion != 0 || aircraft.Engine4Combustion != 0,
                    aircraft.Fuel, aircraft.VerticalSpeed, aircraft.RadioAltitude));
            };
            timeout.Start();
        }
        catch (Exception e) when (e is COMException or DllNotFoundException or BadImageFormatException or System.IO.FileNotFoundException or System.IO.FileLoadException)
        { Disconnect("Could not connect to MSFS. Check the simulator is running and the SimConnect SDK libraries are installed."); }
    }
    public void Disconnect(string message = "MSFS disconnected")
    {
        timeout.Stop(); freshness.Stop();
        var old = connection; connection = null;
        try { old?.Dispose(); } catch (COMException) { }
        Interrupted?.Invoke(); StatusChanged?.Invoke(message);
    }
    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == MessageId && connection is not null)
        {
            try { connection.ReceiveMessage(); }
            catch (COMException) { Disconnect("Simulator connection lost. Reconnect to resume telemetry."); }
            handled = true;
        }
        return IntPtr.Zero;
    }
    public void Dispose() { Disconnect(); source?.RemoveHook(WindowMessage); source = null; }

        [StructLayout(
            LayoutKind.Sequential,
            CharSet = CharSet.Ansi,
            Pack = 1)]
        private struct AircraftData
        {
            [MarshalAs(
                UnmanagedType.ByValTStr,
                SizeConst = 256)]
            public string Title;

            public double Altitude;
            public double GroundSpeed;
            public double Heading;
            public double Latitude;
            public double Longitude;

            public int OnGround;

            public double ParkingBrake;

            public double Engine1;
            public double Engine2;

            public double Fuel;

            public double VerticalSpeed;
            public double RadioAltitude;

            public int Engine1Combustion;
            public int Engine2Combustion;
            public int Engine3Combustion;
            public int Engine4Combustion;
        }

        private void RegisterAircraftData()
        {
            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "TITLE",
                null,
                SIMCONNECT_DATATYPE.STRING256,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "PLANE ALTITUDE",
                "feet",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "GROUND VELOCITY",
                "knots",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "PLANE HEADING DEGREES TRUE",
                "degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "PLANE LATITUDE",
                "degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "PLANE LONGITUDE",
                "degrees",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "SIM ON GROUND",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "BRAKE PARKING POSITION",
                "Position",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "GENERAL ENG RPM:1",
                "rpm",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "GENERAL ENG RPM:2",
                "rpm",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "FUEL TOTAL QUANTITY WEIGHT",
                "pounds",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "VERTICAL SPEED",
                "feet per minute",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "RADIO HEIGHT",
                "feet",
                SIMCONNECT_DATATYPE.FLOAT64,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "GENERAL ENG COMBUSTION:1",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(
                Definitions.Aircraft,
                "GENERAL ENG COMBUSTION:2",
                "Bool",
                SIMCONNECT_DATATYPE.INT32,
                0,
                SimConnect.SIMCONNECT_UNUSED);

            connection!.AddToDataDefinition(Definitions.Aircraft, "GENERAL ENG COMBUSTION:3", "Bool",
                SIMCONNECT_DATATYPE.INT32, 0, SimConnect.SIMCONNECT_UNUSED);
            connection!.AddToDataDefinition(Definitions.Aircraft, "GENERAL ENG COMBUSTION:4", "Bool",
                SIMCONNECT_DATATYPE.INT32, 0, SimConnect.SIMCONNECT_UNUSED);
            connection!.RegisterDataDefineStruct<AircraftData>(
                Definitions.Aircraft);

            connection!.RequestDataOnSimObject(
                Requests.Aircraft,
                Definitions.Aircraft,
                SimConnect.SIMCONNECT_OBJECT_ID_USER,
                SIMCONNECT_PERIOD.SECOND,
                SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                0,
                0,
                0);
        }
}

