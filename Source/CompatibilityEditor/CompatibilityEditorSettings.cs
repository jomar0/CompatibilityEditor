using Verse;

namespace CompatibilityEditor
{
    public class CompatibilityEditorSettings : ModSettings
    {
        public float sliderMin = -5f;
        public float sliderMax = 5f;
        public bool showButtonBar = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref sliderMin, "sliderMin", -5f);
            Scribe_Values.Look(ref sliderMax, "sliderMax", 5f);
            Scribe_Values.Look(ref showButtonBar, "showButtonBar", true);
            base.ExposeData();
        }
    }
}