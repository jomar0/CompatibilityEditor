using RimWorld;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    public class MainTabWindow_CompatibilityEditor : MainTabWindow
    {
        public override void PreOpen()
        {
            base.PreOpen();
            Find.MainTabsRoot.EscapeCurrentTab(false);
            Find.WindowStack.Add(new Dialog_CompatibilityEditor());
        }

        public override void DoWindowContents(Rect inRect)
        {
            // we're using a dialog for the actual UI, so we shouldnt do anything here
        }

        public override Vector2 RequestedTabSize => Vector2.zero;
    }
}