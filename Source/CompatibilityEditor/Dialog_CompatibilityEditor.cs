using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    [StaticConstructorOnStartup]
    internal class Dialog_CompatibilityEditor : Window
    {
        // ── Layout constants ───────────────────────────────
        private const float MARGIN = 10f;
        private const float HEADER_HEIGHT = 35f;
        private const float LEFT_PANEL_W = 190f;
        private const float BUTTON_COL_W = 140f;
        private const float BUTTON_HEIGHT = 30f;
        private const float ROW_HEIGHT = 34f;
        private const float PORTRAIT_SMALL = 32f;
        private const float FILTER_ROW_H = 24f;
        private const int FILTER_COUNT = 6;

        public override Vector2 InitialSize => new Vector2(1000f, 640f);

        public Dialog_CompatibilityEditor()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            draggable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Header
            Text.Font = GameFont.Medium;
            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, HEADER_HEIGHT);
            Widgets.Label(headerRect, "CompatibilityEditor.Title".Translate());
            Text.Font = GameFont.Small;

            float bodyY = headerRect.yMax + MARGIN;
            float bodyH = inRect.yMax - bodyY;

            // Three columns
            Rect leftRect = new Rect(inRect.x, bodyY, LEFT_PANEL_W, bodyH);
            Rect buttonRect = new Rect(inRect.xMax - BUTTON_COL_W, bodyY, BUTTON_COL_W, bodyH);
            Rect centreRect = new Rect(
                leftRect.xMax + MARGIN,
                bodyY,
                buttonRect.xMin - leftRect.xMax - MARGIN * 2,
                bodyH);

            // Left column splits into filters + list
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

        private void DrawFilters(Rect rect)      => Widgets.DrawMenuSection(rect);
        private void DrawPawnList(Rect rect)     => Widgets.DrawMenuSection(rect);
        private void DrawCentre(Rect rect)       => Widgets.DrawMenuSection(rect);
        private void DrawButtonColumn(Rect rect) => Widgets.DrawMenuSection(rect);
    }
}