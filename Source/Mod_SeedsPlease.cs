namespace SeedsPleaseLite;

using HarmonyLib;
using Verse;
using UnityEngine;
using RimWorld;
using System.Collections.Generic;

public class Mod_SeedsPlease : Mod
{
    public Mod_SeedsPlease(ModContentPack content) : base(content)
    {
        base.GetSettings<ModSettings_SeedsPleaseLiteRedux>();
        new Harmony(this.Content.PackageIdPlayerFacing).PatchAll();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        //========Setup ModSettings_SeedsPleaseLiteRedux.Tabs=========
        GUI.BeginGroup(inRect);

        Rect rect = new Rect(0f, 32f, inRect.width, inRect.height - 32f);
        Widgets.DrawMenuSection(rect);

        Listing_Standard options = new Listing_Standard();
        options.Begin(inRect.ContractedBy(15f));
        options.Label("SPL.RequiresRestart".Translate());
        options.GapLine();
        options.Label("SPL.Settings.MarketValueModifier".Translate("100%", "20%", "500%") + ModSettings_SeedsPleaseLiteRedux.marketValueModifier.ToStringPercent(), -1f, "SPL.Settings.MarketValueModifier.Desc".Translate());
        ModSettings_SeedsPleaseLiteRedux.marketValueModifier = options.Slider(ModSettings_SeedsPleaseLiteRedux.marketValueModifier, 0.2f, 5f);

        options.Label("SPL.Settings.SeedExtractionModifier".Translate("100%", "20%", "500%") + ModSettings_SeedsPleaseLiteRedux.extractionModifier.ToStringPercent(), -1f, "SPL.Settings.SeedExtractionModifier.Desc".Translate("4"));
        ModSettings_SeedsPleaseLiteRedux.extractionModifier = options.Slider(ModSettings_SeedsPleaseLiteRedux.extractionModifier, 0.2f, 5f);

        options.Label("SPL.Settings.SeedFactorModifier".Translate("100%", "20%", "500%") + ModSettings_SeedsPleaseLiteRedux.seedFactorModifier.ToStringPercent(), -1f, "SPL.Settings.SeedFactorModifier.Desc".Translate("1"));
        ModSettings_SeedsPleaseLiteRedux.seedFactorModifier = options.Slider(ModSettings_SeedsPleaseLiteRedux.seedFactorModifier, 0.2f, 5f);

        options.CheckboxLabeled("SPL.Settings.NoUselessSeeds".Translate(), ref ModSettings_SeedsPleaseLiteRedux.noUselessSeeds, "SPL.Settings.NoUselessSeeds.Desc".Translate());
        options.CheckboxLabeled("SPL.Settings.ClearSnow".Translate(), ref ModSettings_SeedsPleaseLiteRedux.clearSnow, "SPL.Settings.ClearSnow.Desc".Translate());

        options.CheckboxLabeled("SPL.Settings.EdibleSeeds".Translate(), ref ModSettings_SeedsPleaseLiteRedux.edibleSeeds, "SPL.Settings.EdibleSeeds.Desc".Translate());

        //============

        //Record positioning before closing out the lister...
        Rect seedlessFilterRect = inRect.ContractedBy(15f);
        seedlessFilterRect.y = options.curY + 95f;
        seedlessFilterRect.height = inRect.height - options.curY - 105f; //Use remaining space

        options.ColumnWidth = options.listingRect.width - 30f;
        options.End();

        //========Setup ModSettings_SeedsPleaseLiteRedux.Tabs=========
        var tabs = new List<TabRecord>
        {
            new TabRecord("Seedless", delegate { ModSettings_SeedsPleaseLiteRedux.selectedTab = ModSettings_SeedsPleaseLiteRedux.Tab.seedless; }, ModSettings_SeedsPleaseLiteRedux.selectedTab == ModSettings_SeedsPleaseLiteRedux.Tab.seedless),
            new TabRecord("Labels", delegate { ModSettings_SeedsPleaseLiteRedux.selectedTab = ModSettings_SeedsPleaseLiteRedux.Tab.labels; }, ModSettings_SeedsPleaseLiteRedux.selectedTab == ModSettings_SeedsPleaseLiteRedux.Tab.labels)
        };

        Widgets.DrawMenuSection(seedlessFilterRect); //Used to make the background light grey with white border
        TabDrawer.DrawTabs(new Rect(seedlessFilterRect.x, seedlessFilterRect.y, seedlessFilterRect.width, Text.LineHeight), tabs);

        //========Between ModSettings_SeedsPleaseLiteRedux.Tabs and scroll body=========
        options.Begin(new Rect(seedlessFilterRect.x + 10, seedlessFilterRect.y + 10, seedlessFilterRect.width - 10f, seedlessFilterRect.height - 10f));
        if (ModSettings_SeedsPleaseLiteRedux.selectedTab == ModSettings_SeedsPleaseLiteRedux.Tab.seedless)
        {
            options.Label("SPL.Settings.SeedlessDesc".Translate());
        }
        else
        {
            options.Label("SPL.Settings.LabelsDesc".Translate());
        }
        options.End();
        //========Scroll area=========
        seedlessFilterRect.y += 30f;
        seedlessFilterRect.yMax -= 30f;
        Rect weaponsFilterInnerRect = new Rect(0f, 0f, seedlessFilterRect.width - 30f, (OptionsDrawUtility.lineNumber + 2) * 22f);
        Widgets.BeginScrollView(seedlessFilterRect, ref ModSettings_SeedsPleaseLiteRedux.scrollPos, weaponsFilterInnerRect, true);
        options.Begin(weaponsFilterInnerRect);
        options.DrawList(inRect);
        options.End();
        Widgets.EndScrollView();

        GUI.EndGroup();
    }

    public override string SettingsCategory()
    {
        return "Seeds Please: Lite Redux";
    }

    public override void WriteSettings()
    {
        try
        {
            SeedsPleaseUtility.ProcessInversions();
        }
        catch (System.Exception ex)
        {
            Log.Error("[Seeds Please: Lite Redux] Failed to process user settings. Skipping...\n" + ex);
        }

        base.WriteSettings();
    }
}
