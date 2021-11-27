using System.Collections.Generic;
using System.Linq;
using System;
using RimWorld;
using Verse;
using UnityEngine;
using System.Xml.Linq;
using System.Text;
using static SeedsPleaseLite.ResourceBank.ThingDefOf;
using static SeedsPleaseLite.ResourceBank.ThingCategoryDefOf;
using static SeedsPleaseLite.ResourceBank.RecipeDefOf;

namespace SeedsPleaseLite
{
    public static class SeedsPleaseUtility
    {
        static List<ThingDef> plantsToCheck = new List<ThingDef>();
        static List<(ThingDef, Seed)> butcheryToResolve = new List<(ThingDef, Seed)>();
        public static void Setup()
        {
            var timer = new System.Diagnostics.Stopwatch();
  			timer.Start();

            StringBuilder report = new System.Text.StringBuilder();

            //Resolve references, which validates the seeds are configured right and also sets their market value
            int length = DefDatabase<ThingDef>.defsList.Count;
            for (int i = 0; i < length; ++i)
            {
                ThingDef thingDef = DefDatabase<ThingDef>.defsList[i];

                //Add any missing data to seeds we already have defs for
                foreach (DefModExtension defExtension in thingDef.modExtensions ?? Enumerable.Empty<DefModExtension>())
                {
                    Seed seedComp = defExtension as Seed;
                    if (seedComp != null)
                    {
                        ResolveReferences(thingDef, seedComp);
                        if (seedComp.extractable != Seed.Extractable.False) butcheryToResolve.Add((thingDef, seedComp));    
                        break;
                    }
                }

                //Cache plants to check after resolving is complete
                if (thingDef.plant?.Sowable ?? false) plantsToCheck.Add(thingDef);
            }

            length = plantsToCheck.Count;
            bool missing = false;
            for (int i = 0; i < length; ++i)
            {
                ThingDef thingDef = plantsToCheck[i];
                if
                (
                    thingDef.blueprintDef == null && 
                    thingDef.plant.harvestedThingDef != null && 
                    !thingDef.HasModExtension<Seedless>()
                )
                {
                    AddMissingSeed(report, thingDef);
                    missing = true;
                }
            }
            
            if (missing)
            {
                ResourceCounter.ResetDefs();
                Log.Warning("[Seeds Please: Lite] Some Seeds were autogenerated.\nDon't rely on autogenerated seeds, share the generated XML for proper support.\n\n" + report);
            }
            ResourceBank.ThingCategoryDefOf.SeedsCategory.ResolveReferences();

            butcheryToResolve.ForEach(x => AddButchery(x.Item1, x.Item2));

            timer.Stop();
			TimeSpan timeTaken = timer.Elapsed;

			//Give report
			if (Prefs.DevMode) Log.Message("[Seeds Please: Lite] Seeds processed in " + timeTaken.ToString(@"ss\.fffff") + " seconds");

            //Clear static fields to free memory
            plantsToCheck = null; butcheryToResolve = null;
        }

        static float AssignMarketValueFromHarvest(ThingDef thingDef)
        {
            ThingDef harvestedThingDef = thingDef.plant.harvestedThingDef;
            
            //Flat rate value if there's no harvested thing
            if (harvestedThingDef == null) return 0.5f;
			
            //Adjust value based on plant's growth cycle and yield
            float factor = thingDef.plant.harvestYield / thingDef.plant.growDays + thingDef.plant.growDays / thingDef.plant.harvestYield;

            //Adjust value based on harvested thing's value
            float value = harvestedThingDef.BaseMarketValue * factor * 2.5f;
			
            //Adjust value if this plant needs space
            if (thingDef.plant.blockAdjacentSow) value *= 1.5f;
			
            //Adjust value if it's a wild plant
            int cnt = thingDef.plant.wildBiomes?.Count() ?? 0;
            if (cnt > 1) value *= Mathf.Pow(0.95f, cnt);
			
            //Value adjusted based on type
            if (harvestedThingDef == ThingDefOf.WoodLog) value *= 0.2f;
            else if (harvestedThingDef.IsAddictiveDrug) value *= 1.3f;
            else if (harvestedThingDef.IsDrug) value *= 1.2f;
            else if (harvestedThingDef.IsMedicine) value *= 1.1f;
			
            //Adjust value based on skill need
            value *= Mathf.LerpUnclamped(0.8f, 1.6f, thingDef.plant.sowMinSkill / 20f);

            //Factor in user preference
            value *= SeedsPleaseLite.ModSettings_SeedsPleaseLite.marketValueModifier;
			
            return Mathf.Round(Math.Min(value, 25f) * 100f) / 100f;
		}

