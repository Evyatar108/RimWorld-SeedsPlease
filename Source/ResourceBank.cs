namespace SeedsPleaseLite;

using RimWorld;
using Verse;

public static class ResourceBank
{
    [DefOf]
    public static class Defs
    {
        public static JobDef SowWithSeeds;
        public static ThingCategoryDef SeedExtractable, SeedsCategory;
        public static RecipeDef ExtractSeeds;
    }
}