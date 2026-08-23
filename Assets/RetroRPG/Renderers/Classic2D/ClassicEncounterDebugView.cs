using System;
using RetroRPG.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace RetroRPG.Renderers.Classic2D
{
    /// <summary>Temporary classic overlay used until the battle presentation consumes encounters.</summary>
    public sealed class ClassicEncounterDebugView : MonoBehaviour, IEncounterDebugView
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text label;
        [SerializeField, Min(0.1f)] private float visibleSeconds = 2f;
        private float remainingSeconds;

        public string LastMessage => label == null ? string.Empty : label.text;

        public void Configure(CanvasGroup configuredCanvasGroup, Text configuredLabel)
        {
            canvasGroup = configuredCanvasGroup ?? throw new ArgumentNullException(nameof(configuredCanvasGroup));
            label = configuredLabel ?? throw new ArgumentNullException(nameof(configuredLabel));
            Hide();
        }

        public void Present(EncounterTrigger trigger)
        {
            if (canvasGroup == null || label == null) return;
            label.text = "Encounter: " + trigger.Selection.CreatureKey + "  Lv." + trigger.Selection.Level;
            canvasGroup.alpha = 1f;
            remainingSeconds = visibleSeconds;
        }

        private void Update()
        {
            if (remainingSeconds <= 0f) return;
            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds <= 0f) Hide();
        }

        private void Hide()
        {
            remainingSeconds = 0f;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (label != null) label.text = string.Empty;
        }
    }
}
