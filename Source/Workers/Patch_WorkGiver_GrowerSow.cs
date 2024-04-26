namespace SeedsPleaseLite;

using System;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

//This patch controls the dropping of seeds upon harvest
[HarmonyPatch]
public class Patch_WorkGiver_GrowerSow_JobOnCell
{
    const int SeedsToCarry = 25;
    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(WorkGiver_GrowerSow), nameof(WorkGiver_GrowerSow.JobOnCell));

        //Try patch support for VE More Plants
        MethodInfo WorkGiver_GrowerSowSandy_JobOnCell = AccessTools.TypeByName("VanillaPlantsExpandedMorePlants.WorkGiver_GrowerSowSandy")?.GetMethod("JobOnCell");
        MethodInfo WorkGiver_GrowerSowAquatic_JobOnCell = AccessTools.TypeByName("VanillaPlantsExpandedMorePlants.WorkGiver_GrowerSowAquatic")?.GetMethod("JobOnCell");
        if (WorkGiver_GrowerSowSandy_JobOnCell != null && WorkGiver_GrowerSowAquatic_JobOnCell != null)
        {
            yield return WorkGiver_GrowerSowSandy_JobOnCell;
            yield return WorkGiver_GrowerSowAquatic_JobOnCell;
        }

        MethodInfo WorkGiver_GrowerSowMushroom_JobOnCell = AccessTools.TypeByName("VanillaPlantsExpandedMushrooms.WorkGiver_GrowerSowMushroom")?.GetMethod("JobOnCell");
        if (WorkGiver_GrowerSowMushroom_JobOnCell != null)
        {
            yield return WorkGiver_GrowerSowMushroom_JobOnCell;
        }
    }

    public static Job Postfix(Job __result, Pawn pawn, IntVec3 c)
    {
        if (__result == null || __result.def != JobDefOf.Sow)
        {
            return __result;
        }

        ThingDef seed = __result.plantDefToSow?.blueprintDef;
        if (seed == null || seed.thingCategories.NullOrEmpty())
        {
            return __result;
        }

        Map map = pawn.Map;
        if (ModSettings_SeedsPleaseLiteRedux.clearSnow && NeedsToClearSnowFirst(c, map, pawn, ref __result))
        {
            return __result;
        }

        //Predicate filtering the kind of seed allowed
        Predicate<Thing> predicate = tempThing =>
            !ForbidUtility.IsForbidden(tempThing, pawn.Faction)
            && ForbidUtility.InAllowedArea(tempThing.Position, pawn)
            && PawnLocalAwareness.AnimalAwareOf(pawn, tempThing)
            && ReservationUtility.CanReserve(pawn, tempThing, 1);

        //Find the instance on the map to go fetch
        Thing bestSeedThingForSowing = GenClosest.ClosestThingReachable(c, map, ThingRequest.ForDef(seed), PathEndMode.ClosestTouch, TraverseParms.For(pawn), validator: predicate);

        return bestSeedThingForSowing == null ? null : new Job(ResourceBank.Defs.SowWithSeeds, c, bestSeedThingForSowing)
        {
            plantDefToSow = __result.plantDefToSow,
            count = SeedsToCarry
        };
    }

    static bool NeedsToClearSnowFirst(IntVec3 cell, Map map, Pawn pawn, ref Job job)
    {
        var zoneCells = cell.GetZone(map)?.cells;
        if (!PlantUtility.SnowAllowsPlanting(cell, map))
        {
            for (int i = zoneCells?.Count ?? 0; i-- > 0;)
            {
                Job clearSnowJob = JobMaker.MakeJob(JobDefOf.ClearSnow, cell);
                if (clearSnowJob.MakeDriver(pawn).TryMakePreToilReservations(false))
                {
                    pawn.ClearReservationsForJob(clearSnowJob);
                    job = clearSnowJob;
                    return true;
                }
            }
        }

        return false;
    }
}