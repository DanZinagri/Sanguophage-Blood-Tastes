using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace BloodTastes
{
    // Every bite on a living pawn goes through SanguophageUtility.DoBite: the Bloodfeed ability and the warden's
    // prisoner bloodfeed job both call it, as do VRE - Sanguophage's sanguophage and animal bites and its
    // Hemohunter mental state. Hemogen packs never do. So one postfix covers every bite, and nothing runs between
    // bites. VRE's corpse feeding skips DoBite and is patched separately in VRECorpseCompat.
    [HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.DoBite))]
    public static class Patch_DoBite
    {
        public static void Postfix(Pawn biter, Pawn victim)
        {
            BloodTasteUtility.ApplyFeed(biter, victim);
        }
    }

    public static class BloodTasteUtility
    {
        private static Dictionary<XenotypeDef, BloodTasteDef> tastesByXenotype;
        // Each drug high, with the drug that gives it. Drinking blood carrying a high counts as taking one dose of
        // that drug, so vanilla's own ingestion code does all of it: the high, tolerance, addiction or an existing
        // addiction's need, genetic dependencies, overdose, and drug-use ideology events.
        private static Dictionary<HediffDef, ThingDef> drugForHigh;

        private static readonly HashSet<ThingDef> dosedThisFeed = new HashSet<ThingDef>();
        private static readonly StringBuilder moteText = new StringBuilder();

        private static void EnsureTables()
        {
            if (tastesByXenotype != null)
            {
                return;
            }
            tastesByXenotype = new Dictionary<XenotypeDef, BloodTasteDef>();
            foreach (BloodTasteDef def in DefDatabase<BloodTasteDef>.AllDefsListForReading)
            {
                if (def.hediff == null || def.xenotypes == null)
                {
                    continue;
                }
                // defNames, so a def can list xenotypes from mods that aren't loaded without a cross-reference error.
                foreach (string defName in def.xenotypes)
                {
                    XenotypeDef xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(defName);
                    if (xenotype == null)
                    {
                        continue;
                    }
                    if (tastesByXenotype.TryGetValue(xenotype, out BloodTasteDef other))
                    {
                        Log.Warning($"[Blood Tastes] {def.defName} and {other.defName} both list {defName}; using {other.defName}.");
                        continue;
                    }
                    tastesByXenotype[xenotype] = def;
                }
            }

            // A drug's high is whatever hediff its ingestion gives. Tolerances are given the same way, so drop
            // every chemical's tolerance and addiction hediff, plus anything shaped like a tolerance (the
            // DrugEffectFactor comp) for mods that don't fill in ChemicalDef.toleranceHediff. When two drugs give the
            // same high, the first loaded (vanilla before mods) is the one dosed.
            drugForHigh = new Dictionary<HediffDef, ThingDef>();
            foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!thing.IsDrug || thing.ingestible?.outcomeDoers == null)
                {
                    continue;
                }
                foreach (IngestionOutcomeDoer doer in thing.ingestible.outcomeDoers)
                {
                    if (doer is IngestionOutcomeDoer_GiveHediff give && give.hediffDef != null && !drugForHigh.ContainsKey(give.hediffDef))
                    {
                        drugForHigh[give.hediffDef] = thing;
                    }
                }
            }
            foreach (ChemicalDef chemical in DefDatabase<ChemicalDef>.AllDefsListForReading)
            {
                if (chemical.toleranceHediff != null)
                {
                    drugForHigh.Remove(chemical.toleranceHediff);
                }
                if (chemical.addictionHediff != null)
                {
                    drugForHigh.Remove(chemical.addictionHediff);
                }
            }
            drugForHigh.RemoveAll(pair => pair.Key.IsAddiction || pair.Key.HasComp(typeof(HediffComp_DrugEffectFactor)));
        }

        // source is the pawn drunk from; for a corpse, its inner pawn, which keeps its xenotype and hediffs.
        public static void ApplyFeed(Pawn biter, Pawn source)
        {
            if (biter?.health == null || source?.health == null || biter == source)
            {
                return;
            }
            EnsureTables();
            BloodTastesSettings settings = BloodTastesMod.settings;
            moteText.Clear();

            // Custom xenotypes report Baseliner, so they would wrongly taste like baseliners: skip them.
            if (settings.xenotypeTastes && source.genes != null && !source.genes.UniqueXenotype
                && tastesByXenotype.TryGetValue(source.genes.Xenotype, out BloodTasteDef taste))
            {
                Hediff given = GiveOrTopUp(biter, taste.hediff, taste.severity);
                if (given != null)
                {
                    given.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                    AppendMote(given.LabelBaseCap);
                }
            }

            if (settings.copyDrugs && biter.needs != null && biter.carryTracker != null)
            {
                // One dose per drug per feed, even if the source carries two of its highs.
                dosedThisFeed.Clear();
                foreach (Hediff high in source.health.hediffSet.hediffs)
                {
                    if (drugForHigh.TryGetValue(high.def, out ThingDef drug))
                    {
                        dosedThisFeed.Add(drug);
                    }
                }
                foreach (ThingDef drug in dosedThisFeed)
                {
                    // A fresh, never-spawned single dose: Ingested treats it like one the biter carried and
                    // swallowed, then destroys it.
                    Thing dose = ThingMaker.MakeThing(drug);
                    dose.Ingested(biter, 0f);
                    AppendMote(drug.LabelCap);
                    if (biter.Dead)
                    {
                        break;
                    }
                }
                dosedThisFeed.Clear();
            }

            if (settings.showMote && moteText.Length > 0 && biter.Spawned)
            {
                MoteMaker.ThrowText(biter.DrawPos, biter.Map, moteText.ToString());
            }
        }

        // Adds the hediff, or raises an existing one to this severity. Returns null when nothing was given.
        private static Hediff GiveOrTopUp(Pawn pawn, HediffDef def, float severity)
        {
            if (severity <= 0f)
            {
                return null;
            }
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                if (existing.Severity < severity)
                {
                    existing.Severity = severity;
                }
                return existing;
            }
            Hediff hediff = HediffMaker.MakeHediff(def, pawn);
            hediff.Severity = severity;
            pawn.health.AddHediff(hediff);
            return pawn.health.hediffSet.hediffs.Contains(hediff) ? hediff : null;
        }

        private static void AppendMote(string label)
        {
            if (moteText.Length > 0)
            {
                moteText.Append(", ");
            }
            moteText.Append(label);
        }
    }
}
