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
    public sealed class DialogueCatalog : MonoBehaviour
    {
        [SerializeField] private List<DialogueCatalogEntry> entries = new List<DialogueCatalogEntry>();
        private readonly Dictionary<string, DialogueDefinition> definitions = new Dictionary<string, DialogueDefinition>(StringComparer.Ordinal);

        public void Configure(IList<DialogueDefinition> configuredDefinitions)
        {
            if (configuredDefinitions == null)
            {
                throw new ArgumentNullException(nameof(configuredDefinitions));
            }

            definitions.Clear();
            entries = new List<DialogueCatalogEntry>(configuredDefinitions.Count);
            for (int index = 0; index < configuredDefinitions.Count; index++)
            {
                DialogueDefinition definition = configuredDefinitions[index] ?? throw new ArgumentException("Dialogue definitions cannot contain null.", nameof(configuredDefinitions));
                if (definitions.ContainsKey(definition.InteractionKey))
                {
                    throw new ArgumentException("Dialogue interaction keys must be unique.", nameof(configuredDefinitions));
                }

                definitions.Add(definition.InteractionKey, definition);
                var entry = new DialogueCatalogEntry();
                entry.Configure(definition);
                entries.Add(entry);
            }
        }

        public bool TryResolve(string interactionKey, out DialogueDefinition definition)
        {
            return !string.IsNullOrWhiteSpace(interactionKey) && definitions.TryGetValue(interactionKey, out definition);
        }

        private void Awake()
        {
            definitions.Clear();
            for (int index = 0; index < entries.Count; index++)
            {
                DialogueDefinition definition = entries[index].ToDefinition();
                if (definitions.ContainsKey(definition.InteractionKey))
                {
                    throw new InvalidOperationException("Serialized dialogue interaction keys must be unique.");
                }

                definitions.Add(definition.InteractionKey, definition);
            }
        }

        private void OnValidate()
        {
            if (entries == null)
            {
                entries = new List<DialogueCatalogEntry>();
            }
        }
    }

    /// <summary>Owns one dialogue session and safely gates player/NPC simulation while it is open.</summary>
    public sealed class DialogueController : MonoBehaviour
    {
        [SerializeField] private DialogueCatalog dialogueCatalog;
        [SerializeField] private PlayerController player;
        [SerializeField] private MonoBehaviour dialogueViewComponent;
        [SerializeField] private bool readSubmitInput = true;

        private IDialogueView view;
        private DialogueSession session;
        private NpcSimulationDriver suspendedDriver;
        private bool priorPlayerInputEnabled;
        private bool priorDriverSuspended;
        private int openedFrame = -1;

        public DialogueSession Session => session;
        public bool IsOpen => session != null && !session.IsClosed;

        public void Configure(DialogueCatalog configuredCatalog, PlayerController configuredPlayer, IDialogueView configuredView = null)
        {
            dialogueCatalog = configuredCatalog ?? throw new ArgumentNullException(nameof(configuredCatalog));
            player = configuredPlayer ?? throw new ArgumentNullException(nameof(configuredPlayer));
            view = configuredView;
            dialogueViewComponent = configuredView as MonoBehaviour;
        }

        public void SetView(IDialogueView configuredView)
        {
            view = configuredView;
            if (IsOpen)
            {
                view?.Present(session);
            }
        }

        public void SetViewComponent(MonoBehaviour configuredViewComponent)
        {
            if (configuredViewComponent != null && !(configuredViewComponent is IDialogueView))
            {
                throw new ArgumentException("Dialogue view component must implement IDialogueView.", nameof(configuredViewComponent));
            }

            dialogueViewComponent = configuredViewComponent;
            SetView(configuredViewComponent as IDialogueView);
        }

        public bool TryOpen(string interactionKey, MapRuntimeRoot activeMap)
        {
            if (IsOpen || dialogueCatalog == null || player == null || activeMap == null || !activeMap.IsRuntimeActive ||
                !dialogueCatalog.TryResolve(interactionKey, out DialogueDefinition definition))
            {
                return false;
            }

            session = new DialogueSession(definition);
            openedFrame = Time.frameCount;
            priorPlayerInputEnabled = player.InputEnabled;
            player.CancelPendingMove();
            player.InputEnabled = false;
            suspendedDriver = activeMap.NpcSimulationDriver;
            if (suspendedDriver != null)
            {
                priorDriverSuspended = suspendedDriver.IsSuspended;
                suspendedDriver.SetSuspended(true);
            }

            view?.Present(session);
            return true;
        }

        public void Advance(float deltaSeconds)
        {
            if (!IsOpen)
            {
                return;
            }

            session.Advance(deltaSeconds);
            view?.Present(session);
        }

        public DialogueAdvanceResult AdvanceOrClose()
        {
            if (!IsOpen)
            {
                return DialogueAdvanceResult.Ignored;
            }

            DialogueAdvanceResult result = session.AdvanceOrComplete();
            if (result == DialogueAdvanceResult.Closed)
            {
                FinishSession();
            }
            else
            {
                view?.Present(session);
            }

            return result;
        }

        public void Close()
        {
            if (session != null && !session.IsClosed)
            {
                session.Close();
            }

            FinishSession();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            Advance(Time.deltaTime);
            if (readSubmitInput && Time.frameCount != openedFrame && IsSubmitPressed())
            {
                AdvanceOrClose();
            }
        }

        private void OnDestroy()
        {
            Close();
        }

        private void OnDisable()
        {
            Close();
        }

        private void FinishSession()
        {
            if (session == null)
            {
                return;
            }

            view?.Hide();
            if (player != null)
            {
                player.InputEnabled = priorPlayerInputEnabled;
            }

            if (suspendedDriver != null)
            {
                suspendedDriver.SetSuspended(priorDriverSuspended);
            }

            suspendedDriver = null;
            session = null;
            openedFrame = -1;
        }

        private static bool IsSubmitPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.zKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        }

        private void Awake()
        {
            view = dialogueViewComponent as IDialogueView;
        }
    }
}
