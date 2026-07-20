using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    internal class CompatibilityOverrideStore : GameComponent
    {
        public static CompatibilityOverrideStore Instance;
        private Dictionary<(int, int), (float overrideVal, float vanillaVal)> overrides = new Dictionary<(int, int), (float overrideVal, float vanillaVal)>();
        private List<int> keysA = new List<int>();
        private List<int> keysB = new List<int>();
        private List<float> overrideValues = new List<float>();
        private List<float> vanillaValues = new List<float>();

        public CompatibilityOverrideStore(Game game) : base()
        {
            Instance = this;
        }

        private static (int, int) MakeKey(int idA, int idB)
        {
            return idA < idB ? (idA, idB) : (idB, idA);
        }

        public bool TryGetOverride(int idA, int idB, out float value)
        {
            if (overrides.TryGetValue(MakeKey(idA, idB), out var entry))
            {
                value = entry.overrideVal;
                return true;
            }
            value = 0f;
            return false;
        }
        public int Count => overrides.Count;

        public void ClearAll() => overrides.Clear();

        public bool TryGetVanilla(int idA, int idB, out float value)
        {
            if (overrides.TryGetValue(MakeKey(idA, idB), out var entry))
            {
                value = entry.vanillaVal;
                return true;
            }
            value = 0f;
            return false;
        }


        public void SetOverride(int idA, int idB, float overrideVal, float vanillaVal)
        {
            if (Mathf.Approximately(overrideVal, vanillaVal))
            {
                overrides.Remove(MakeKey(idA, idB));
            }
            else
            {
                overrides[MakeKey(idA, idB)] = (overrideVal, vanillaVal);
            }
        }

        public void RemoveOverride(int idA, int idB)
        {
            overrides.Remove(MakeKey(idA, idB));
        }

        public bool HasOverride(int idA, int idB)
        {
            return overrides.ContainsKey(MakeKey(idA, idB));
        }

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                keysA.Clear();
                keysB.Clear();
                overrideValues.Clear();
                vanillaValues.Clear();

                foreach (var kvp in overrides)
                {
                    keysA.Add(kvp.Key.Item1);
                    keysB.Add(kvp.Key.Item2);
                    overrideValues.Add(kvp.Value.overrideVal);
                    vanillaValues.Add(kvp.Value.vanillaVal);
                }
            }
            Scribe_Collections.Look(ref keysA, "keysA", LookMode.Value);
            Scribe_Collections.Look(ref keysB, "keysB", LookMode.Value);
            Scribe_Collections.Look(ref overrideValues, "overrideValues", LookMode.Value);
            Scribe_Collections.Look(ref vanillaValues, "vanillaValues", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                overrides.Clear();
                if (keysA != null && keysB != null && overrideValues != null && vanillaValues != null)
                {
                    for (int i = 0; i < keysA.Count; i++)
                    {
                        overrides[MakeKey(keysA[i], keysB[i])] = (overrideValues[i], vanillaValues[i]);
                    }
                }


            }
        }

        public string DebugDump()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"[CompatibilityEditor] {overrides.Count} override(s):");

            if (overrides.Count == 0) return sb.ToString();

            Dictionary<int, string> names = BuildNameIndex();

            foreach (KeyValuePair<(int, int), (float overrideVal, float vanillaVal)> kvp in overrides)
            {
                sb.AppendLine(
                    $"  {Describe(kvp.Key.Item1, names)} <-> {Describe(kvp.Key.Item2, names)}" +
                    $"  =  {kvp.Value.overrideVal:F2}   (vanilla {kvp.Value.vanillaVal:F2})");
            }

            return sb.ToString();
        }

        private static string Describe(int id, Dictionary<int, string> names)
        {
            return names.TryGetValue(id, out string name)
                ? $"{id} \"{name}\""
                : $"{id} \"?\"";
        }

        private static Dictionary<int, string> BuildNameIndex()
        {
            Dictionary<int, string> names = new Dictionary<int, string>();
            if (Current.ProgramState != ProgramState.Playing) return names;

            AddNames(names, PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead);
            AddNames(names, PawnsFinder.AllCaravansAndTravellingTransporters_AliveOrDead);

            return names;
        }

        private static void AddNames(Dictionary<int, string> names, List<Pawn> pawns)
        {
            foreach (Pawn p in pawns)
            {
                if (p == null) continue;
                names[p.thingIDNumber] = p.LabelShort;
            }
        }
    }
}
