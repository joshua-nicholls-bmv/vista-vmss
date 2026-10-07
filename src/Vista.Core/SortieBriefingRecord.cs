namespace Vista.Core;
public sealed record SortieBriefingRecord
{
    public Guid SortieId {get;init;}
    public string CurrentPlanKey {get;init;}="";
    public string? PlanKey {get;init;}
    public Guid? Revision {get;init;}
    public SimBriefBriefing? Document {get;init;}
    public string MissionNotes {get;init;}="";
    public DateTimeOffset? ImportedAt {get;init;}
    public DateTimeOffset? SignedAt {get;init;}
    public Guid? SignedBy {get;init;}
    public string? SignedLabel {get;init;}
    public bool IsCurrent=>Document is not null&&PlanKey==CurrentPlanKey;
    public bool IsSigned=>IsCurrent&&SignedAt is not null;
}
