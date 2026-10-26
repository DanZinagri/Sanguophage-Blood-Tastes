using System.Collections.Generic;
using Verse;

namespace BloodTastes
{
    // One def per blood taste: drinking from a pawn of any listed xenotype gives the biter this hediff. Xenotypes are
    // defNames, resolved at startup, so a def can list xenotypes from mods that aren't loaded. Put MayRequireAnyOf
    // (the listed mods' packageIds) on both this def and its hediff so neither exists without one of them.
    public class BloodTasteDef : Def
    {
        public List<string> xenotypes;
        public HediffDef hediff;
        // Severity the hediff is given at. A repeat bite keeps the higher of this and the current severity.
        public float severity = 1f;
        // Optional: when the source has one of these hediffs, the taste is given at that entry's severity instead
        // (the highest match wins), so a taste can grow with the source, e.g. Revia's Soul Reap tiers. Hediffs are
        // defNames for the same reason as xenotypes.
        public List<SourceHediffSeverity> severityFromSourceHediffs;

        private List<(HediffDef hediff, float severity)> resolvedSourceSeverities;

        public override void ResolveReferences()
        {
            base.ResolveReferences();
            if (severityFromSourceHediffs.NullOrEmpty())
            {
                return;
            }
            resolvedSourceSeverities = new List<(HediffDef, float)>();
            foreach (SourceHediffSeverity entry in severityFromSourceHediffs)
            {
                HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(entry.hediff);
                if (def != null)
                {
                    resolvedSourceSeverities.Add((def, entry.severity));
                }
            }
        }

        public float SeverityFor(Pawn source)
        {
            float result = severity;
            if (resolvedSourceSeverities == null)
            {
                return result;
            }
            bool matched = false;
            foreach ((HediffDef def, float entrySeverity) in resolvedSourceSeverities)
            {
                if (source.health.hediffSet.HasHediff(def) && (!matched || entrySeverity > result))
                {
                    result = entrySeverity;
                    matched = true;
                }
            }
            return result;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (xenotypes.NullOrEmpty())
            {
                yield return "xenotypes is empty";
            }
            if (hediff == null)
            {
                yield return "hediff is null";
            }
        }
    }

    public class SourceHediffSeverity
    {
        public string hediff;
        public float severity = 1f;
    }
}
