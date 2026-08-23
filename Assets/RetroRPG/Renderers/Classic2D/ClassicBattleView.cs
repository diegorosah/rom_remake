using System;
using System.Collections.Generic;
using RetroRPG.Core;
using RetroRPG.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RetroRPG.Renderers.Classic2D
{
    [Serializable]
    public sealed class ClassicBattleSpriteEntry
    {
        [SerializeField] private string creatureKey;
        [SerializeField] private Sprite front;
        [SerializeField] private Sprite back;

        public string CreatureKey => creatureKey;
        public Sprite Front => front;
        public Sprite Back => back;

        public void Configure(string configuredCreatureKey, Sprite configuredFront, Sprite configuredBack)
        {
            if (string.IsNullOrWhiteSpace(configuredCreatureKey)) throw new ArgumentException("Creature key is required.", nameof(configuredCreatureKey));
            creatureKey = configuredCreatureKey;
            front = configuredFront ?? throw new ArgumentNullException(nameof(configuredFront));
            back = configuredBack ?? throw new ArgumentNullException(nameof(configuredBack));
        }
    }

    /// <summary>Minimal selectable battle presentation for the classic renderer.</summary>
    public sealed class ClassicBattleView : MonoBehaviour, IBattleView
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text statusText;
        [SerializeField] private Text actionLabel;
        [SerializeField] private Button primaryAction;
        [SerializeField] private BattleCoordinator coordinator;
        [SerializeField] private Image playerImage;
        [SerializeField] private Image opponentImage;
        [SerializeField] private List<ClassicBattleSpriteEntry> spriteEntries = new List<ClassicBattleSpriteEntry>();
        private readonly Dictionary<string, ClassicBattleSpriteEntry> sprites = new Dictionary<string, ClassicBattleSpriteEntry>(StringComparer.Ordinal);

        public bool IsVisible => canvasGroup != null && canvasGroup.alpha > 0f;

        public void Configure(
            CanvasGroup configuredCanvasGroup,
            Text configuredStatusText,
            Text configuredActionLabel,
            Button configuredPrimaryAction,
            BattleCoordinator configuredCoordinator,
            Image configuredPlayerImage,
            Image configuredOpponentImage,
            IList<ClassicBattleSpriteEntry> configuredSprites)
        {
            canvasGroup = configuredCanvasGroup ?? throw new ArgumentNullException(nameof(configuredCanvasGroup));
            statusText = configuredStatusText ?? throw new ArgumentNullException(nameof(configuredStatusText));
            actionLabel = configuredActionLabel ?? throw new ArgumentNullException(nameof(configuredActionLabel));
            primaryAction = configuredPrimaryAction ?? throw new ArgumentNullException(nameof(configuredPrimaryAction));
            coordinator = configuredCoordinator ?? throw new ArgumentNullException(nameof(configuredCoordinator));
            playerImage = configuredPlayerImage ?? throw new ArgumentNullException(nameof(configuredPlayerImage));
            opponentImage = configuredOpponentImage ?? throw new ArgumentNullException(nameof(configuredOpponentImage));
            spriteEntries = configuredSprites == null ? new List<ClassicBattleSpriteEntry>() : new List<ClassicBattleSpriteEntry>(configuredSprites);
            RebuildSprites();
            BindButton();
            SetVisible(false);
        }

        private void Awake()
        {
            RebuildSprites();
            BindButton();
        }

        private void OnEnable()
        {
            BindButton();
        }

        private void OnDestroy()
        {
            if (primaryAction != null) primaryAction.onClick.RemoveListener(HandlePrimaryAction);
        }

        public void PresentBattle(BattleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            SetVisible(true);
            if (!sprites.TryGetValue(state.Player.Spec.Key, out var playerSprite) || !sprites.TryGetValue(state.Opponent.Spec.Key, out var opponentSprite))
            {
                throw new InvalidOperationException("Battle presentation is missing a creature sprite.");
            }
            playerImage.sprite = playerSprite.Back;
            opponentImage.sprite = opponentSprite.Front;
            actionLabel.text = "Attack";
            primaryAction.interactable = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primaryAction.gameObject);
            RenderState(state, "A wild creature appeared!");
        }

        public void PresentTurn(BattleTurnResult result, BattleState state)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (state == null) throw new ArgumentNullException(nameof(state));
            var message = result.FirstAction.HasValue
                ? FormatAction(result.FirstAction.Value)
                : "No action was resolved.";
            if (result.SecondAction.HasValue) message += "\n" + FormatAction(result.SecondAction.Value);
            RenderState(state, message);
        }

        public void PresentOutcome(BattleOutcome outcome, BattleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            statusText.text += "\n\n" + (outcome == BattleOutcome.PlayerWon ? "Victory!" : "Defeat.");
            actionLabel.text = "Return to map";
            primaryAction.interactable = true;
        }

        public void HideBattle()
        {
            SetVisible(false);
        }

        private void HandlePrimaryAction()
        {
            if (coordinator == null) return;
            if (coordinator.IsAwaitingReturn) coordinator.ReturnToMap();
            else coordinator.TrySubmitPrimaryAttack();
        }

        private void BindButton()
        {
            if (primaryAction == null) return;
            primaryAction.onClick.RemoveListener(HandlePrimaryAction);
            primaryAction.onClick.AddListener(HandlePrimaryAction);
        }

        private void RebuildSprites()
        {
            sprites.Clear();
            if (spriteEntries == null) return;
            for (var index = 0; index < spriteEntries.Count; index++)
            {
                var entry = spriteEntries[index];
                if (entry == null || string.IsNullOrWhiteSpace(entry.CreatureKey) || entry.Front == null || entry.Back == null)
                {
                    throw new InvalidOperationException("Battle sprite entries must be complete.");
                }
                if (sprites.ContainsKey(entry.CreatureKey)) throw new InvalidOperationException("Battle sprite creature keys must be unique.");
                sprites.Add(entry.CreatureKey, entry);
            }
        }

        private void RenderState(BattleState state, string message)
        {
            statusText.text = message + "\n\n" +
                state.Player.Spec.Key + "  Lv." + state.Player.Level + "  HP " + state.Player.CurrentHitPoints + "/" + state.Player.Stats.HitPoints + "\n" +
                state.Opponent.Spec.Key + "  Lv." + state.Opponent.Level + "  HP " + state.Opponent.CurrentHitPoints + "/" + state.Opponent.Stats.HitPoints;
        }

        private static string FormatAction(BattleActionResult action)
        {
            return (action.PlayerActed ? "Player" : "Opponent") + " used " + action.SkillKey + " for " + action.Damage + " damage.";
        }

        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }
    }
}