        static void AddMissingSeed(StringBuilder report, ThingDef thingDef)
        {
            string name = thingDef?.defName;
            if (name.NullOrEmpty())
            {
                Log.Warning("[Seeds Please: Lite] Tried to generate a seed for an invalid definition. Skipping...");
                return;
            }
            foreach (string prefix in ResourceBank.knownPrefixes)
            {
                name = name.Replace(prefix, "");
			}
            name = name.CapitalizeFirst();
			
            report.Append("\n<!-- SeedsPlease :: " + thingDef.defName + "(" + ((thingDef.modContentPack?.IsCoreMod ?? true) ? "Patched" : thingDef.modContentPack.PackageId) + ")");
			
            ThingDef seed = DefDatabase<ThingDef>.GetNamed("Seed_" + name, false);

            if (seed == null)
            {
                ThingDef template = Seed_Psychoid;
                seed = new ThingDef()
                {
                    defName = "Seed_" + name,
                    label = name.ToLower() + " seeds",
                    stackLimit = template.stackLimit,
                    tradeTags = template.tradeTags,
                    thingCategories = template.thingCategories,
                    soundDrop = template.soundDrop,
                    soundInteract = template.soundInteract,
                    statBases = template.statBases,
                    graphicData = template.graphicData,
                    description = template.description,
                    thingClass = template.thingClass,
                    modExtensions = new List<DefModExtension>(),
                    pathCost = template.pathCost,
                    rotatable = template.rotatable,
                    drawGUIOverlay = template.drawGUIOverlay,
                    alwaysHaulable = template.alwaysHaulable,
                    altitudeLayer = template.altitudeLayer,
                    selectable = template.selectable,
                    useHitPoints = template.useHitPoints,
                    resourceReadoutPriority = template.resourceReadoutPriority,
                    category = template.category,
                    uiIcon = template.uiIcon,
                    uiIconColor = template.uiIconColor,
                    ingestible = template.ingestible,
                    descriptionHyperlinks = new List<DefHyperlink>() { thingDef },
                    BaseMarketValue = AssignMarketValueFromHarvest(thingDef)
				};

                //Add comp
                Seed seedComp = new Seed()
                {
                    plant = thingDef,
                    harvestOverride = thingDef.plant.harvestedThingDef,
                    sources = new List<ThingDef>() {thingDef}
                };
                seed.modExtensions.Add(seedComp);
				
                //Add ref to the category
                SeedsCategory.childThingDefs.Add(seed);

                //Short hash
                ShortHashGiver.GiveShortHash(seed, typeof(ThingDef));
				
                //Add the seed to the database and let it resolve its links with other defs
                DefDatabase<ThingDef>.Add(seed);
                butcheryToResolve.Add((seed, seedComp));
                ResolveReferences(seed, seedComp, true);
                report.Append("Autogenerated as ");
			}
            else
            {
                seed.GetModExtension<Seed>().sources.Add(thingDef);
                report.Append("Inserted to ");
			}

            report.Append(seed.defName + "-->\n");
			
            var seedXml =
            new XElement("ThingDef", new XAttribute("ParentName", "SeedBase"),
				new XElement("defName", seed.defName),
				new XElement("label", seed.label),
                new XElement("descriptionHyperlinks", 
                  new XElement("ThingDef",thingDef)),
				new XElement("modExtensions",
                    new XElement("li", new XAttribute("Class", "SeedsPleaseLite.Seed"),
                        new XElement("sources",
                            new XElement("li", thingDef.defName)))));
				
				report.AppendLine(seedXml.ToString());
		}

