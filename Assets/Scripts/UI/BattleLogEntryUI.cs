using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TR.Systems;

namespace TR.UI
{
    /// One row of the battle log.
    public class BattleLogEntryUI : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("Coloured bar down the side of the row, tinted by the outcome.")]
        [SerializeField] private Image resultStripe;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private TMP_Text modeText;
        [SerializeField] private TMP_Text arenaText;
        [SerializeField] private TMP_Text wavesText;
        [SerializeField] private TMP_Text trophyText;
        [SerializeField] private TMP_Text dateText;
        [Tooltip("Optional background image, tinted a faint version of the outcome colour.")]
        [SerializeField] private Image background;

        [Header("Outcome Colours")]
        [SerializeField] private Color victoryColor = new Color(0.30f, 0.80f, 0.38f);
        [SerializeField] private Color defeatColor = new Color(0.86f, 0.31f, 0.31f);
        [SerializeField] private Color abandonedColor = new Color(0.62f, 0.62f, 0.66f);
        [SerializeField] private Color trophyGainColor = new Color(1f, 0.80f, 0.20f);
        [SerializeField] private Color trophyLossColor = new Color(0.86f, 0.44f, 0.44f);
        [Range(0f, 1f)]
        [SerializeField] private float backgroundTintStrength = 0.12f;

        public void Bind(MatchRecord record)
        {
            if (record == null) return;

            Color tint = ColorFor(record.Outcome);
            if (resultStripe != null) resultStripe.color = tint;
            if (background != null)
            {
                var c = tint;
                c.a = backgroundTintStrength;
                background.color = c;
            }

            if (resultText != null)
            {
                resultText.text = LabelFor(record.Outcome);
                resultText.color = tint;
            }

            if (modeText != null)
            {
                // The partner is the only thing that distinguishes one duo run from another, so
                // show them by name rather than just saying "Duo".
                modeText.text = record.Mode == MatchMode.Duo
                    ? (string.IsNullOrEmpty(record.partnerName) ? "Duo" : "Duo with " + record.partnerName)
                    : "Single";
            }

            if (arenaText != null) arenaText.text = record.arenaName;

            if (wavesText != null)
            {
                wavesText.text = record.totalWaves > 0
                    ? $"Wave {record.wavesCleared}/{record.totalWaves}"
                    : $"Wave {record.wavesCleared}";
            }

            if (trophyText != null)
            {
                if (record.trophyDelta == 0)
                {
                    trophyText.text = "0";
                    trophyText.color = abandonedColor;
                }
                else
                {
                    trophyText.text = record.trophyDelta > 0 ? $"+{record.trophyDelta}" : record.trophyDelta.ToString();
                    trophyText.color = record.trophyDelta > 0 ? trophyGainColor : trophyLossColor;
                }
            }

            if (dateText != null) dateText.text = FormatWhen(record.LocalTime);
        }

        private Color ColorFor(MatchOutcome outcome)
        {
            switch (outcome)
            {
                case MatchOutcome.Victory: return victoryColor;
                case MatchOutcome.Defeat: return defeatColor;
                default: return abandonedColor;
            }
        }

        private static string LabelFor(MatchOutcome outcome)
        {
            switch (outcome)
            {
                case MatchOutcome.Victory: return "Victory";
                case MatchOutcome.Defeat: return "Defeat";
                default: return "Left";
            }
        }

        // Relative for anything recent, absolute once it stops being useful.
        private static string FormatWhen(System.DateTime when)
        {
            var span = System.DateTime.Now - when;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return when.ToString("d MMM");
        }
    }
}
