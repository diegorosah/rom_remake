using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroRPG.Runtime
{
    /// <summary>Declarative dialogue content supplied by an importer or an editor-authored preview.</summary>
    public sealed class DialogueDefinition
    {
        private readonly string[] pages;

        public DialogueDefinition(string interactionKey, IList<string> configuredPages, float charactersPerSecond = 30f, bool faceTarget = true)
        {
            if (string.IsNullOrWhiteSpace(interactionKey))
            {
                throw new ArgumentException("An interaction key is required.", nameof(interactionKey));
            }

            if (configuredPages == null || configuredPages.Count == 0)
            {
                throw new ArgumentException("At least one dialogue page is required.", nameof(configuredPages));
            }

            if (charactersPerSecond <= 0f || float.IsNaN(charactersPerSecond) || float.IsInfinity(charactersPerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(charactersPerSecond));
            }

            pages = new string[configuredPages.Count];
            for (int index = 0; index < configuredPages.Count; index++)
            {
                pages[index] = configuredPages[index] ?? string.Empty;
            }

            InteractionKey = interactionKey;
            CharactersPerSecond = charactersPerSecond;
            FaceTarget = faceTarget;
        }

        public string InteractionKey { get; }
        public float CharactersPerSecond { get; }
        public bool FaceTarget { get; }
        public int PageCount => pages.Length;
        public string GetPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= pages.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(pageIndex));
            }

            return pages[pageIndex];
        }
    }

    public enum DialogueSessionState
    {
        Typing = 0,
        WaitingForAdvance = 1,
        Closed = 2,
    }

    public enum DialogueAdvanceResult
    {
        CompletedPage = 0,
        AdvancedPage = 1,
        Closed = 2,
        Ignored = 3,
    }

    /// <summary>Pure dialogue progression state; it has no dependency on a Unity UI implementation.</summary>
    public sealed class DialogueSession
    {
        private readonly DialogueDefinition definition;
        private float pendingCharacters;
        private int pageIndex;
        private int visibleCharacterCount;
        private DialogueSessionState state;

        public DialogueSession(DialogueDefinition configuredDefinition)
        {
            definition = configuredDefinition ?? throw new ArgumentNullException(nameof(configuredDefinition));
            state = GetCurrentPage().Length == 0 ? DialogueSessionState.WaitingForAdvance : DialogueSessionState.Typing;
        }

        public DialogueDefinition Definition => definition;
        public int PageIndex => pageIndex;
        public int VisibleCharacterCount => visibleCharacterCount;
        public DialogueSessionState State => state;
        public bool IsClosed => state == DialogueSessionState.Closed;
        public string FullPageText => state == DialogueSessionState.Closed ? string.Empty : GetCurrentPage();
        public string VisibleText => state == DialogueSessionState.Closed
            ? string.Empty
            : GetCurrentPage().Substring(0, Mathf.Clamp(visibleCharacterCount, 0, GetCurrentPage().Length));

        public void Advance(float deltaSeconds)
        {
            if (state != DialogueSessionState.Typing || deltaSeconds <= 0f)
            {
                return;
            }

            pendingCharacters += deltaSeconds * definition.CharactersPerSecond;
            int wholeCharacters = Mathf.FloorToInt(pendingCharacters);
            if (wholeCharacters <= 0)
            {
                return;
            }

            pendingCharacters -= wholeCharacters;
            visibleCharacterCount = Mathf.Min(GetCurrentPage().Length, visibleCharacterCount + wholeCharacters);
            if (visibleCharacterCount >= GetCurrentPage().Length)
            {
                state = DialogueSessionState.WaitingForAdvance;
            }
        }

        public DialogueAdvanceResult AdvanceOrComplete()
        {
            if (state == DialogueSessionState.Closed)
            {
                return DialogueAdvanceResult.Ignored;
            }

            if (state == DialogueSessionState.Typing)
            {
                visibleCharacterCount = GetCurrentPage().Length;
                pendingCharacters = 0f;
                state = DialogueSessionState.WaitingForAdvance;
                return DialogueAdvanceResult.CompletedPage;
            }

            if (pageIndex + 1 < definition.PageCount)
            {
                pageIndex++;
                visibleCharacterCount = 0;
                pendingCharacters = 0f;
                state = GetCurrentPage().Length == 0 ? DialogueSessionState.WaitingForAdvance : DialogueSessionState.Typing;
                return DialogueAdvanceResult.AdvancedPage;
            }

            state = DialogueSessionState.Closed;
            return DialogueAdvanceResult.Closed;
        }

        public void Close()
        {
            state = DialogueSessionState.Closed;
            pendingCharacters = 0f;
        }

        private string GetCurrentPage()
        {
            return definition.GetPage(pageIndex);
        }
    }

    /// <summary>Presentation gateway: a concrete uGUI/UITK/pixel view can implement this without changing dialogue rules.</summary>
    public interface IDialogueView
    {
        void Present(DialogueSession session);
        void Hide();
    }

    [Serializable]
    public sealed class DialogueCatalogEntry
    {
        [SerializeField] private string interactionKey;
        [TextArea, SerializeField] private string[] pages = Array.Empty<string>();
        [SerializeField, Min(0.01f)] private float charactersPerSecond = 30f;
        [SerializeField] private bool faceTarget = true;

        public string InteractionKey => interactionKey;

        public void Configure(DialogueDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            interactionKey = definition.InteractionKey;
            charactersPerSecond = definition.CharactersPerSecond;
            faceTarget = definition.FaceTarget;
            pages = new string[definition.PageCount];
            for (int index = 0; index < pages.Length; index++)
            {
                pages[index] = definition.GetPage(index);
            }
        }

        public DialogueDefinition ToDefinition()
        {
            return new DialogueDefinition(interactionKey, pages, charactersPerSecond, faceTarget);
        }
    }

    /// <summary>Keyed dialogue lookup which remains independent of map, NPC, and view implementations.</summary>

    /// <summary>Owns one dialogue session and safely gates player/NPC simulation while it is open.</summary>
}
