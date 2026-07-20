using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    /// <summary>
    /// A compact confirmation prompt. Vanilla's <see cref="Dialog_MessageBox"/>
    /// sizes itself for a paragraph of text, which strands the button row at the
    /// bottom of a mostly empty window for one-line questions.
    ///
    /// Buttons are drawn left to right in the order given. Closing the dialog
    /// without picking one (Escape, or the X) runs no action at all, which is
    /// the "cancel" case.
    /// </summary>
    internal class Dialog_ConfirmExit : Window
    {
        private const float BUTTON_H = 32f;
        private const float GAP = 10f;
        private const float PAD = 16f;

        internal struct ButtonSpec
        {
            public string label;
            public Action action;
            public bool destructive;

            public ButtonSpec(string label, Action action, bool destructive = false)
            {
                this.label = label;
                this.action = action;
                this.destructive = destructive;
            }
        }

        private readonly string text;
        private readonly List<ButtonSpec> buttons;
        private readonly float width;
        private readonly float height;

        public override Vector2 InitialSize => new Vector2(width, height);

        public Dialog_ConfirmExit(string text, List<ButtonSpec> buttons, float width = 460f, float height = 170f)
        {
            this.text = text;
            this.buttons = buttons;
            this.width = width;
            this.height = height;

            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;

            // Escape dismisses without running any action.
            closeOnCancel = true;
            closeOnAccept = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect textRect = new Rect(
                inRect.x,
                inRect.y,
                inRect.width,
                inRect.height - BUTTON_H - PAD);

            Widgets.Label(textRect, text);

            if (buttons.Count == 0) return;

            float totalGap = GAP * (buttons.Count - 1);
            float w = (inRect.width - totalGap) / buttons.Count;
            float y = inRect.yMax - BUTTON_H;

            for (int i = 0; i < buttons.Count; i++)
            {
                ButtonSpec b = buttons[i];
                Rect r = new Rect(inRect.x + (w + GAP) * i, y, w, BUTTON_H);

                Color prev = GUI.color;
                if (b.destructive) GUI.color = ColorLibrary.RedReadable;

                bool clicked = Widgets.ButtonText(r, b.label);

                GUI.color = prev;

                if (!clicked) continue;

                // Close first: the action may itself close a parent window, and
                // doing that while this dialog is still on the stack gets messy.
                Close();
                b.action?.Invoke();
                return;
            }
        }
    }
}