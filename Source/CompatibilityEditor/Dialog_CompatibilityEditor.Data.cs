using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    internal partial class Dialog_CompatibilityEditor : Window
    {
        // ── Nested types ────────────────────────────────────
        private enum PawnCategory { Colonist, Lodger, Prisoner, Slave, Guest }

        private static readonly PawnCategory[] AllCategories =
            (PawnCategory[])Enum.GetValues(typeof(PawnCategory));

        /// <summary>
        /// A single drawable entry in the left-hand pawn list: either a category
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

        private List<Pawn> candidatePawns = new List<Pawn>();

        private List<ListEntry> pawnListEntries = new List<ListEntry>();
        private float pawnListHeight;

        private Vector2 compatListScroll = Vector2.zero;
        private List<Pawn> compatListPawns = new List<Pawn>();
        private List<ListEntry> compatListEntries = new List<ListEntry>();
        private float compatListHeight;

        // Staged edits for the current focus pawn only. Cleared on switch.
        // Key: other pawn's thingIDNumber.
        private Dictionary<int, float> stagedEdits = new Dictionary<int, float>();

        // Text field buffers, one per row. Key: other pawn's thingIDNumber.
        private Dictionary<int, string> textFieldBuffers = new Dictionary<int, string>();

        // Saved values as they currently stand — override if one exists, else vanilla.
        // Populated on focus change and after saving. Never computed in the draw loop.
        private Dictionary<int, float> savedValueCache = new Dictionary<int, float>();

        private bool HasStagedChanges => stagedEdits.Count > 0;
        private static float SliderMin => CompatibilityEditorMod.Settings.sliderMin;
        private static float SliderMax => CompatibilityEditorMod.Settings.sliderMax;

        // ── Construction & window lifecycle ─────────────────

        /// <summary>Constructor</summary>
        public Dialog_CompatibilityEditor()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            draggable = true;
            doCloseX = true;
        }

        /// <summary>Builds the initial candidate list as soon as the window opens.</summary>
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
                // IsFreeColonist includes quest lodgers
                return p.IsQuestLodger() ? PawnCategory.Lodger : PawnCategory.Colonist;
            }

            return PawnCategory.Guest;
        }

        /// <summary>Whether pawns of the given category should currently be shown.</summary>
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

        /// <summary>Translated display label for a pawn category.</summary>
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

        /// <summary>Gathers all living, non-hostile, humanlike pawns that pass the active filters.</summary>
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

        /// <summary>
        /// Refreshes the candidate list from the current filters, keeping or
        /// reassigning the focus pawn, and rebuilds every dependent cache.
        /// </summary>
        private void RebuildCandidates()
        {
            candidatePawns = GetCandidatePawns();
            candidatePawns.SortBy(p => p.LabelShort);

            // Focus pawn may have been filtered out.
            if (focusPawn != null && !candidatePawns.Contains(focusPawn))
                focusPawn = null;

            if (focusPawn == null && candidatePawns.Count > 0)
                SetFocusPawn(candidatePawns[0]);   // this already rebuilds the value cache
            else
                RebuildSavedValueCache();                 // focus survived; rows must follow the new candidate set

            stagedEdits.RemoveAll(kvp => !savedValueCache.ContainsKey(kvp.Key));
            RebuildPawnListEntries();                    // always
        }

        /// <summary>
        /// Flattens the candidate list into positioned rows grouped by category,
        /// inserting a header before the first pawn of each non-empty group.
        /// </summary>
        private void RebuildPawnListEntries()
        {
            pawnListEntries.Clear();
            float y = 0f;

            foreach (PawnCategory cat in AllCategories)
            {
                if (!ShowCategory(cat)) continue;

                bool headerDrawn = false;

                foreach (Pawn p in candidatePawns)
                {
                    if (CategoryOf(p) != cat) continue;

                    if (!headerDrawn)
                    {
                        pawnListEntries.Add(new ListEntry
                        {
                            isHeader = true,
                            label = CategoryLabel(cat),
                            y = y,
                            height = HEADER_ROW_H
                        });
                        y += HEADER_ROW_H;
                        headerDrawn = true;
                    }

                    pawnListEntries.Add(new ListEntry
                    {
                        pawn = p,
                        y = y,
                        height = LIST_ROW_H
                    });
                    y += LIST_ROW_H;
                }
            }

            pawnListHeight = y;
        }

        /// <summary>Switches the focus pawn, clearing any staged edits from the previous one.</summary>
        private void SetFocusPawn(Pawn p)
        {
            focusPawn = p;
            stagedEdits.Clear();
            textFieldBuffers.Clear();
            RebuildSavedValueCache();
        }

        /// <summary>
        /// Recomputes the display cache and centre-panel pawn list for the current
        /// focus pawn, preferring a stored override over the vanilla compatibility value.
        /// </summary>
        private void RebuildSavedValueCache()
        {
            savedValueCache.Clear();
            compatListPawns.Clear();

            if (focusPawn == null) return;

            CompatibilityOverrideStore store = CompatibilityOverrideStore.Instance;


            foreach (Pawn other in candidatePawns)
            {
                // The focus pawn is never listed in the centre panel, even if it passes the filters.
                if (other == focusPawn) continue;

                float value;
                // Prefer the stored override if one exists, else fall back to the vanilla value.
                if (store != null && store.TryGetOverride(focusPawn.thingIDNumber, other.thingIDNumber, out float ov))
                    value = ov;
                else
                    value = focusPawn.relations.CompatibilityWith(other);

                // Round to two decimal places for display, but keep the full value in the staged edits.
                savedValueCache[other.thingIDNumber] = Round2(value);
                compatListPawns.Add(other);
            }

            // Rebuild the centre list entries so that the row positions match the new pawn list.
            RebuildCompatListEntries();
        }

        /// <summary>
        /// Recomputes the centre-panel row list for the current focus pawn, inserting a
        /// header before the first pawn of each non-empty category.
        /// </summary>
        private void RebuildCompatListEntries()
        {
            compatListEntries.Clear();
            float y = 0f;

            foreach (PawnCategory cat in AllCategories)
            {
                if (!ShowCategory(cat)) continue;

                bool headerDrawn = false;

                foreach (Pawn p in compatListPawns)
                {
                    if (CategoryOf(p) != cat) continue;

                    if (!headerDrawn)
                    {
                        compatListEntries.Add(new ListEntry
                        {
                            isHeader = true,
                            label = CategoryLabel(cat),
                            y = y,
                            height = HEADER_ROW_H
                        });
                        y += HEADER_ROW_H;
                        headerDrawn = true;
                    }

                    compatListEntries.Add(new ListEntry { pawn = p, y = y, height = ROW_HEIGHT });
                    y += ROW_HEIGHT;
                }
            }

            compatListHeight = y;
        }

        // ── Bulk edit ────────────────────────────────────────

        /// <summary>Opens the bulk dialog for raising values below a threshold to a target.</summary>
        private void PromptRaise()
        {
            Find.WindowStack.Add(new Dialog_BulkValue(
                "CompatibilityEditor.BulkRaise".Translate(),
                new[] { "CompatibilityEditor.BulkAllBelow".Translate().ToString(),
                "CompatibilityEditor.BulkTo".Translate().ToString() },
                new[] { 0f, 1f },
                SliderMin, SliderMax,
                v => ApplyBulk(current => current < v[0], v[1]), BulkClamp.SecondAtLeast));
        }

        /// <summary>Opens the bulk dialog for lowering values above a threshold to a target.</summary>
        private void PromptLower()
        {
            Find.WindowStack.Add(new Dialog_BulkValue(
                "CompatibilityEditor.BulkLower".Translate(),
                new[] { "CompatibilityEditor.BulkAllAbove".Translate().ToString(),
                "CompatibilityEditor.BulkTo".Translate().ToString() },
                new[] { 0f, -1f },
                SliderMin, SliderMax,
                v => ApplyBulk(current => current > v[0], v[1]), BulkClamp.SecondAtMost));
        }

        /// <summary>Opens the bulk dialog for setting every visible row to a single value.</summary>
        private void PromptSet()
        {
            Find.WindowStack.Add(new Dialog_BulkValue(
                "CompatibilityEditor.BulkSet".Translate(),
                new[] { "CompatibilityEditor.BulkTo".Translate().ToString() },
                new[] { 0f },
                SliderMin, SliderMax,
                v => ApplyBulk(current => true, v[0])));
        }

        /// <summary>
        /// Stages <paramref name="value"/> on every visible row whose current value
        /// satisfies <paramref name="shouldApply"/>. Nothing is written to the store.
        /// </summary>
        private void ApplyBulk(Func<float, bool> shouldApply, float value)
        {
            foreach (Pawn other in compatListPawns)
            {
                int id = other.thingIDNumber;
                if (shouldApply(ValueFor(id)))
                    SetStaged(id, value);
            }
        }

        // ── Staged value helpers ─────────────────────────────

        /// <summary>Gets the staged value for a row if it has a staged change, otherwise the saved value.</summary>
        private float ValueFor(int otherId)
        {
            if (stagedEdits.TryGetValue(otherId, out float s)) return s;
            return savedValueCache.TryGetValue(otherId, out float d) ? d : 0f;
        }

        /// <summary>Whether the row has an unsaved staged edit.</summary>
        private bool IsDirty(int otherId) => stagedEdits.ContainsKey(otherId);

        /// <summary>Stages a value for a row, clearing the stage if it matches the saved value, and refreshes the text buffer.</summary>
        private void SetStaged(int otherId, float value, bool updateBuffer = true)
        {
            value = Round2(value);

            if (savedValueCache.TryGetValue(otherId, out float saved) && Mathf.Approximately(value, saved))
                stagedEdits.Remove(otherId); // clear the stage if the new value matches the saved value
            else
                stagedEdits[otherId] = value;

            if (updateBuffer) textFieldBuffers[otherId] = value.ToString("F2");
        }

        /// <summary>Text field buffer for a row, lazily initialised from its current value.</summary>
        private string BufferFor(int otherId)
        {
            if (!textFieldBuffers.TryGetValue(otherId, out string b))
            {
                b = ValueFor(otherId).ToString("F2");
                textFieldBuffers[otherId] = b;
            }
            return b;
        }

        /// <summary>The un-overridden compatibility value for the focus pawn and <paramref name="other"/>.</summary>
        private float VanillaFor(Pawn other)
        {
            CompatibilityOverrideStore store = CompatibilityOverrideStore.Instance;
            if (store != null && store.TryGetVanilla(focusPawn.thingIDNumber, other.thingIDNumber, out float v))
                return Round2(v);

            // No stored override, so the postfix doesn't fire and this is the real value.
            return Round2(focusPawn.relations.CompatibilityWith(other));
        }

        /// <summary>Rounds to 2 decimal places, matching the precision the UI displays and stores.</summary>
        private static float Round2(float v) => Mathf.Round(v * 100f) / 100f;

        // ── Staged changes  ─────────────────────────

        /// <summary>Writes every staged edit to the override store and clears the staging area.</summary>
        private void CommitStaged()
        {
            CompatibilityOverrideStore store = CompatibilityOverrideStore.Instance;
            if (store == null || focusPawn == null) { stagedEdits.Clear(); return; }

            foreach (Pawn other in compatListPawns)
            {
                if (!stagedEdits.TryGetValue(other.thingIDNumber, out float value)) continue;
                store.SetOverride(focusPawn.thingIDNumber, other.thingIDNumber, value, VanillaFor(other));
            }

            stagedEdits.Clear();
            textFieldBuffers.Clear();
            RebuildSavedValueCache();
            if (Prefs.DevMode)
                Log.Message(store.DebugDump());
        }

        /// <summary>Drops every staged edit without touching the override store.</summary>
        private void DiscardStaged()
        {
            stagedEdits.Clear();
            textFieldBuffers.Clear();
        }

        /// <summary>Stages every row of the focus pawn back to its vanilla value.</summary>
        private void StageResetPawn()
        {
            foreach (Pawn other in compatListPawns)
                SetStaged(other.thingIDNumber, VanillaFor(other));
        }

        /// <summary>Whether "Reset Pawn" has anything to do: staged edits or a stored override.</summary>
        private bool CanResetPawn()
        {
            if (focusPawn == null) return false; // can't reset if there's no pawn to reset
            if (HasStagedChanges) return true; // staged edits are always resettable

            CompatibilityOverrideStore store = CompatibilityOverrideStore.Instance;
            if (store == null) return false; // no store means no overrides, so nothing to reset

            foreach (Pawn other in compatListPawns)
                if (store.HasOverride(focusPawn.thingIDNumber, other.thingIDNumber))
                    return true; // there's at least one override to reset

            return false; // no staged edits and no overrides, so nothing to reset
        }

        /// <summary>Confirms and, if accepted, clears every override in the store across all pawns.</summary>
        private void PromptResetAll()
        {
            CompatibilityOverrideStore store = CompatibilityOverrideStore.Instance;
            if (store == null || store.Count == 0) return;

            int count = store.Count;

            TryLeave(() =>
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "CompatibilityEditor.BulkResetAllConfirm".Translate(count),
                    () => { store.ClearAll(); RebuildSavedValueCache(); },
                    destructive: true)));
        }

        // ── Exit guard ────────────────────────────────────────

        /// <summary>Runs <paramref name="onProceed"/> immediately, or prompts to save/discard first if there are unsaved staged edits.</summary>
        private void TryLeave(Action onProceed)
        {
            if (!HasStagedChanges) // we don't care if there's nothing to save
            {
                onProceed();
                return;
            }

            string pawnName = focusPawn != null ? focusPawn.LabelShort : "";

            Find.WindowStack.Add(new Dialog_ConfirmExit(
                "CompatibilityEditor.UnsavedPrompt".Translate(stagedEdits.Count, pawnName),
                new List<Dialog_ConfirmExit.ButtonSpec>
                {
                    new Dialog_ConfirmExit.ButtonSpec("CompatibilityEditor.PromptSave".Translate(), () => { CommitStaged(); onProceed(); }),
                    new Dialog_ConfirmExit.ButtonSpec("CompatibilityEditor.PromptDiscard".Translate(), () => { DiscardStaged(); onProceed(); }),
                    new Dialog_ConfirmExit.ButtonSpec("CancelButton".Translate(), null)}));
        }
    }
}
