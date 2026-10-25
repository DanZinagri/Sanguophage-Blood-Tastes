using HarmonyLib;
using UnityEngine;
using Verse;

namespace BloodTastes
{
    public class BloodTastesSettings : ModSettings
    {
        public bool xenotypeTastes = true;
        public bool copyDrugs = true;
        public bool showMote = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref xenotypeTastes, "xenotypeTastes", true);
            Scribe_Values.Look(ref copyDrugs, "copyDrugs", true);
            Scribe_Values.Look(ref showMote, "showMote", true);
        }
    }

    public class BloodTastesMod : Mod
    {
        public static BloodTastesSettings settings;

        public BloodTastesMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<BloodTastesSettings>();
            Harmony harmony = new Harmony("DanZinagri.SanguophageBloodTastes");
            harmony.PatchAll();
            VRECorpseCompat.TryPatch(harmony);
        }

        public override string SettingsCategory()
        {
            return "BloodTastes_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("BloodTastes_XenotypeTastes".Translate(), ref settings.xenotypeTastes, "BloodTastes_XenotypeTastesTip".Translate());
            listing.CheckboxLabeled("BloodTastes_CopyDrugs".Translate(), ref settings.copyDrugs, "BloodTastes_CopyDrugsTip".Translate());
            listing.CheckboxLabeled("BloodTastes_ShowMote".Translate(), ref settings.showMote, "BloodTastes_ShowMoteTip".Translate());
            listing.End();
        }
    }
}
