using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.IR
{
    public enum DialogueTokenKind
    {
        Glyph,
        Newline,
        PromptScroll,
        PromptClear,
        Placeholder,
        ExtendedControl
    }

    /// <summary>Decoded semantic text token. It never contains source-ROM bytes.</summary>
    [Serializable]
    public sealed class DialogueToken
    {
        public DialogueToken(DialogueTokenKind kind, string value = null, int[] parameters = null)
        {
            if (kind != DialogueTokenKind.Glyph && kind != DialogueTokenKind.Placeholder && kind != DialogueTokenKind.ExtendedControl && !string.IsNullOrEmpty(value)) throw new ArgumentException("Only value-bearing dialogue tokens may have text.", nameof(value));
            if ((kind == DialogueTokenKind.Glyph || kind == DialogueTokenKind.Placeholder || kind == DialogueTokenKind.ExtendedControl) && string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A semantic dialogue token value is required.", nameof(value));
            Kind = kind; Value = value; Parameters = new ReadOnlyCollection<int>(new List<int>(parameters ?? new int[0]));
        }

        public DialogueTokenKind Kind { get; }
        public string Value { get; }
        public IReadOnlyList<int> Parameters { get; }
    }

    [Serializable]
    public sealed class DialoguePageDefinition
    {
        public DialoguePageDefinition(IList<DialogueToken> tokens)
        {
            if (tokens == null || tokens.Count == 0) throw new ArgumentException("A dialogue page needs tokens.", nameof(tokens));
            for (var i = 0; i < tokens.Count; i++) if (tokens[i] == null) throw new ArgumentException("Dialogue tokens cannot contain null.", nameof(tokens));
            Tokens = new ReadOnlyCollection<DialogueToken>(new List<DialogueToken>(tokens));
        }

        public IReadOnlyList<DialogueToken> Tokens { get; }
    }

    public enum DialoguePresentation
    {
        Npc,
        Neutral
    }

    [Serializable]
    public sealed class DialogueDefinition
    {
        public DialogueDefinition(string id, string targetEventId, DialoguePresentation presentation, bool facePlayer, IList<DialoguePageDefinition> pages)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(targetEventId)) throw new ArgumentException("Dialogue and target ids are required.");
            if (presentation != DialoguePresentation.Npc && presentation != DialoguePresentation.Neutral) throw new ArgumentOutOfRangeException(nameof(presentation));
            if (pages == null || pages.Count == 0) throw new ArgumentException("A dialogue needs pages.", nameof(pages));
            for (var i = 0; i < pages.Count; i++) if (pages[i] == null) throw new ArgumentException("Dialogue pages cannot contain null.", nameof(pages));
            Id = id; TargetEventId = targetEventId; Presentation = presentation; FacePlayer = facePlayer; Pages = new ReadOnlyCollection<DialoguePageDefinition>(new List<DialoguePageDefinition>(pages));
        }

        public string Id { get; } public string TargetEventId { get; } public DialoguePresentation Presentation { get; } public bool FacePlayer { get; } public IReadOnlyList<DialoguePageDefinition> Pages { get; }
    }

    [Serializable]
    public sealed class DialogueCatalogDefinition
    {
        private readonly Dictionary<string, DialogueDefinition> byTargetEventId;

        public DialogueCatalogDefinition(IList<DialogueDefinition> dialogues)
        {
            if (dialogues == null) throw new ArgumentNullException(nameof(dialogues));
            var copied = new List<DialogueDefinition>(dialogues.Count); byTargetEventId = new Dictionary<string, DialogueDefinition>(StringComparer.Ordinal);
            for (var i = 0; i < dialogues.Count; i++)
            {
                var dialogue = dialogues[i] ?? throw new ArgumentException("Dialogues cannot contain null.", nameof(dialogues));
                if (byTargetEventId.ContainsKey(dialogue.TargetEventId)) throw new ArgumentException("Each target has at most one declared dialogue.", nameof(dialogues));
                byTargetEventId.Add(dialogue.TargetEventId, dialogue); copied.Add(dialogue);
            }

            copied.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id)); Dialogues = new ReadOnlyCollection<DialogueDefinition>(copied);
        }

        public IReadOnlyList<DialogueDefinition> Dialogues { get; }
        public bool TryGetForTarget(string targetEventId, out DialogueDefinition dialogue) { return byTargetEventId.TryGetValue(targetEventId, out dialogue); }
    }
}
