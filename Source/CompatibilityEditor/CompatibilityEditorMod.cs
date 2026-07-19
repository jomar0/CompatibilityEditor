using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    [StaticConstructorOnStartup]
    public class CompatibilityEditorMod : Mod
    {
        public static CompatibilityEditorSettings Settings;

        public CompatibilityEditorMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<CompatibilityEditorSettings>();
            var harmony = new Harmony("jomar.compatibilityeditor");
            harmony.PatchAll();
        }

        public override string SettingsCategory()
        {
            return "CompatibilityEditor.Title".Translate();
        }

        private string sliderMinBuffer;
        private string sliderMaxBuffer;

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (sliderMinBuffer == null) sliderMinBuffer = Settings.sliderMin.ToString("F1");
            if (sliderMaxBuffer == null) sliderMaxBuffer = Settings.sliderMax.ToString("F1");

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // Slider Min
            float textBoxWidth = 60f;
            Rect minRow = listing.GetRect(Text.LineHeight);
            Rect minLabelRect = new Rect(minRow.x, minRow.y, minRow.width * 0.3f, minRow.height);
            Rect minSliderRect = new Rect(minLabelRect.xMax, minRow.y, minRow.width * 0.5f, minRow.height);
            Rect minTextRect = new Rect(minSliderRect.xMax + 10f, minRow.y, textBoxWidth, minRow.height);

            Widgets.Label(minLabelRect, "CompatibilityEditor.SliderMin".Translate());

            float prevMin = Settings.sliderMin;
            Settings.sliderMin = Widgets.HorizontalSlider(minSliderRect, Settings.sliderMin, -20f, 0f);
            if (Settings.sliderMin != prevMin)
                sliderMinBuffer = Settings.sliderMin.ToString("F1");

            float prevMinText = Settings.sliderMin;
            Widgets.TextFieldNumeric(minTextRect, ref Settings.sliderMin, ref sliderMinBuffer, -20f, 0f);
            if (Settings.sliderMin != prevMinText)
                Settings.sliderMin = Mathf.Round(Settings.sliderMin * 10f) / 10f;

            listing.Gap(12f);

            // Slider Max
            Rect maxRow = listing.GetRect(Text.LineHeight);
            Rect maxLabelRect = new Rect(maxRow.x, maxRow.y, maxRow.width * 0.3f, maxRow.height);
            Rect maxSliderRect = new Rect(maxLabelRect.xMax, maxRow.y, maxRow.width * 0.5f, maxRow.height);
            Rect maxTextRect = new Rect(maxSliderRect.xMax + 10f, maxRow.y, textBoxWidth, maxRow.height);

            Widgets.Label(maxLabelRect, "CompatibilityEditor.SliderMax".Translate());

            float prevMax = Settings.sliderMax;
            Settings.sliderMax = Widgets.HorizontalSlider(maxSliderRect, Settings.sliderMax, 0f, 20f);
            if (Settings.sliderMax != prevMax)
                sliderMaxBuffer = Settings.sliderMax.ToString("F1");

            float prevMaxText = Settings.sliderMax;
            Widgets.TextFieldNumeric(maxTextRect, ref Settings.sliderMax, ref sliderMaxBuffer, 0f, 20f);
            if (Settings.sliderMax != prevMaxText)
                Settings.sliderMax = Mathf.Round(Settings.sliderMax * 10f) / 10f;

            listing.Gap(12f);

            // Button bar toggle
            listing.CheckboxLabeled("CompatibilityEditor.ShowButtonBar".Translate(), ref Settings.showButtonBar, tooltip: "CompatibilityEditor.ShowButtonBarTip".Translate());

            listing.Gap(12f);

            if (listing.ButtonText("CompatibilityEditor.ResetDefaults".Translate()))
            {
                Settings.sliderMin = -5f;
                Settings.sliderMax = 5f;
                Settings.showButtonBar = true;
                sliderMinBuffer = Settings.sliderMin.ToString("F1");
                sliderMaxBuffer = Settings.sliderMax.ToString("F1");
            }

            listing.End();
            ApplyButtonVisibility();
        }

        public static void ApplyButtonVisibility()
        {
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("CompatibilityEditor");
            if (def != null)
                def.buttonVisible = Settings.showButtonBar;
        }
    }
}