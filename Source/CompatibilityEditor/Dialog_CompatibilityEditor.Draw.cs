using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    [StaticConstructorOnStartup]
    internal partial class Dialog_CompatibilityEditor
    {
        // ── Icons ───────────────────────────────────────────
        private static readonly Texture2D RevertStagedIcon;
        private static readonly Texture2D ClearOverrideIcon;

        /// <summary>Loads the row action icons once at startup.</summary>
        static Dialog_CompatibilityEditor()
        {
            RevertStagedIcon = ContentFinder<Texture2D>.Get("UI/RevertStaged");
            ClearOverrideIcon = ContentFinder<Texture2D>.Get("UI/ClearOverride");
        }

        // ── Layout constants ───────────────────────────────
        private const float MARGIN = 10f;
        private const float HEADER_HEIGHT = 35f;
        private const float LEFT_PANEL_W = 190f;
        private const float BUTTON_COL_W = 140f;
        private const float BUTTON_HEIGHT = 30f;
        private const float FILTER_ROW_H = 24f;
        private const int FILTER_COUNT = 6;

        private const float LIST_ROW_H = 40f;
        private const float HEADER_ROW_H = 26f;
        private const float LIST_PAD = 8f;
        private const float PORTRAIT_W = 24f;
        private const float PORTRAIT_H = 34f;

        private const float ROW_HEIGHT = 34f;
        private const float ROW_NAME_W = 120f;
        private const float ROW_VALUE_W = 50f;
        private const float ROW_TEXT_W = 55f;
        private const float ROW_REVERT_W = 24f;
        private const float ROW_GAP = 8f;

        private static readonly Vector2 ListPortraitSize = new Vector2(PORTRAIT_W, PORTRAIT_H);

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1200f, UI.screenWidth * 0.6f),
            Mathf.Min(800f, UI.screenHeight * 0.75f));

        /// <summary>Lays out and draws the header, left panel, centre panel, and button column.</summary>
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, HEADER_HEIGHT);
            Widgets.Label(headerRect, "CompatibilityEditor.Title".Translate());
            Text.Font = GameFont.Small;

            float bodyY = headerRect.yMax + MARGIN;
            float bodyH = inRect.yMax - bodyY;

            Rect leftRect = new Rect(inRect.x, bodyY, LEFT_PANEL_W, bodyH);
            Rect buttonRect = new Rect(inRect.xMax - BUTTON_COL_W, bodyY, BUTTON_COL_W, bodyH);
            Rect centreRect = new Rect(
                leftRect.xMax + MARGIN,
                bodyY,
                buttonRect.xMin - leftRect.xMax - MARGIN * 2,
                bodyH);

            float filterH = FILTER_COUNT * FILTER_ROW_H;
            Rect filterRect = new Rect(leftRect.x, leftRect.y, leftRect.width, filterH);
            Rect listRect = new Rect(
                leftRect.x,
                filterRect.yMax + MARGIN,
                leftRect.width,
                leftRect.height - filterH - MARGIN);

            DrawFilters(filterRect);
            DrawPawnList(listRect);
            DrawCentre(centreRect);
            DrawButtonColumn(buttonRect);
        }

        // ── Left panel ─────────────────────────────────────

        /// <summary>Draws the category/scope checkboxes, laid out horizontally or vertically to fit the rect.</summary>
        private void DrawFilters(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);

            bool horizontal = inner.width > inner.height * 2f;

            // Derive stride from the rect actually given, not from a constant,
            // so the panel can be moved or reoriented without adjusting layout.
            float stride = horizontal
                ? inner.width / FILTER_COUNT
                : inner.height / FILTER_COUNT;

            int i = 0;

            DrawFilterToggle(inner, horizontal, stride, ref i, "CompatibilityEditor.FilterColonists", ref showColonists);
            DrawFilterToggle(inner, horizontal, stride, ref i, "CompatibilityEditor.FilterLodgers", ref showLodgers);
            DrawFilterToggle(inner, horizontal, stride, ref i, "CompatibilityEditor.FilterPrisoners", ref showPrisoners);
            DrawFilterToggle(inner, horizontal, stride, ref i, "CompatibilityEditor.FilterSlaves", ref showSlaves);
            DrawFilterToggle(inner, horizontal, stride, ref i, "CompatibilityEditor.FilterGuests", ref showGuests);
            DrawFilterToggle(inner, horizontal, stride, ref i, "CompatibilityEditor.ThisMapOnly", ref thisMapOnly);
        }

        /// <summary>Draws one filter checkbox at the given slot index and rebuilds candidates if it changes.</summary>
        private void DrawFilterToggle(Rect inner, bool horizontal, float stride, ref int index, string key, ref bool value)
        {
            Rect r = horizontal
                ? new Rect(inner.x + stride * index, inner.y, stride, inner.height)
                : new Rect(inner.x, inner.y + stride * index, inner.width, stride);

            index++;

            bool before = value;
            Widgets.CheckboxLabeled(r, key.Translate(), ref value);
            if (value != before) RebuildCandidates();
        }

        /// <summary>Draws the scrollable left-hand pawn list, culling rows outside the visible area.</summary>
        private void DrawPawnList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(4f);

            float scrollBarW = pawnListHeight > inner.height ? 16f : 0f;
            Rect viewRect = new Rect(0f, 0f, inner.width - scrollBarW, pawnListHeight);

            Widgets.BeginScrollView(inner, ref pawnListScroll, viewRect);

            float top = pawnListScroll.y;
            float bottom = top + inner.height;
            int pawnIndex = 0;

            foreach (ListEntry e in pawnListEntries)
            {
                // Cull off-screen rows, but keep the pawn index in step so
                // alternating backgrounds stay consistent while scrolling.
                if (e.y + e.height < top || e.y > bottom)
                {
                    if (!e.isHeader) pawnIndex++;
                    continue;
                }

                Rect row = new Rect(0f, e.y, viewRect.width, e.height);

                if (e.isHeader)
                {
                    DrawCategoryHeader(row, e.label);
                    continue;
                }

                DrawPawnListRow(row, e.pawn, pawnIndex);
                pawnIndex++;
            }

            Widgets.EndScrollView();
        }

        /// <summary>Draws a small grey category header with an underline, shared by both list panels.</summary>
        private void DrawCategoryHeader(Rect row, string label)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.LowerLeft;
            GUI.color = ColorLibrary.Grey;

            Widgets.Label(new Rect(row.x + LIST_PAD, row.y, row.width - LIST_PAD * 2, row.height), label);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;

            Widgets.DrawLineHorizontal(row.x + LIST_PAD, row.yMax - 1f, row.width - LIST_PAD * 2);
        }

        /// <summary>Draws one pawn row in the left-hand list, and switches focus on click.</summary>
        private void DrawPawnListRow(Rect row, Pawn p, int index)
        {
            if (index % 2 == 1) Widgets.DrawAltRect(row);
            if (p == focusPawn) Widgets.DrawHighlightSelected(row);
            Widgets.DrawHighlightIfMouseover(row);

            Rect portrait = new Rect(
                row.x + LIST_PAD,
                row.y + (row.height - PORTRAIT_H) / 2f,
                PORTRAIT_W,
                PORTRAIT_H);

            GUI.DrawTexture(portrait, PortraitsCache.Get(p, ListPortraitSize, Rot4.South));

            float labelX = portrait.xMax + 6f;
            Rect label = new Rect(labelX, row.y, row.xMax - LIST_PAD - labelX, row.height);

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(label, p.LabelShort);
            Text.Anchor = TextAnchor.UpperLeft;

            if (Widgets.ButtonInvisible(row))
            {
                Pawn local = p;
                TryLeave(() => SetFocusPawn(local));
            }
        }

        // ── Centre panel ────────────────────────────────────

        /// <summary>Draws the centre panel: bulk-action header on top, scrollable row list below.</summary>
        private void DrawCentre(Rect rect)
        {
            Widgets.DrawMenuSection(rect);

            Rect headerRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, BUTTON_HEIGHT);

            Rect listRect = new Rect(
                rect.x + 4f,
                headerRect.yMax + 4f,
                rect.width - 8f,
                rect.yMax - headerRect.yMax - 8f);

            DrawCentreHeader(headerRect);
            DrawRowList(listRect);
        }

        /// <summary>Draws the bulk-action buttons, disabled when there is nothing to act on.</summary>
        private void DrawCentreHeader(Rect rect)
        {
            bool enabled = focusPawn != null && compatListPawns.Count > 0;
            Color prev = GUI.color;
            if (!enabled) GUI.color = Color.gray;

            float w = (rect.width - ROW_GAP * 2) / 3f;

            Rect raiseRect = new Rect(rect.x, rect.y, w, rect.height);
            Rect lowerRect = new Rect(raiseRect.xMax + ROW_GAP, rect.y, w, rect.height);
            Rect setRect = new Rect(lowerRect.xMax + ROW_GAP, rect.y, w, rect.height);

            if (Widgets.ButtonText(raiseRect, "CompatibilityEditor.BulkRaiseFull".Translate()) && enabled)
                PromptRaise();

            if (Widgets.ButtonText(lowerRect, "CompatibilityEditor.BulkLowerFull".Translate()) && enabled)
                PromptLower();

            if (Widgets.ButtonText(setRect, "CompatibilityEditor.BulkSetFull".Translate()) && enabled)
                PromptSet();

            GUI.color = prev;
        }

        /// <summary>Draws the scrollable centre-panel row list, or a placeholder when it's empty.</summary>
        private void DrawRowList(Rect rect)
        {
            if (focusPawn == null) return;

            // The centre list is empty when the focus pawn is the only candidate,
            // or when all other candidates are filtered out.
            if (compatListEntries.Count == 0)
            {
                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);

                Widgets.Label(rect, "CompatibilityEditor.NoOtherPawns".Translate());

                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
                return;
            }

            float scrollBarW = compatListHeight > rect.height ? 16f : 0f;
            Rect viewRect = new Rect(0f, 0f, rect.width - scrollBarW, compatListHeight);

            Widgets.BeginScrollView(rect, ref compatListScroll, viewRect);

            float top = compatListScroll.y;
            float bottom = top + rect.height;
            int index = 0;

            foreach (ListEntry e in compatListEntries)
            {
                if (e.y + e.height < top || e.y > bottom)
                {
                    if (!e.isHeader) index++;
                    continue;
                }

                Rect row = new Rect(0f, e.y, viewRect.width, e.height);

                if (e.isHeader)
                {
                    DrawCategoryHeader(row, e.label);
                    continue;
                }

                DrawRow(row, e.pawn, index);
                index++;
            }

            Widgets.EndScrollView();
        }

        /// <summary>Draws one compatibility row: portrait, name, value, slider, text field, and revert/clear button.</summary>
        private void DrawRow(Rect row, Pawn other, int index)
        {
            int id = other.thingIDNumber;
            bool dirty = IsDirty(id);

            if (index % 2 == 1) Widgets.DrawAltRect(row);
            if (dirty) Widgets.DrawHighlight(row);

            float x = row.x + ROW_GAP;

            // Portrait
            Rect portrait = new Rect(x, row.y + (row.height - PORTRAIT_H) / 2f, PORTRAIT_W, PORTRAIT_H);
            GUI.DrawTexture(portrait, PortraitsCache.Get(other, ListPortraitSize, Rot4.South));
            x = portrait.xMax + ROW_GAP;

            // Name
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, row.y, ROW_NAME_W, row.height), other.LabelShort);
            x += ROW_NAME_W + ROW_GAP;

            // Value
            float value = ValueFor(id);
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = GetCompatibilityColor(value);
            Widgets.Label(new Rect(x, row.y, ROW_VALUE_W, row.height), value.ToString("F2"));
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            x += ROW_VALUE_W + ROW_GAP;

            // Revert button, drawn only when there is something to revert
            bool hasOverride = CompatibilityOverrideStore.Instance != null
                            && CompatibilityOverrideStore.Instance.HasOverride(focusPawn.thingIDNumber, id);

            float sliderRight = row.xMax - ROW_GAP - ROW_REVERT_W - ROW_GAP - ROW_TEXT_W - ROW_GAP;

            Rect sliderRect = new Rect(x, row.y + (row.height - 20f) / 2f, sliderRight - x, 20f);
            Rect textRect = new Rect(sliderRect.xMax + ROW_GAP, row.y + 3f, ROW_TEXT_W, row.height - 6f);
            Rect revertRect = new Rect(textRect.xMax + ROW_GAP, row.y + (row.height - ROW_REVERT_W) / 2f, ROW_REVERT_W, ROW_REVERT_W);

            // Slider
            float newSlider = Widgets.HorizontalSlider(sliderRect, value, SliderMin, SliderMax);
            if (!Mathf.Approximately(newSlider, value))
                SetStaged(id, newSlider);

            // Text box — locals because dictionary values can't be passed by ref
            float typed = ValueFor(id);
            string buf = BufferFor(id);
            Widgets.TextFieldNumeric(textRect, ref typed, ref buf, SliderMin, SliderMax);
            textFieldBuffers[id] = buf;
            if (!Mathf.Approximately(typed, ValueFor(id)))
                SetStaged(id, typed, updateBuffer: false);

            if (dirty)
            {
                if (Widgets.ButtonImage(revertRect, RevertStagedIcon, Color.white, GenUI.MouseoverColor))
                    SetStaged(id, savedValueCache[id]);
                TooltipHandler.TipRegion(revertRect, "CompatibilityEditor.RowRevertStagedTip".Translate());
            }
            else if (hasOverride)
            {
                if (Widgets.ButtonImage(revertRect, ClearOverrideIcon, ColorLibrary.RedReadable, GenUI.MouseoverColor))
                    SetStaged(id, VanillaFor(other));
                TooltipHandler.TipRegion(revertRect, "CompatibilityEditor.RowClearOverrideTip".Translate());
            }
        }

        // ── Right panel ──────────────────────────────────────

        /// <summary>Draws the right-hand column of save/discard/reset buttons.</summary>
        private void DrawButtonColumn(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);

            float y = inner.y;
            float w = inner.width;

            CompatibilityOverrideStore store = CompatibilityOverrideStore.Instance;
            bool dirty = HasStagedChanges;

            DrawColButton(inner.x, ref y, w, "CompatibilityEditor.ResetPawn".Translate(), CanResetPawn(), StageResetPawn);

            DrawColButton(inner.x, ref y, w, "CompatibilityEditor.Discard".Translate(), dirty, DiscardStaged);

            y += 8f;
            Widgets.DrawLineHorizontal(inner.x, y, w);
            y += 8f;

            string saveLabel = dirty
                ? "CompatibilityEditor.SaveChanges".Translate(stagedEdits.Count)
                : "CompatibilityEditor.SaveChanges".Translate(0);

            DrawColButton(inner.x, ref y, w, saveLabel, dirty, CommitStaged);

            DrawColButton(inner.x, ref y, w, "CompatibilityEditor.SaveAndClose".Translate(), dirty, () => { CommitStaged(); base.Close(); });

            y += 8f;
            Widgets.DrawLineHorizontal(inner.x, y, w);
            y += 8f;

            DrawColButton(inner.x, ref y, w, "CompatibilityEditor.Close".Translate(), true, () => Close());

            // Reset All sits apart from the save group — it is the only button whose
            // blast radius is the entire save file.
            float resetY = inner.yMax - BUTTON_HEIGHT;
            DrawColButton(inner.x, ref resetY, w, "CompatibilityEditor.ResetAll".Translate(), store != null && store.Count > 0, PromptResetAll);
        }

        /// <summary>Draws one full-width button in the right-hand column and advances <paramref name="y"/> past it.</summary>
        private void DrawColButton(float x, ref float y, float w, string label, bool enabled, Action onClick)
        {
            Rect r = new Rect(x, y, w, BUTTON_HEIGHT);
            y += BUTTON_HEIGHT + 6f;

            Color prev = GUI.color;
            if (!enabled) GUI.color = Color.gray;

            // ButtonText returns true on click regardless of styling, so the guard
            // has to be on the action rather than the draw.
            if (Widgets.ButtonText(r, label) && enabled)
                onClick();

            GUI.color = prev;
        }

        /// <summary>Colour-codes a compatibility value for quick visual scanning.</summary>
        private static Color GetCompatibilityColor(float value)
        {
            if (value >= 2f) return ColorLibrary.Green;
            if (value >= 1f) return ColorLibrary.White;
            return value < 0f ? ColorLibrary.RedReadable : ColorLibrary.Grey;
        }
    }
}
