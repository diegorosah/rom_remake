using System;
using UnityEngine;

namespace RetroRPG.Runtime
{
    /// <summary>
    /// One cardinal animation sequence. Frame duration is measured in the fixed
    /// 60 Hz animation clock, rather than in Unity animation-controller time.
    /// </summary>
    [Serializable]
    public sealed class DirectionalSpriteSequence
    {
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [SerializeField, Min(1)] private int ticksPerFrame = 8;

        public DirectionalSpriteSequence(Sprite[] sequenceFrames, int sequenceTicksPerFrame)
        {
            if (sequenceFrames == null)
            {
                throw new ArgumentNullException(nameof(sequenceFrames));
            }

            if (sequenceTicksPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequenceTicksPerFrame));
            }

            frames = (Sprite[])sequenceFrames.Clone();
            ticksPerFrame = sequenceTicksPerFrame;
        }

        public int FrameCount => frames == null ? 0 : frames.Length;
        public int TicksPerFrame => Mathf.Max(1, ticksPerFrame);
        public bool HasFrames => FrameCount > 0;

        /// <summary>Returns a single frame without exposing the serialized array.</summary>
        public Sprite GetFrame(int index)
        {
            if (frames == null || index < 0 || index >= frames.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return frames[index];
        }
    }

    /// <summary>
    /// Code-driven directional sprite animation. No AnimatorController is used or
    /// required; callers set a facing/movement state and advance a deterministic
    /// 60 Hz clock.
    /// </summary>
    public sealed class DirectionalSpriteAnimator : MonoBehaviour
    {
        public const int TickRate = 60;

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private DirectionalSpriteSequence[] idleSequences = new DirectionalSpriteSequence[4];
        [SerializeField] private DirectionalSpriteSequence[] walkSequences = new DirectionalSpriteSequence[4];
        [SerializeField] private GridDirection facing = GridDirection.Down;
        [SerializeField] private bool walking;

        private float pendingTicks;
        private int elapsedTicks;
        private int currentFrameIndex;

        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public GridDirection Facing => facing;
        public bool IsWalking => walking;
        public Sprite CurrentSprite => spriteRenderer == null ? null : spriteRenderer.sprite;
        public int CurrentFrameIndex => currentFrameIndex;
        public int ElapsedTicks => elapsedTicks;

        public void Configure(
            SpriteRenderer configuredRenderer,
            DirectionalSpriteSequence[] configuredIdleSequences,
            DirectionalSpriteSequence[] configuredWalkSequences)
        {
            if (configuredRenderer == null)
            {
                throw new ArgumentNullException(nameof(configuredRenderer));
            }

            spriteRenderer = configuredRenderer;
            idleSequences = CopyDirectionalSequences(configuredIdleSequences, nameof(configuredIdleSequences));
            walkSequences = CopyDirectionalSequences(configuredWalkSequences, nameof(configuredWalkSequences));
            ResetAnimationClock();
            ApplySprite();
        }

        public void SetSequence(GridDirection direction, bool isWalking, DirectionalSpriteSequence sequence)
        {
            int index = DirectionToIndex(direction);
            DirectionalSpriteSequence[] sequences = isWalking ? walkSequences : idleSequences;
            if (sequences == null || sequences.Length != 4)
            {
                sequences = new DirectionalSpriteSequence[4];
                if (isWalking)
                {
                    walkSequences = sequences;
                }
                else
                {
                    idleSequences = sequences;
                }
            }

            sequences[index] = sequence;
            if (direction == facing && isWalking == walking)
            {
                ResetAnimationClock();
                ApplySprite();
            }
        }

        public void SetState(GridDirection direction, bool isWalking)
        {
            if (!GridDirections.IsCardinal(direction))
            {
                return;
            }

            if (facing == direction && walking == isWalking)
            {
                return;
            }

            facing = direction;
            walking = isWalking;
            ResetAnimationClock();
            ApplySprite();
        }

        public void Tick()
        {
            AdvanceTicks(1);
        }

        public void Advance(float seconds)
        {
            if (seconds <= 0f)
            {
                return;
            }

            pendingTicks += seconds * TickRate;
            int wholeTicks = Mathf.FloorToInt(pendingTicks);
            if (wholeTicks <= 0)
            {
                return;
            }

            pendingTicks -= wholeTicks;
            AdvanceTicks(wholeTicks);
        }

        private void Awake()
        {
            ApplySprite();
        }

        private void AdvanceTicks(int tickCount)
        {
            DirectionalSpriteSequence sequence = GetActiveSequence();
            if (tickCount <= 0 || sequence == null || !sequence.HasFrames)
            {
                return;
            }

            elapsedTicks += tickCount;
            currentFrameIndex = (elapsedTicks / sequence.TicksPerFrame) % sequence.FrameCount;
            ApplySprite();
        }

        private void ResetAnimationClock()
        {
            pendingTicks = 0f;
            elapsedTicks = 0;
            currentFrameIndex = 0;
        }

        private void ApplySprite()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            DirectionalSpriteSequence sequence = GetActiveSequence();
            spriteRenderer.sprite = sequence != null && sequence.HasFrames
                ? sequence.GetFrame(Mathf.Clamp(currentFrameIndex, 0, sequence.FrameCount - 1))
                : null;
        }

        private DirectionalSpriteSequence GetActiveSequence()
        {
            DirectionalSpriteSequence[] sequences = walking ? walkSequences : idleSequences;
            int index = DirectionToIndex(facing);
            return sequences != null && sequences.Length == 4 ? sequences[index] : null;
        }

        private static DirectionalSpriteSequence[] CopyDirectionalSequences(
            DirectionalSpriteSequence[] source,
            string parameterName)
        {
            if (source == null || source.Length != 4)
            {
                throw new ArgumentException("Exactly four cardinal sequences are required in Down, Up, Left, Right order.", parameterName);
            }

            return (DirectionalSpriteSequence[])source.Clone();
        }

        private static int DirectionToIndex(GridDirection direction)
        {
            switch (direction)
            {
                case GridDirection.Down:
                    return 0;
                case GridDirection.Up:
                    return 1;
                case GridDirection.Left:
                    return 2;
                case GridDirection.Right:
                    return 3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "A cardinal direction is required.");
            }
        }
    }
}
