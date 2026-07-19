using HarmonyLib;
using RimWorld;
using Verse;

namespace CompatibilityEditor
{
    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.CompatibilityWith))]
    [HarmonyPriority(Priority.Last)]
    internal class Patch_CompatibilityWith
    {
        static void Postfix(Pawn ___pawn, Pawn otherPawn, ref float __result)
        {
            if (CompatibilityOverrideStore.Instance != null
                && CompatibilityOverrideStore.Instance.TryGetOverride(
                    ___pawn.thingIDNumber, otherPawn.thingIDNumber, out float val))
            {
                __result = val;
            }
        }
    }
}
