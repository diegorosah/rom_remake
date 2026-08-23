using System;
using RetroRPG.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace RetroRPG.Renderers.Classic2D
{
    /// <summary>Pixel-friendly uGUI presentation for the renderer-neutral dialogue session.</summary>
    public sealed class ClassicDialogueView : MonoBehaviour, IDialogueView
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text dialogueText;
        [SerializeField] private Text advancePrompt;

        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0f;
        public string VisibleText => dialogueText == null ? string.Empty : dialogueText.text;

        public void Configure(CanvasGroup configuredCanvasGroup, Text configuredDialogueText, Text configuredAdvancePrompt)
        {
            canvasGroup = configuredCanvasGroup ?? throw new ArgumentNullException(nameof(configuredCanvasGroup));
            dialogueText = configuredDialogueText ?? throw new ArgumentNullException(nameof(configuredDialogueText));
            advancePrompt = configuredAdvancePrompt;
            Hide();
        }

        public void Present(DialogueSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (canvasGroup == null || dialogueText == null) return;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            dialogueText.text = session.VisibleText;
            if (advancePrompt != null)
            {
                advancePrompt.enabled = session.State == DialogueSessionState.WaitingForAdvance;
                advancePrompt.text = "▼";
            }
        }

        public void Hide()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (dialogueText != null) dialogueText.text = string.Empty;
            if (advancePrompt != null) advancePrompt.enabled = false;
        }
    }
}
