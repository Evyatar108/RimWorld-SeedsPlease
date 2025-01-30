namespace SeedsPleaseLite;

using System.Collections.Generic;
using System;
using RimWorld;
using Verse;
using UnityEngine;
using System.Xml.Linq;
using System.Text;
using System.Linq;
using static SeedsPleaseLite.ResourceBank.Defs;
using Settings = SeedsPleaseLite.ModSettings_SeedsPleaseLiteRedux;
using static SeedsPleaseLite.ResourceBank;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

[StaticConstructorOnStartup]
public static class SeedsPleaseUtility
{
    public static ThingDef[] allPlants;
    static ThingDef template;
    static SeedsPleaseUtility()
    {
        var timer = new System.Diagnostics.Stopwatch();
        timer.Start();

        Setup();

        //Give becnhmark report
        timer.Stop();
        TimeSpan timeTaken = timer.Elapsed;
        if (Prefs.DevMode)
        {
            Log.Message("[Seeds Please: Lite Redux] Seeds processed in " + timeTaken.ToString(@"ss\.fffff") + " seconds");
        }
    }

    public static void Setup(bool rerun = false)
    {
        if (Settings.seedlessInversions == null)
        {
            Settings.seedlessInversions = new HashSet<string>();
        }

        if (Settings.seedlessCache == null)
        {
            Settings.seedlessCache = new HashSet<ushort>();
        }

        StringBuilder report = new System.Text.StringBuilder();

        allPlants = GetSeedsAndPlants(out List<ThingDef> seedCache, out List<(ThingDef, Seed)> butcheryToResolve);

        if (ProcessPlants(report, seedCache, butcheryToResolve))
        {
            ResourceCounter.ResetDefs();
            if (!rerun)
            {
                Log.Warning("[Seeds Please: Lite Redux] Some Seeds were autogenerated.\nDon't rely on autogenerated seeds, share the generated XML for proper support.\n\n" + report);
            }
        }

        allPlants = allPlants.OrderBy(x => Settings.seedlessCache.Contains(x.shortHash)).ToArray();

        SeedsCategory.ResolveReferences();

        AddButchery(butcheryToResolve);

        SeedExtractable.ResolveReferences();
        ExtractSeeds.ResolveReferences();

        var storageBuildingDefs = DefDatabase<ThingDef>.AllDefs.Where(x =>
            !string.IsNullOrWhiteSpace(x.building?.storageGroupTag)
            || x.building?.fixedStorageSettings != null
            || x.building?.defaultStorageSettings != null);
        foreach (ThingDef storageBuildingDef in storageBuildingDefs)
        {
            storageBuildingDef.ResolveReferences();
        }
    }

    static ThingDef[] GetSeedsAndPlants(out List<ThingDef> seedCache, out List<(ThingDef, Seed)> butcheryToResolve)
    {
        butcheryToResolve = new List<(ThingDef, Seed)>();
        var allPlantsWorkingList = new List<ThingDef>();
        seedCache = new List<ThingDef>();

        //Resolve references, which validates the seeds are configured right and also sets their market value
        var list = DefDatabase<ThingDef>.AllDefsListForReading;
        for (int i = list.Count; i-- > 0;)
        {
            ThingDef thingDef = list[i];
            if (thingDef.defName.Contains("Seed_"))
            {
                seedCache.Add(thingDef);
            }

            //Add any missing data to seeds we already have defs for
            var modExtensions = thingDef.modExtensions;
            for (int j = modExtensions?.Count ?? 0; j-- > 0;)
            {
                if (modExtensions[j] is Seed seedEx)
                {
                    //Assign the first seed we come across as our template seed for later use
                    if (template == null)
                    {
                        template = thingDef;
                    }

                    //Add dynamic data
                    ProcessSeed(thingDef, seedEx);

                    //Queue for butchery processing later
                    if (seedEx.extractable != Seed.Extractable.False)
                    {
                        butcheryToResolve.Add((thingDef, seedEx));
                    }

                    break;
                }
            }

            //Cache plants to check after resolving is complete
            if (thingDef.plant?.Sowable ?? false)
            {
                allPlantsWorkingList.Add(thingDef);
            }
        }

        return allPlantsWorkingList.ToArray();
    }

