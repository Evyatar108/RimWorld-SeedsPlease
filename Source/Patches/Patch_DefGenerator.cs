using Verse;
using RimWorld;
using HarmonyLib;
using System.Linq;
 
namespace SeedsPleaseLite
{
    [HarmonyPatch (typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PostResolve))]
    static class Patch_DefGenerator
    {
        static void Postfix()
        {
            var seedDatabase = DefDatabase<ThingDef>.AllDefs.Where(x => x.HasComp(typeof(CompSeed))).ToList();
            foreach (var seed in seedDatabase)
            {
                SeedsPleaseMod.ResolveSpecialReferences(seed, seed.GetCompProperties<CompProperties_Seed>());
            }

            var report = new System.Text.StringBuilder();
            if (SeedsPleaseMod.AddMissingSeeds(report))
            {
                ResourceCounter.ResetDefs();

                Log.Warning("SeedsPlease :: Some Seeds were autogenerated.\nDon't rely on autogenerated seeds, share the generated xml for proper support.\n\n" + report);
            }
            SeedsPleaseMod.AddButchery();
        }
    }
}