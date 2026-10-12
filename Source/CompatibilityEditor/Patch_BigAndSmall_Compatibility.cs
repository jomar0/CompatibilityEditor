using System.Reflection;
using HarmonyLib;
using Verse;

namespace CompatibilityEditor
{
    /// <summary>
    /// Big and Small does not read compatibility solely through
    /// <c>Pawn_RelationsTracker.CompatibilityWith</c>. It also has a parallel
    /// extension, <c>RomancePatches.TryGetCompatibilityWith</c>, which recomputes
    /// compatibility from scratch (age gap + per-pair offset, scaled by its own
    /// romance tags) and is called directly by its romance-eligibility check.
    ///
    /// Because that path never calls <c>CompatibilityWith</c>, our postfix on
    /// that method never fires for it, so overrides were invisible on B&S-tagged
    /// pawns — and its <c>Mathf.Max(..., oldValue)</c> floor prevented lowering a
    /// pair's compatibility at all.
    ///
    /// This patch postfixes the B&S method directly, forcing our stored override
    /// when one exists. It is gated on B&S being present via <see cref="Prepare"/>,
    /// so it silently does nothing when the mod is absent — no hard dependency.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_BigAndSmall_Compatibility
    {
        // Runs before Harmony tries to apply the patch. Returning false skips it
        // entirely, which is how we avoid a hard dependency on Big and Small.
        public static bool Prepare()
        {
            return AccessTools.TypeByName("BigAndSmall.RomancePatches") != null;
        }

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method("BigAndSmall.RomancePatches:TryGetCompatibilityWith");
        }

        // Runs last so our override is the final word, after B&S's own maths.
        // Signature notes:
        //   pawn / otherPawn  — the extension's "this" pawn and its target
        //   ref float result  — the method's `out` value (the computed compatibility)
        //   ref bool __result — the method's bool return (true = "I set a value")
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn pawn, Pawn otherPawn, ref float result, ref bool __result)
        {
            if (pawn == null || otherPawn == null) return;

            CompatibilityOverrideStore store = CompatibilityOverrideStore.Instance;
            if (store == null) return;

            if (store.TryGetOverride(pawn.thingIDNumber, otherPawn.thingIDNumber, out float ov))
            {
                result = ov;
                __result = true;
            }
        }
    }
}
