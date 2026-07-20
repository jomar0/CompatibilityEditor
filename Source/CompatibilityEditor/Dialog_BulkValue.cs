using System;
using UnityEngine;
using Verse;

namespace CompatibilityEditor
{
    /// <summary>
    /// Constrains the second value relative to the first, so a "raise" action
    /// cannot be configured to lower and vice versa.
    /// </summary>
    internal enum BulkClamp
    {
        None,
        SecondAtLeast,
        SecondAtMost
    }

    /// <summary>
    /// Generic prompt for one or more float values, each with a slider and a
    /// matching text box. Used by the bulk actions in the compatibility editor.
    /// </summary>
    internal class Dialog_BulkValue : Window
    {
        private const float ROW_H = 34f;
        private const float LABEL_W = 90f;
        private const float TEXT_W = 60f;
        private const float GAP = 8f;

        private readonly string title;
        private readonly string[] labels;
        private readonly float[] values;
        private readonly string[] buffers;
        private readonly float min;
        private readonly float max;
        private readonly BulkClamp clamp;
        private readonly Action<float[]> onConfirm;

        public override Vector2 InitialSize =>
            new Vector2(440f, 100f + labels.Length * ROW_H + 40f);

        public Dialog_BulkValue(string title, string[] labels, float[] initial,
                                float min, float max, Action<float[]> onConfirm,
                                BulkClamp clamp = BulkClamp.None)
        {
            this.title = title;
            this.labels = labels;
            this.min = min;
            this.max = max;
            this.clamp = clamp;
            this.onConfirm = onConfirm;

            values = (float[])initial.Clone();
            buffers = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                buffers[i] = values[i].ToString("F2");

            ApplyClamp();

            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), title);
            Text.Font = GameFont.Small;

            float y = inRect.y + 40f;

            for (int i = 0; i < values.Length; i++)
            {
                Rect labelRect = new Rect(inRect.x, y, LABEL_W, ROW_H);
                Rect sliderRect = new Rect(
                    labelRect.xMax + GAP,
                    y + (ROW_H - 20f) / 2f,
                    inRect.width - LABEL_W - GAP - TEXT_W - GAP,
                    20f);
                Rect textRect = new Rect(sliderRect.xMax + GAP, y + 3f, TEXT_W, ROW_H - 6f);

                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(labelRect, labels[i]);
                Text.Anchor = TextAnchor.UpperLeft;

                float newSlider = Widgets.HorizontalSlider(sliderRect, values[i], min, max);
                if (!Mathf.Approximately(newSlider, values[i]))
                {
                    values[i] = Round2(newSlider);
                    buffers[i] = values[i].ToString("F2");
                }

                // Array elements are addressable, so the buffer can be passed
                // by ref directly — unlike dictionary values.
                float typed = values[i];
                Widgets.TextFieldNumeric(textRect, ref typed, ref buffers[i], min, max);
                if (!Mathf.Approximately(typed, values[i]))
                    values[i] = Round2(typed);

                y += ROW_H;
            }

            ApplyClamp();

            float btnY = inRect.yMax - 32f;
            float btnW = (inRect.width - 10f) / 2f;

            if (Widgets.ButtonText(new Rect(inRect.x, btnY, btnW, 30f), "CancelButton".Translate()))
                Close();

            if (Widgets.ButtonText(new Rect(inRect.x + btnW + 10f, btnY, btnW, 30f), "OK".Translate()))
            {
                onConfirm(values);
                Close();
            }
        }

        /// <summary>
        /// Pushes the target value to follow the threshold rather than blocking
        /// the threshold slider, which would feel jammed for no visible reason.
        /// </summary>
        private void ApplyClamp()
        {
            if (clamp == BulkClamp.None || values.Length < 2) return;

            if (clamp == BulkClamp.SecondAtLeast && values[1] < values[0])
                values[1] = values[0];
            else if (clamp == BulkClamp.SecondAtMost && values[1] > values[0])
                values[1] = values[0];
            else
                return;

            values[1] = Round2(values[1]);
            buffers[1] = values[1].ToString("F2");
        }

        private static float Round2(float v) => Mathf.Round(v * 100f) / 100f;
    }
}