    public static bool ProcessPlants(StringBuilder report, List<ThingDef> seedCache, List<(ThingDef, Seed)> butcheryToResolve)
    {
        bool neededToGenerate = false;
        for (int i = allPlants.Length; i-- > 0;)
        {
            ThingDef thingDef = allPlants[i];

            //Process seedless and custom inversions
            bool seedLess = thingDef.HasModExtension<Seedless>() || thingDef.plant.harvestedThingDef == null;
            if (Settings.seedlessInversions.Contains(thingDef.defName))
            {
                seedLess = !seedLess; //Inversion
            }

            if (seedLess)
            {
                Settings.seedlessCache.Add(thingDef.shortHash);
            }

            if (thingDef.blueprintDef == null)
            {
                if (!seedLess)
                {
                    AddMissingSeed(thingDef, report, seedCache, butcheryToResolve);
                    neededToGenerate = true;
                }
            }
            else
            {
                if (seedLess)
                {
                    //If we want to remove seeds without needing to restart the game, it would start here. Future project. For now, a restart it requires to process everything correctly.
                }
            }
        }

        return neededToGenerate;
    }

    static void AddMissingSeed(ThingDef thingDef, StringBuilder report, List<ThingDef> seedCache, List<(ThingDef, Seed)> butcheryToResolve)
    {
        if (!ProcessLabel(out string label, thingDef))
        {
            return;
        }

        report.Append("\n<!-- [Seeds Please: Lite Redux] " + thingDef.defName + "(" + ((thingDef.modContentPack?.IsCoreMod ?? true) ? "Patched" : thingDef.modContentPack.PackageId) + ")");

        //Check if a seed exists for this plant but that seed was simply missing a source reference to it
        var defName = "Seed_" + thingDef.defName;
        ThingDef seed = seedCache.Find(x => x.defName.Contains(defName));

        //Seed found, edit it...
        if (seed != null)
        {
            var tmp = seed.GetModExtension<Seed>();
            if (tmp == null)
            {
                Log.Warning("[Seeds Please: Lite Redux] " + seed.defName + " is missing the seed extension. Skipping...");
            }
            else if (tmp.sources == null)
            {
                Log.Warning("[Seeds Please: Lite Redux] " + seed.defName + " is missing sources. Skipping...");
            }
            else
            {
                tmp.sources.Add(thingDef);
                report.Append("Inserted to ");
            }
        }
        //If no seed was found we need to generate one...
        else
        {
            if (template == null)
            {
                Log.Warning("[Seeds Please: Lite Redux] Could not find template seed...");
                return;
            }

            /*
            seed = DeepClone(template);
            seed.defName = defName;
            seed.label = label.ToLower() + " seeds";
            seed.modExtensions = new List<DefModExtension>();
            */

            seed = new ThingDef()
            {
                defName = defName,
                label = label.ToLower() + " seeds",
                stackLimit = template.stackLimit,
                tradeTags = template.tradeTags,
                thingCategories = template.thingCategories,
                soundDrop = template.soundDrop,
                soundInteract = template.soundInteract,
                statBases = template.statBases,
                graphicData = template.graphicData,
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
                designateHaulable = template.designateHaulable,
                drawerType = template.drawerType,
                comps = template.comps,
                allowedArchonexusCount = template.allowedArchonexusCount,
                virtualDefParent = template.virtualDefParent,
            };

            //Add extension
            Seed seedEx = new Seed()
            {
                plant = thingDef,
                harvestOverride = thingDef.plant.harvestedThingDef,
                sources = new List<ThingDef>() { thingDef }
            };
            seed.modExtensions.Add(seedEx);

            //Add ref to the category
            SeedsCategory.childThingDefs.Add(seed);

            //Short hash
            ShortHashGiver.GiveShortHash(seed, typeof(ThingDef), ShortHashGiver.takenHashesPerDeftype[typeof(ThingDef)]);

            //Add the seed to the database and let it resolve its links with other defs
            DefDatabase<ThingDef>.Add(seed);
            butcheryToResolve.Add((seed, seedEx));
            ProcessSeed(seed, seedEx, true);
            report.Append("Autogenerated as ");
        }

        report.Append(seed.defName + "-->\n");
        report.AppendLine(ProcessAutoXML(seed, thingDef).ToString());

        //Embedded methods
        bool ProcessLabel(out string label, ThingDef thingDef)
        {
            label = thingDef?.label;
            if (label.NullOrEmpty())
            {
                Log.Warning("[Seeds Please: Lite Redux] Tried to generate a seed for an invalid definition. Skipping...");
                return false;
            }

            label = label.CapitalizeFirst();
            return !label.NullOrEmpty();
        }
        XElement ProcessAutoXML(ThingDef seed, ThingDef thingDef)
        {
            return new XElement("ThingDef", new XAttribute("ParentName", "SeedBase"),
            new XElement("defName", seed.defName),
            new XElement("label", seed.label),
            new XElement("descriptionHyperlinks",
            new XElement("ThingDef", thingDef)),
            new XElement("modExtensions",
                new XElement("li", new XAttribute("Class", "SeedsPleaseLite.Seed"),
                    new XElement("sources",
                        new XElement("li", thingDef.defName)))));
        }
    }

