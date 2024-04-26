namespace SeedsPleaseLite;

using RimWorld;
using Verse;
using System.Linq;

/// <summary>
/// This is responsible for the seed extraction recipe's ability to do the "Do until X" bill type, explaining how/what to count.
/// </summary>
public class RecipeWorkerCounter_ExtractSeeds : RecipeWorkerCounter
{
    public override bool CanCountProducts(Bill_Production bill)
    {
        if (bill.ingredientFilter.AllowedThingDefs.Count<ThingDef>() != 1)
        {
            LongEventHandler.QueueLongEvent(() => HelperMessage(), null, false, null);
            return false;
        }
        return true;
    }

    void HelperMessage()
    {
        Messages.Clear();
        Messages.Message("SPL.BillHelp".Translate(), MessageTypeDefOf.RejectInput, false);
    }

    public override int CountProducts(Bill_Production bill)
    {
        if (bill.ingredientFilter.AllowedThingDefs.Count<ThingDef>() != 1)
        {
            return 0;
        }

        return bill.Map.resourceCounter.GetCount(bill.ingredientFilter.AllowedThingDefs.First<ThingDef>().butcherProducts[0].thingDef);
    }

    public override string ProductsDescription(Bill_Production bill)
    {
        if (bill.ingredientFilter.AllowedThingDefs.Count<ThingDef>() != 1)
        {
            return "Invalid";
        }

        return bill.ingredientFilter.AllowedThingDefs.First<ThingDef>().butcherProducts[0].thingDef.label;
    }

    public override bool CanPossiblyStore(Bill_Production bill, ISlotGroup slotGroup)
    {
        if (bill.ingredientFilter.AllowedThingDefs.Count<ThingDef>() != 1)
        {
            return false;
        }

        if (!slotGroup.Settings.AllowedToAccept(bill.ingredientFilter.AllowedThingDefs.First<ThingDef>()))
        {
            return false;
        }
        return true;
    }
}
