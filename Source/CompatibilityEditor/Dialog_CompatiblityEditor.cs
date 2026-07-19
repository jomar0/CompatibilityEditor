using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    [StaticConstructorOnStartup]
    internal class Dialog_CompatibilityEditor : Window
    {
        private static readonly Texture2D WarningIcon;

        static Dialog_CompatibilityEditor()
        {
            WarningIcon = ContentFinder<Texture2D>.Get("UI/Widgets/YellowWarning");
        }


        private const float MARGIN = 10f;
        private const float HEADER_HEIGHT = 35f;
        private const float BUTTON_HEIGHT = 30f;
        private const float BUTTON_WIDTH = 120f;
        private const float PAWN_AREA_WIDTH = 120f;
        private static float SliderMin => CompatibilityEditorMod.Settings.sliderMin;
        private static float SliderMax => CompatibilityEditorMod.Settings.sliderMax;

        private Pawn pawn1;
        private Pawn pawn2;
        private float sliderValue;
        private float originalValue;
        private float vanillaValue;
        private string textBuffer;
        private bool hasUnsavedChanges;

        public Dialog_CompatibilityEditor() : base()
        {
            List<Pawn> colonists = Find.CurrentMap.mapPawns.FreeColonists;
            pawn1 = colonists.Count > 0 ? colonists[0] : null;
            pawn2 = colonists.Count > 1 ? colonists[1] : null;

            LoadCurrentValue();

            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;

        }

        private void LoadCurrentValue()
        {
            if (pawn1 == null || pawn2 == null)
            {
                sliderValue = 0f;
                textBuffer = "0.00";
                return;
            }
            // If there is a compatibility override store, we defer to that value
            if (CompatibilityOverrideStore.Instance != null)
            {
                if (CompatibilityOverrideStore.Instance.TryGetOverride(pawn1.thingIDNumber, pawn2.thingIDNumber, out float val)) sliderValue = val;
                else sliderValue = pawn1.relations.CompatibilityWith(pawn2);
                if (CompatibilityOverrideStore.Instance.TryGetVanilla(pawn1.thingIDNumber, pawn2.thingIDNumber, out float vanillaVal)) vanillaValue = vanillaVal;
                else vanillaValue = pawn1.relations.CompatibilityWith(pawn2);
            }


            sliderValue = Mathf.Round(sliderValue * 100f) / 100f; // make sure the slider value is to 2dp
            originalValue = sliderValue;
            textBuffer = sliderValue.ToString("F2");
            hasUnsavedChanges = false;
        }

        private static Color GetCompatibilityColor(float value)
        {
            if (value >= 2f) return ColorLibrary.Green;
            if (value >= 1f) return ColorLibrary.White;
            return value < 0f ? ColorLibrary.RedReadable : ColorLibrary.Grey;
        }

        public override Vector2 InitialSize => new Vector2(500f, 450f);
        private static readonly Vector2 PortraitSize = new Vector2(70f, 110f);


        private bool ValidPairSelected => pawn1 != null && pawn2 != null && pawn1.thingIDNumber != pawn2.thingIDNumber;
        public override void DoWindowContents(Rect rect)
        {
            //Header
            Text.Font = GameFont.Medium;
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, HEADER_HEIGHT);
            Widgets.Label(headerRect, "CompatibilityEditor.Title".Translate());
            Text.Font = GameFont.Small;

            // Pawn Area
            Rect pawnSectionRect = new Rect(
                rect.x,
                headerRect.yMax + MARGIN,
                rect.width,
                PortraitSize.y + Text.LineHeight + MARGIN * 6
                );

            Widgets.DrawMenuSection(pawnSectionRect);
            Rect pawnInner = pawnSectionRect.ContractedBy(MARGIN * 2);

            float totalWidth = PAWN_AREA_WIDTH * 3 + MARGIN * 2;
            float startX = pawnInner.x + (pawnInner.width - totalWidth) / 2f;

            Rect leftCol = new Rect(startX, pawnInner.y, PAWN_AREA_WIDTH, pawnInner.height);
            Rect centreCol = new Rect(leftCol.xMax + MARGIN, pawnInner.y, PAWN_AREA_WIDTH, pawnInner.height);
            Rect rightCol = new Rect(centreCol.xMax + MARGIN, pawnInner.y, PAWN_AREA_WIDTH, pawnInner.height);

            DrawPawnColumn(leftCol, pawn1, p => pawn1 = p);
            DrawPawnColumn(rightCol, pawn2, p => pawn2 = p);
            DrawCompatibilityLabel(centreCol);

            // Controls
            Rect controlsSectionRect = new Rect(
                rect.x,
                pawnSectionRect.yMax + MARGIN,
                rect.width,
                BUTTON_HEIGHT + MARGIN * 4
            );
            Widgets.DrawMenuSection(controlsSectionRect);
            Rect controlsInner = controlsSectionRect.ContractedBy(MARGIN * 2);

            float textBoxWidth = 60f;
            Rect sliderRect = new Rect(
                controlsInner.x,
                controlsInner.y,
                controlsInner.width - textBoxWidth - MARGIN,
                controlsInner.height
            );
            Rect textBoxRect = new Rect(
                sliderRect.xMax + MARGIN,
                controlsInner.y,
                textBoxWidth,
                BUTTON_HEIGHT
            );


            Color prevColor; // storage for gui color


            //Slider
            if (ValidPairSelected)
            {
                float prevSliderValue = sliderValue;
                sliderValue = Widgets.HorizontalSlider(sliderRect, sliderValue, SliderMin, SliderMax);
                if (!Mathf.Approximately(sliderValue, prevSliderValue))
                {
                    sliderValue = Mathf.Round(sliderValue * 100f) / 100f; // round to 2dp
                    textBuffer = sliderValue.ToString("F2");
                    hasUnsavedChanges = true;
                }

                //textbox
                float prevTextValue = sliderValue;
                Widgets.TextFieldNumeric(textBoxRect, ref sliderValue, ref textBuffer, SliderMin, SliderMax);
                if (!Mathf.Approximately(sliderValue, prevTextValue))
                {
                    sliderValue = Mathf.Round(sliderValue * 100f) / 100f; // round to 2dp
                    hasUnsavedChanges = true;
                }
            }
            else // if the pair is invalid, grey out the slider and textbox and show N/A in the textbox
            {
                prevColor = GUI.color;
                Widgets.HorizontalSlider(sliderRect, 0f, SliderMin, SliderMax);
                Widgets.Label(textBoxRect, "N/A");
                GUI.color = prevColor;
            }

            // buttons
            float totalButtonWidth = BUTTON_WIDTH * 3 + MARGIN * 2;
            float buttonStartX = rect.x + (rect.width - totalButtonWidth) / 2f;
            float buttonY = rect.yMax - BUTTON_HEIGHT - MARGIN;

            Rect resetRect = new Rect(buttonStartX, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect closeRect = new Rect(resetRect.xMax + MARGIN, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);
            Rect saveRect = new Rect(closeRect.xMax + MARGIN, buttonY, BUTTON_WIDTH, BUTTON_HEIGHT);


            // reset button
            if (Widgets.ButtonText(resetRect, "CompatibilityEditor.Reset".Translate()))
            {
                if (ValidPairSelected && CompatibilityOverrideStore.Instance != null)
                {
                    if (CompatibilityOverrideStore.Instance.TryGetVanilla(pawn1.thingIDNumber, pawn2.thingIDNumber, out float vanillaVal))
                    {
                        sliderValue = vanillaVal;
                    }
                    else
                    {
                        sliderValue = pawn1.relations.CompatibilityWith(pawn2);
                    }
                    sliderValue = Mathf.Round(sliderValue * 100f) / 100f; // round to 2dp
                    textBuffer = sliderValue.ToString("F2");
                    hasUnsavedChanges = !Mathf.Approximately(sliderValue, originalValue); // only mark as having unsaved changes if the reset value is different from the original value

                }
            }


            //close button
            if (Widgets.ButtonText(closeRect, "CompatibilityEditor.Close".Translate()))
            {
                Close();
            }

            // save button
            prevColor = GUI.color;
            if (!hasUnsavedChanges || !ValidPairSelected || CompatibilityOverrideStore.Instance == null)
            {
                GUI.color = Color.gray;
            }
            if (Widgets.ButtonText(saveRect, "CompatibilityEditor.Save".Translate()))
            {
                if (hasUnsavedChanges && ValidPairSelected && CompatibilityOverrideStore.Instance != null)
                {
                    CompatibilityOverrideStore.Instance.SetOverride(pawn1.thingIDNumber, pawn2.thingIDNumber, sliderValue, vanillaValue);
                    Close();
                }
            }
            GUI.color = prevColor;

            TooltipHandler.TipRegion(resetRect, "CompatibilityEditor.ResetTip".Translate());
            TooltipHandler.TipRegion(closeRect, "CompatibilityEditor.CloseTip".Translate());
            TooltipHandler.TipRegion(saveRect, hasUnsavedChanges ? "CompatibilityEditor.SaveTip".Translate() : "CompatibilityEditor.NoChangesTip".Translate());

            Text.Font = GameFont.Small;
        }


        private void DrawPawnColumn(Rect rect, Pawn pawn, Action<Pawn> onSelect)
        {
            if (pawn == null) return;

            // Name label
            Rect nameRect = new Rect(rect.x, rect.y, rect.width, Text.LineHeight);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(nameRect, pawn.LabelShort);
            Text.Anchor = TextAnchor.UpperLeft;

            // Portrait
            Rect portraitRect = new Rect(
                rect.x + (rect.width - PortraitSize.x) / 2f,
                nameRect.yMax + MARGIN,
                PortraitSize.x,
                PortraitSize.y
            );
            Widgets.DrawOptionBackground(portraitRect, false);
            Widgets.DrawHighlightIfMouseover(portraitRect);
            GUI.DrawTexture(portraitRect, PortraitsCache.Get(pawn, PortraitSize, Rot4.South));

            // Click to select different pawn
            if (Widgets.ButtonInvisible(portraitRect))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (Pawn p in Find.CurrentMap.mapPawns.FreeColonists)
                {
                    Pawn localPawn = p;
                    options.Add(new FloatMenuOption(p.LabelShort, () =>
                    {
                        onSelect(localPawn);
                        LoadCurrentValue();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        private void DrawCompatibilityLabel(Rect rect)
        {
            if (!ValidPairSelected)
            {
                float iconSize = 32f;
                Rect iconRect = new Rect(
                    rect.x + (rect.width - iconSize) / 2f,
                    rect.y + (rect.height - iconSize) / 2f,
                    iconSize,
                    iconSize
                );
                //GUI.color = Color.yellow;
                GUI.DrawTexture(iconRect, WarningIcon);
                //GUI.color = Color.white;
                return;
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;
            GUI.color = GetCompatibilityColor(sliderValue);
            Widgets.Label(rect, sliderValue.ToString("F2"));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
        }

    }
}
