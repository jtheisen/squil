namespace Squil;

public record CurrentLocation(String Location);

public static partial class Extensions
{
    public static RelatedEntities GetRelatedEntities(this RelatedEntities[] relatedEntities, String alias)
        => relatedEntities.Where(e => e.Extent.RelationAlias == alias).Single($"Unexpectedly no single '{alias}' data");

    public static RelatedEntities GetRelatedEntitiesOrNull(this RelatedEntities[] relatedEntities, String alias)
        => relatedEntities.Where(e => e.Extent.RelationAlias == alias).SingleOrDefault($"Unexpectedly multiple '{alias}' data");

    public static QuerySearchMode? GetDefaultSearchMode(this IWithUsedKb cmo, AppSettings settings)
        => cmo.UsedKb?.Apply(kb => kb <= settings.PreferScanningUnderKb ? QuerySearchMode.Scan : QuerySearchMode.Seek);
}
