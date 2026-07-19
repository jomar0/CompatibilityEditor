using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    internal class Dialog_CompatibilityEditor : Window
    {
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
        private static readonly Vector2 ListPortraitSize = new Vector2(PORTRAIT_W, PORTRAIT_H);

        public override Vector2 InitialSize => new Vector2(
            Mathf.Min(1200f, UI.screenWidth * 0.6f),
            Mathf.Min(800f, UI.screenHeight * 0.75f));

        // ── Pawn categories ────────────────────────────────
        private enum PawnCategory { Colonist, Lodger, Prisoner, Slave, Guest }

        private static readonly PawnCategory[] AllCategories =
            (PawnCategory[])Enum.GetValues(typeof(PawnCategory));

        // ── Filter state ───────────────────────────────────
        private bool showColonists = true;
        private bool showLodgers = false;
        private bool showPrisoners = false;
        private bool showSlaves = false;
        private bool showGuests = false;
        private bool thisMapOnly = true;

        // ── UI state ───────────────────────────────────────
        private Pawn focusPawn;
        private Vector2 pawnListScroll = Vector2.zero;
        private List<Pawn> cachedCandidates = new List<Pawn>();
        private List<ListEntry> listEntries = new List<ListEntry>();
        private float listContentHeight;

        /// <summary>
        /// A single drawable line in the left-hand pawn list: either a category
        /// header or a pawn. Positions are precomputed at rebuild time because
        /// mixed row heights make them awkward to derive during the draw loop.
        /// </summary>
        private struct ListEntry
        {
            public bool isHeader;
            public string label;
            public Pawn pawn;
            public float y;
            public float height;
        }

        public Dialog_CompatibilityEditor()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            draggable = true;
            doCloseX = true;
        }

        public override void PostOpen()
        {
            base.PostOpen();
            RebuildCandidates();
        }

        /// <summary>
        /// Routes every exit path (Close button, Escape, the X) through the
        /// unsaved-changes guard.
        /// </summary>
        public override void Close(bool doCloseSound = true)
        {
            TryLeave(() => base.Close(doCloseSound));
        }

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

        private void DrawCentre(Rect rect) => Widgets.DrawMenuSection(rect);
        private void DrawButtonColumn(Rect rect) => Widgets.DrawMenuSection(rect);

        // ── Pawn sourcing ──────────────────────────────────

        /// <summary>
        /// Classifies a pawn into exactly one category.
        /// Order matters: <c>IsColonist</c> returns true for secure slaves, so
        /// the slave check must come first.
        /// </summary>
        private static PawnCategory CategoryOf(Pawn p)
        {
            if (p.IsPrisonerOfColony) return PawnCategory.Prisoner;
            if (p.IsSlaveOfColony) return PawnCategory.Slave;

            if (p.IsFreeColonist)
            {
                // IsFreeColonist includes quest lodgers — the "temporary
                // colonists" problem. Split them out.
                return p.IsQuestLodger() ? PawnCategory.Lodger : PawnCategory.Colonist;
            }

            return PawnCategory.Guest;
        }

        private bool ShowCategory(PawnCategory c)
        {
            switch (c)
            {
                case PawnCategory.Colonist: return showColonists;
                case PawnCategory.Lodger: return showLodgers;
                case PawnCategory.Prisoner: return showPrisoners;
                case PawnCategory.Slave: return showSlaves;
                default: return showGuests;
            }
        }

        private static string CategoryLabel(PawnCategory c)
        {
            switch (c)
            {
                case PawnCategory.Colonist: return "CompatibilityEditor.FilterColonists".Translate();
                case PawnCategory.Lodger: return "CompatibilityEditor.FilterLodgers".Translate();
                case PawnCategory.Prisoner: return "CompatibilityEditor.FilterPrisoners".Translate();
                case PawnCategory.Slave: return "CompatibilityEditor.FilterSlaves".Translate();
                default: return "CompatibilityEditor.FilterGuests".Translate();
            }
        }

        private List<Pawn> GetCandidatePawns()
        {
            IEnumerable<Pawn> source = thisMapOnly
                ? (IEnumerable<Pawn>)Find.CurrentMap.mapPawns.AllPawns
                : PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive;

            List<Pawn> result = new List<Pawn>();

            foreach (Pawn p in source)
            {
                if (p == null || p.Dead) continue;
                if (!p.RaceProps.Humanlike) continue;
                if (p.HostileTo(Faction.OfPlayer)) continue;
                if (!ShowCategory(CategoryOf(p))) continue;
                result.Add(p);
            }

            return result;
        }

        private void RebuildCandidates()
        {
            cachedCandidates = GetCandidatePawns();
            cachedCandidates.SortBy(p => p.LabelShort);

            // Focus pawn may have been filtered out.
            if (focusPawn != null && !cachedCandidates.Contains(focusPawn))
                focusPawn = null;

            if (focusPawn == null && cachedCandidates.Count > 0)
                SetFocusPawn(cachedCandidates[0]);

            RebuildListEntries();
        }

        /// <summary>
        /// Flattens the candidate list into positioned rows grouped by category,
        /// inserting a header before the first pawn of each non-empty group.
        /// </summary>
        private void RebuildListEntries()
        {
            listEntries.Clear();
            float y = 0f;

            foreach (PawnCategory cat in AllCategories)
            {
                if (!ShowCategory(cat)) continue;

                bool headerDrawn = false;

                foreach (Pawn p in cachedCandidates)
                {
                    if (CategoryOf(p) != cat) continue;

                    if (!headerDrawn)
                    {
                        listEntries.Add(new ListEntry
                        {
                            isHeader = true,
                            label = CategoryLabel(cat),
                            y = y,
                            height = HEADER_ROW_H
                        });
                        y += HEADER_ROW_H;
                        headerDrawn = true;
                    }

                    listEntries.Add(new ListEntry
                    {
                        pawn = p,
                        y = y,
                        height = LIST_ROW_H
                    });
                    y += LIST_ROW_H;
                }
            }

            listContentHeight = y;
        }

        // ── Left panel ─────────────────────────────────────

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

        private void DrawPawnList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(4f);

            float scrollBarW = listContentHeight > inner.height ? 16f : 0f;
            Rect viewRect = new Rect(0f, 0f, inner.width - scrollBarW, listContentHeight);

            Widgets.BeginScrollView(inner, ref pawnListScroll, viewRect);

            float top = pawnListScroll.y;
            float bottom = top + inner.height;
            int pawnIndex = 0;

            foreach (ListEntry e in listEntries)
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

        // ── Stubs ──────────────────────────────────────────

        /// <summary>
        /// Sets the focus pawn and rebuilds the displayed value cache.
        /// </summary>
        private void SetFocusPawn(Pawn p)
        {
            focusPawn = p;
            // TODO: rebuild value cache
        }

        /// <summary>
        /// Prompts before discarding staged changes, then runs the given action.
        /// </summary>
        private void TryLeave(Action onProceed)
        {
            onProceed();
            // TODO: dirty guard
        }
    }
}