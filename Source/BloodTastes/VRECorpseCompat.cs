using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace BloodTastes
{
    // VRE - Sanguophage feeds on corpses without calling SanguophageUtility.DoBite, in two places:
    //   - the Corpse Feed ability, through its own static CompAbilityEffect_CorpsefeederBite.DoBite(biter, corpse, ...)
    //   - the corpsefeeder gene's auto-drink job, JobDriver_DrinkCorpseHemogen, whose last toil does the feeding in
    //     a compiler-generated lambda and then destroys the corpse.
    // VRE is optional, so everything here is found by name and nothing references its assembly.
    public static class VRECorpseCompat
    {
        private const string PackageId = "vanillaracesexpanded.sanguophage";

        public static void TryPatch(Harmony harmony)
        {
            if (!ModsConfig.IsActive(PackageId))
            {
                return;
            }

            MethodInfo abilityBite = AccessTools.Method("VanillaRacesExpandedSanguophage.CompAbilityEffect_CorpsefeederBite:DoBite");
            if (abilityBite != null)
            {
                harmony.Patch(abilityBite, postfix: new HarmonyMethod(typeof(VRECorpseCompat), nameof(CorpseBitePostfix)));
            }
            else
            {
                Log.Warning("[Blood Tastes] VRE - Sanguophage is active but CompAbilityEffect_CorpsefeederBite.DoBite was not found; Corpse Feed gives no blood tastes.");
            }

            // The lambda's name (<MakeNewToils>b__7_0 today) shifts whenever VRE recompiles, so pick it by what it
            // does: the one that hands out the hemogen.
            System.Type driver = AccessTools.TypeByName("VanillaRacesExpandedSanguophage.JobDriver_DrinkCorpseHemogen");
            MethodInfo drinkToil = driver == null ? null : AccessTools.GetDeclaredMethods(driver).FirstOrDefault(method =>
                method.Name.StartsWith("<MakeNewToils>")
                && PatchProcessor.ReadMethodBody(method).Any(instruction => instruction.Value is MethodInfo called
                    && called.DeclaringType == typeof(GeneUtility) && called.Name == nameof(GeneUtility.OffsetHemogen)));
            if (drinkToil != null)
            {
                harmony.Patch(drinkToil, prefix: new HarmonyMethod(typeof(VRECorpseCompat), nameof(DrinkToilPrefix)));
            }
            else
            {
                Log.Warning("[Blood Tastes] VRE - Sanguophage is active but its corpse-drinking toil was not found; corpsefeeder auto-drinking gives no blood tastes.");
            }
        }

        // Postfix: the ability destroys the corpse only after DoBite returns.
        public static void CorpseBitePostfix(Pawn biter, Corpse corpse)
        {
            BloodTasteUtility.ApplyFeed(biter, corpse?.InnerPawn);
        }

        // Prefix: the toil destroys the corpse at its end, and it has no early exit, so the feed always happens.
        public static void DrinkToilPrefix(JobDriver __instance)
        {
            if (__instance.job?.targetA.Thing is Corpse corpse)
            {
                BloodTasteUtility.ApplyFeed(__instance.pawn, corpse.InnerPawn);
            }
        }
    }
}
