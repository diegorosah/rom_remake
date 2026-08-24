using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroRPG.Runtime
{
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
        private string lastFailure;

        public DialogueSession Session => session;
        public bool IsOpen => session != null && !session.IsClosed;
        public string LastFailure => lastFailure;

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
            lastFailure = null;
            if (IsOpen)
            {
                lastFailure = "A dialogue session is already open.";
                return false;
            }
            if (dialogueCatalog == null)
            {
                lastFailure = "DialogueCatalog is not configured.";
                return false;
            }
            if (player == null)
            {
                lastFailure = "PlayerController is not configured.";
                return false;
            }
            if (activeMap == null || !activeMap.IsRuntimeActive)
            {
                lastFailure = "The active map is missing or inactive.";
                return false;
            }
            if (!dialogueCatalog.TryResolve(interactionKey, out DialogueDefinition definition))
            {
                lastFailure = "Dialogue key is not present in the runtime catalog: " + interactionKey + ".";
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
            lastFailure = null;
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
