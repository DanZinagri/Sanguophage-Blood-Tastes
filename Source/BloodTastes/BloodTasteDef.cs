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
}
