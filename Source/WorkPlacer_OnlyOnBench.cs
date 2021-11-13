using Verse;
using RimWorld;
using System.Linq;

namespace SeedsPleaseLite
{
	public class WorkPlacer_OnlyOnBench : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{		
			//Try to determine if this is a workbench that deals with food
			if (map.thingGrid.ThingsAt(loc).Any(x => x.def.building != null && x.def.building.isMealSource && x.def.thingClass != typeof(Building_NutrientPasteDispenser))) return true;
			return new AcceptanceReport("Must be placed on a stove's surface.");
		}

		public WorkPlacer_OnlyOnBench()
		{
		}
	}
}