    static T DeepClone<T>(T obj)
    {
        if (!typeof(T).IsSerializable)
        {
            throw new ArgumentException("The type must be serializable.", nameof(obj));
        }

        if (ReferenceEquals(obj, null))
        {
            return default;
        }

        using (var ms = new MemoryStream())
        {
            IFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, obj);
            ms.Seek(0, SeekOrigin.Begin);
            return (T)formatter.Deserialize(ms);
        }
    }

    static void ProcessSeed(ThingDef thingDef, Seed seedEx, bool resolveBase = false)
    {
        if (resolveBase)
        {
            thingDef.ResolveReferences();
        }

        //Check the seed's sources
        for (int i = seedEx?.sources?.Count ?? 0; i-- > 0;)
        {
            ThingDef sourcePlant = seedEx.sources[i];

            if (Settings.seedlessInversions.Contains(sourcePlant.defName))
            {
                continue;
            }

            //Validate source is actually a plant
            if (sourcePlant.plant == null)
            {
                Log.Warning("[Seeds Please: Lite Redux] " + sourcePlant.defName + " is not a plant.");
                continue;
            }

            //Give this plant a blueprint that equals this seed
            sourcePlant.blueprintDef = thingDef;

            //Apply the harvestFactor
            sourcePlant.plant.harvestYield *= seedEx.harvestFactor;

            //Set plant reference
            if (seedEx.plant == null && sourcePlant.plant.Sowable)
            {
                seedEx.plant = sourcePlant;
            }

            //Edit edible if needed
            if (!Settings.edibleSeeds && thingDef.ingestible != null)
            {
                thingDef.ingestible.foodType = FoodTypeFlags.None;
            }

            UpdateDescriptions(thingDef, sourcePlant);
        }

        if (seedEx.plant == null)
        {
            thingDef.BaseMarketValue = 0f; //Remove value, to prevent it from being used anywhere (loot, traders)
            return; //Probably was set as an inversion, skip
        }

        //Set plant's blueprint?
        if (seedEx.plant.blueprintDef == null)
        {
            seedEx.plant.blueprintDef = thingDef;
        }

        //If using an override, set it on the plant
        if (seedEx.harvestOverride != null)
        {
            seedEx.plant.plant.harvestedThingDef = seedEx.harvestOverride;
        }
        else
        {
            seedEx.harvestOverride = seedEx.plant.plant.harvestedThingDef;
        }

        //Set the market value
        if (thingDef.BaseMarketValue <= 0f)
        {
            if (seedEx.harvestOverride != null)
            {
                thingDef.BaseMarketValue = AddMarketValue(seedEx.plant);
            }
            else
            {
                Log.Warning("[Seeds Please: Lite Redux] " + thingDef.defName + " cannot be given an automatic market value. Its value needs to be manually determined and written into the XML.");
            }
        }

        float AddMarketValue(ThingDef thingDef)
        {
            ThingDef harvestedThingDef = thingDef.plant.harvestedThingDef;

            //Flat rate value if there's no harvested thing
            if (harvestedThingDef == null)
            {
                return 0.5f;
            }

            //Adjust value based on plant's growth cycle and yield
            float factor = (thingDef.plant.harvestYield / thingDef.plant.growDays) + (thingDef.plant.growDays / thingDef.plant.harvestYield);

            //Adjust value based on harvested thing's value
            float value = harvestedThingDef.BaseMarketValue * factor * 2.5f;

            //Adjust value if this plant needs space
            if (thingDef.plant.blockAdjacentSow)
            {
                value *= 1.5f;
            }

            //Adjust value if it's a wild plant
            int cnt = thingDef.plant.wildBiomes?.Count ?? 0;
            if (cnt > 1)
            {
                value *= Mathf.Pow(0.95f, cnt);
            }

            //Value adjusted based on type
            if (harvestedThingDef == ThingDefOf.WoodLog)
            {
                value *= 0.2f;
            }
            else if (harvestedThingDef.IsAddictiveDrug)
            {
                value *= 1.3f;
            }
            else if (harvestedThingDef.IsDrug)
            {
                value *= 1.2f;
            }
            else if (harvestedThingDef.IsMedicine)
            {
                value *= 1.1f;
            }

            //Adjust value based on skill need
            value *= Mathf.LerpUnclamped(0.8f, 1.6f, thingDef.plant.sowMinSkill / 20f);

            //Factor in user preference
            value *= Settings.marketValueModifier;

            return Mathf.Round(Math.Min(value, 25f) * 100f) / 100f;
        }
    }

    static void AddButchery(List<(ThingDef, Seed)> butcheryToResolve)
    {
        for (int i = butcheryToResolve.Count; i-- > 0;)
        {
            var def = butcheryToResolve[i];
            ThingDef seed = def.Item1;
            Seed seedEx = def.Item2;

            ThingCategoryDef se = SeedExtractable; //alias the category into shorthand
            //Iterate through the sources within each seed
            if (seedEx?.sources == null)
            {
                continue;
            }

            var sources = seedEx.sources;
            for (int j = sources.Count; j-- > 0;)
            {
                ThingDef thisProduce = sources[j].plant.harvestedThingDef;
                float harvestYield = sources[j].plant.harvestYield;
                if (thisProduce == null)
                {
                    continue;
                }

                //We don't add butchery things to non-produce harvests like wood.
                if (thisProduce.IsIngestible == false && seedEx.extractable != Seed.Extractable.True)
                {
                    continue;
                }

                //Add butchery product values. Butchering this produce renders this seed
                if (thisProduce.butcherProducts == null)
                {
                    thisProduce.butcherProducts = new List<ThingDefCountClass>();
                }

                float seedMultiplierBasedOnHarvestYield = Math.Max(1f, 20 / harvestYield);

                ThingDefCountClass seedToAdd = new ThingDefCountClass(seed, (int)Math.Round(seedEx.extractionValue * Settings.extractionModifier * seedMultiplierBasedOnHarvestYield));

                //Make the produce drop this seed when processed
                if (thisProduce.butcherProducts.Count == 0)
                {
                    thisProduce.butcherProducts.Add(seedToAdd);
                }
                else
                //Give warning, or ignore if the seed is the same (which would happen if an alt plant exists like for example wild healroot)
                {
                    var list = thisProduce.butcherProducts[0];
                    if (list.thingDef != seed)
                    {
                        int? priorityCurrent = list.thingDef.GetModExtension<Seed>()?.priority;
                        int priorityNew = seedEx.priority;

                        //Compare priorioty to determine winner
                        if (priorityNew > priorityCurrent)
                        {
                            thisProduce.butcherProducts[0] = seedToAdd;
                        }
                        else if (priorityNew == priorityCurrent)
                        {
                            Log.Warning($"[Seeds Please: Lite Redux] The seed {seed.defName} wants to be extracted from {thisProduce.defName} but this produce already contains seeds for {thisProduce.butcherProducts[0].thingDef.defName}. This will need to be resolved manually, please report.");
                        }
                    }
                }

                //Don't allow null lists
                if (thisProduce.thingCategories == null)
                {
                    thisProduce.thingCategories = new List<ThingCategoryDef>();
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
        }
    }

    private static void UpdateDescriptions(ThingDef seedDef, ThingDef plantDef)
    {
        var harvestedThing = plantDef?.plant?.harvestedThingDef;
        if (seedDef == null || plantDef == null || harvestedThing == null)
        {
            return;
        }

        seedDef.description = plantDef.description;
        if (seedDef.descriptionHyperlinks == null)
        {
            seedDef.descriptionHyperlinks = new List<DefHyperlink>();
        }

        if (plantDef.descriptionHyperlinks == null)
        {
            plantDef.descriptionHyperlinks = new List<DefHyperlink>();
        }

        if (harvestedThing.descriptionHyperlinks == null)
        {
            harvestedThing.descriptionHyperlinks = new List<DefHyperlink>();
        }

        seedDef.descriptionHyperlinks.Add(plantDef);
        seedDef.descriptionHyperlinks.Add(harvestedThing);
        seedDef.descriptionHyperlinks.Add(Defs.ExtractSeeds);

        plantDef.descriptionHyperlinks.Add(seedDef);
        plantDef.descriptionHyperlinks.Add(harvestedThing);

        harvestedThing.descriptionHyperlinks.Add(plantDef);
        harvestedThing.descriptionHyperlinks.Add(seedDef);
        harvestedThing.descriptionHyperlinks.Add(Defs.ExtractSeeds);

        seedDef.descriptionHyperlinks = seedDef.descriptionHyperlinks.DistinctBy(x => x.def?.defName)?.ToList();
        plantDef.descriptionHyperlinks = plantDef.descriptionHyperlinks.DistinctBy(x => x.def?.defName).ToList();
        harvestedThing.descriptionHyperlinks = harvestedThing.descriptionHyperlinks.DistinctBy(x => x.def.defName)?.ToList();
    }

    public static IEnumerable<T> DistinctBy<T, TKey>(this IEnumerable<T> items, Func<T, TKey> property)
    {
        return items.GroupBy(property).Select(x => x.First());
    }

    public static void ProcessInversions()
    {
        bool inversionsChanged = false;
        for (int i = SeedsPleaseUtility.allPlants.Length; i-- > 0;)
        {
            ThingDef thingDef = SeedsPleaseUtility.allPlants[i];

            bool seedless = thingDef.HasModExtension<Seedless>() || thingDef.plant.harvestedThingDef == null;

            if (seedless)
            {
                //Normally seedless but was apparently removed, then an inversion was just made. Add to settings
                if (!Settings.seedlessCache.Contains(thingDef.shortHash))
                {
                    if (Settings.seedlessInversions.Add(thingDef.defName))
                    {
                        inversionsChanged = true;
                    }
                }
                //If the cache is normal then remove any inversion if one exists
                else
                {
                    if (Settings.seedlessInversions.Remove(thingDef.defName))
                    {
                        inversionsChanged = true;
                    }
                }
            }
            else
            {
                //Normally needs a seed, but not anymore. Add an inversion
                if (Settings.seedlessCache.Contains(thingDef.shortHash))
                {
                    if (Settings.seedlessInversions.Add(thingDef.defName))
                    {
                        inversionsChanged = true;
                    }
                }
                else
                {
                    if (Settings.seedlessInversions.Remove(thingDef.defName))
                    {
                        inversionsChanged = true;
                    }
                }
            }
        }

        if (inversionsChanged)
        {
            allPlants = allPlants.OrderBy(x => Settings.seedlessCache.Contains(x.shortHash)).ToArray();
            Find.WindowStack.Add(new Dialog_MessageBox(text: "SPL.Settings.ReloadNeeded".Translate(), title: "SPL.Settings.ReloadNeededHeader".Translate()));
        }
    }
}