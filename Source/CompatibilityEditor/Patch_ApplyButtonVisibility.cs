using HarmonyLib;
using Verse;

namespace CompatibilityEditor
{
    [HarmonyPatch(typeof(UIRoot_Entry), nameof(UIRoot_Entry.Init))]
    public static class Patch_ApplyButtonVisibility
    {
        static void Postfix()
        {
            CompatibilityEditorMod.ApplyButtonVisibility();
        }
    }
}