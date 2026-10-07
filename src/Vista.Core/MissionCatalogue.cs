namespace Vista.Core;

public sealed record MissionFamily
{
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string[] DepartureIcaos { get; init; } = [];
    public string[] ReturnIcaos { get; init; } = [];
    public bool Enabled { get; init; }
}
public sealed record FamilyAircraftType(string FamilyCode, Guid AircraftTypeId);
public sealed record CatalogueOption
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string FamilyCode { get; init; } = "";
    public string Title { get; init; } = "";
    public Guid? ElementId { get; init; }
    public Guid? DestinationBaseId { get; init; }
    public bool Enabled { get; init; }
}
public sealed record PayloadPreset
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Kind { get; init; } = "";
    public int PayloadKg { get; init; }
    public int PersonnelCount { get; init; }
    public bool Enabled { get; init; }
    public string Priority { get; init; } = "";
    public string Label => $"{Title} · {PayloadKg:N0} kg";
}
public sealed record PayloadDestination(Guid PayloadId, Guid BaseId);
public sealed record FamilyPayload(string FamilyCode, Guid PayloadId);
public sealed record MissionCatalogue(IReadOnlyList<MissionFamily> Families, IReadOnlyList<FamilyAircraftType> AircraftTypes,
    IReadOnlyList<CatalogueOption> Options, IReadOnlyList<PayloadPreset> Payloads,
    IReadOnlyList<PayloadDestination> PayloadDestinations, IReadOnlyList<FamilyPayload> FamilyPayloads,
    IReadOnlyList<StoredWaypoint> ElementWaypoints, IReadOnlyList<MissionElement> AllElements);
public sealed record CatalogueTask(Guid OptionId, Guid? PayloadId);
public sealed record CataloguePlan(string Title, string Callsign, Guid AircraftId, Guid DepartureBaseId,
    Guid ArrivalBaseId, Guid? AlternateBaseId, bool CorridorOutbound, bool CorridorReturn,
    IReadOnlyList<CatalogueTask> Tasks, DateTimeOffset? PlannedDepartureAt = null);