        static void AddButchery(ThingDef seed, Seed seedComp)
        {
            ThingCategoryDef se = SeedExtractable; //alias the category into shorthand
            //Iterate through the sources within each seed
            foreach (ThingDef source in seedComp?.sources ?? Enumerable.Empty<ThingDef>())
            {
                if (source.plant.harvestedThingDef == null) continue;

                ThingDef thisProduce = DefDatabase<ThingDef>.GetNamed(source.plant.harvestedThingDef.defName);
                if (thisProduce == null) continue;

                //We don't add butchery things to non-produce harvests like wood.
                if (thisProduce.IsIngestible == false && seedComp.extractable != Seed.Extractable.True) continue;

                //Add butchery product values. Butchering this produce renders this seed
                if (thisProduce.butcherProducts == null) thisProduce.butcherProducts = new List<ThingDefCountClass>();
                ThingDefCountClass seedToAdd = new ThingDefCountClass(seed, (int)Math.Round(seedComp.extractionValue * SeedsPleaseLite.ModSettings_SeedsPleaseLite.extractionModifier));

                //Make the produce drop this seed when processed
                if (thisProduce.butcherProducts.Count == 0) thisProduce.butcherProducts.Add(seedToAdd);

                //Give warning, or ignore if the seed is the same (which would happen if an alt plant exists like for example wild healroot)
                else if (thisProduce.butcherProducts[0].thingDef != seed) 
                {
                    var priorityCurrent = thisProduce.butcherProducts[0].thingDef.GetModExtension<Seed>()?.priority;
                    var priorityNew = seedComp.priority;

                    //Compare priorioty to determine winner
                    if (priorityNew > priorityCurrent) thisProduce.butcherProducts[0] = seedToAdd;
                    else if (priorityNew == priorityCurrent) Log.Warning("[Seeds Please: Lite] The seed " + seed.defName + " wants to be extracted from "
                    + thisProduce.defName + " but this produce already contains seeds for " + thisProduce.butcherProducts[0].thingDef.defName + 
                    ". This will need to be resolved manually, please report.");
                }

                //Add category
                if (!thisProduce.thingCategories.Contains(se))
                {
                    thisProduce.thingCategories.Add(se);
                    if (!se.childThingDefs.Contains(thisProduce))
                    {
                        se.childThingDefs.Add(thisProduce);
                    }
                }
            }

            se.ResolveReferences();
            ExtractSeeds.ResolveReferences();
        }

        static void ResolveReferences (ThingDef thingDef, Seed seedComp, bool resolveBase = false)
        {
            if (resolveBase) thingDef.ResolveReferences();

            //Check the seed's sources
            foreach (ThingDef sourcePlant in seedComp?.sources ?? Enumerable.Empty<ThingDef>())
            {
                //Validate source is actually a plant
                if (sourcePlant.plant == null)
                {
                    Log.Warning("SeedsPlease :: " + sourcePlant.defName + " is not a plant.");
                    continue;
				}
				
                //Give this plant a blueprint that equals this seed
                sourcePlant.blueprintDef = thingDef;

                //Apply the harvestFactor
                sourcePlant.plant.harvestYield *= seedComp.harvestFactor;
				
                //Set plant reference
                if (seedComp.plant == null && sourcePlant.plant.Sowable) seedComp.plant = sourcePlant;
			}
			
            if (seedComp.plant == null)
            {
                Log.Warning("[Seeds Please: Lite]" + thingDef.defName + " has no sowable plant.");
                return;
			}
			
            //Set plant's blueprint?
            if (seedComp.plant.blueprintDef == null) seedComp.plant.blueprintDef = thingDef;
			
            //If using an override, set it on the plant
            if (seedComp.harvestOverride != null) seedComp.plant.plant.harvestedThingDef = seedComp.harvestOverride;
			else seedComp.harvestOverride = seedComp.plant.plant.harvestedThingDef;
			
            //Set the market value
            if (thingDef.BaseMarketValue <= 0f && seedComp.harvestOverride != null) thingDef.BaseMarketValue = AssignMarketValueFromHarvest(seedComp.plant);
		}
    }
}