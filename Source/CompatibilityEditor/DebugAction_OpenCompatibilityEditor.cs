using LudeonTK;
using Verse;

namespace CompatibilityEditor
{
    public static class DebugAction_OpenCompatibilityEditor
    {
        [DebugAction("Pawns", "Open Pawn Compatibility Editor", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        public static void OpenEditor()
        {
            Find.WindowStack.Add(new Dialog_CompatibilityEditor());
        }
    }
}