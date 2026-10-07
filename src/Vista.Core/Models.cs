using System.Text.Json.Serialization;

namespace Vista.Core;

public sealed record VistaConfiguration
{
    public string SupabaseUrl { get; init; } = "";
    public string PublishableKey { get; init; } = "";
    public string MapTileUrlTemplate { get; init; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";
    public string SimbriefAtlasAirframeId { get; init; } = "";
    public string SimbriefTyphoonAirframeId { get; init; } = "";
}
public sealed record Pilot
{
    public Guid Id { get; init; }
    public Guid AuthUserId { get; init; }
    public string PilotNumber { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? Callsign { get; init; }
    public string DisplayLabel { get; init; } = "";
    public string Role { get; init; } = "";
    public string Status { get; init; } = "";
    public string? SimbriefUserId { get; init; }
}
public sealed record Airfield
{
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public Guid Id { get; init; }
    public string Icao { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public bool Active { get; init; }
    [JsonIgnore] public string Label => $"{Icao} · {Name}";
}
public sealed record AircraftType
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Active { get; init; }
    public string? SimbriefAircraftCode { get; init; }
    public string? SimbriefProfileId { get; init; }
}
public sealed record Squadron
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public Guid BaseId { get; init; }
    public bool Active { get; init; }
}
public sealed record Aircraft
{
    public Guid Id { get; init; }
    public string Serial { get; init; } = "";
    public Guid AircraftTypeId { get; init; }
    public Guid? SquadronId { get; init; }
    public Guid HomeBaseId { get; init; }
    public Guid? CurrentBaseId { get; init; }
    public string Status { get; init; } = "";
}
public sealed record FleetAircraft(Aircraft Aircraft, string Type, string Base, string Unit)
{
    public FleetReservation? Reservation {get;init;}
    public string Availability=>Reservation?.FlightState??(Status=="available"?"Available":Status);
    public string AssignedPilot=>Reservation?.PilotLabel??"—";
    public Guid Id => Aircraft.Id;
    public string Serial => Aircraft.Serial;
    public string Status => Aircraft.Status;
    public string Label => $"{Serial} · {Type} · {Base}";
}
public sealed record Mission
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public Guid DepartureBaseId { get; init; }
    public Guid ArrivalBaseId { get; init; }
    public Guid? AircraftTypeId { get; init; }
    public Guid? SquadronId { get; init; }
    public int Revision { get; init; }
    public bool Active { get; init; }
}
public sealed record MissionElement
{
    public string Code { get; init; } = "";
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Active { get; init; }
}
public sealed record StoredWaypoint
{
    public Guid? MissionElementId { get; init; }
    public Guid Id { get; init; }
    public int Position { get; init; }
    public string Identifier { get; init; } = "";
    public decimal Latitude { get; init; }
    public decimal Longitude { get; init; }
    public int? AltitudeFt { get; init; }
    public int? SpeedKts { get; init; }
    public string Instructions { get; init; } = "";
    public Guid? SourceElementWaypointId { get; init; }
    public Guid? ElementInstance { get; init; }
}
public sealed record PlanWaypoint(string Identifier, decimal Latitude, decimal Longitude, int? AltitudeFt,
    int? SpeedKts, string Instructions, Guid? SourceElementWaypointId = null, Guid? ElementInstance = null);
public sealed record SortiePlan(string Source, string Title, Guid AircraftId, string Callsign,
    Guid? DepartureBaseId, Guid? ArrivalBaseId, Guid? AlternateBaseId, Guid? MissionId,
    string RouteText, DateTimeOffset? PlannedDepartureAt, IReadOnlyList<PlanWaypoint> Waypoints);
public sealed record MissionArchive(Guid PilotId, Guid PlanId);
public sealed record Sortie
{
    public Guid? MissionPlanId {get;init;}
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public System.Text.Json.JsonElement PlanSnapshot { get; init; }
    public Guid Id { get; init; }
    public Guid PilotId { get; init; }
    public Guid AircraftId { get; init; }
    public string Source { get; init; } = "";
    public Guid? MissionId { get; init; }
    public string Title { get; init; } = "";
    public Guid DepartureBaseId { get; init; }
    public Guid ArrivalBaseId { get; init; }
    public Guid? AlternateBaseId { get; init; }
    public string Callsign { get; init; } = "";
    public string RouteText { get; init; } = "";
    public string Status { get; init; } = "";
    public DateTimeOffset? PlannedDepartureAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    [JsonIgnore] public string DisplayStatus => Status is "briefed" or "airborne" ? "ACTIVE" : Status=="completed" ? "Previously flown" : "Not yet flown";
    [JsonIgnore] public string SquadronLabel { get; init; } = "Unassigned";
    [JsonIgnore] public DateTime CreatedLocal => CreatedAt.LocalDateTime;
}
public sealed record PilotStatistics
{
    public long CompletedSorties { get; init; }
    public decimal FlightHours { get; init; }
    public decimal DistanceNm { get; init; }
}
public sealed record Catalogue(IReadOnlyList<Airfield> Bases, IReadOnlyList<AircraftType> Types,
    IReadOnlyList<Squadron> Squadrons, IReadOnlyList<Aircraft> Aircraft,
    IReadOnlyList<Mission> Missions, IReadOnlyList<MissionElement> Elements);

public sealed record SharedMission
{
 public Guid Id {get;init;}
 public Guid OwnerId {get;init;}
 public string Title {get;init;}="";
 public string Squadron {get;init;}="";
 public string Author {get;init;}="";
 public string DepartureIcao {get;init;}="";
 public string ArrivalIcao {get;init;}="";
 public int Revision {get;init;}
 public bool Active {get;init;}
 public DateTimeOffset UpdatedAt {get;init;}
 public string Route=>$"{DepartureIcao} → {ArrivalIcao}";
}
public sealed record FleetReservation(Guid AircraftId,string PilotLabel,string Callsign,string MissionTitle,string FlightState,DateTimeOffset? StartedAt);
