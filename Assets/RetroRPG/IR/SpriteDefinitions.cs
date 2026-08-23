using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RetroRPG.IR
{
    /// <summary>Cardinal facing used by a game-agnostic overworld sprite.</summary>
    public enum SpriteDirection
    {
        South,
        North,
        West,
        East
    }

    public enum SpriteAnimationState
    {
        Idle,
        Walking
    }

    /// <summary>An immutable indexed-colour sprite frame. Pixels use the 4bpp range 0..15.</summary>
    [Serializable]
    public sealed class IndexedSpriteFrameDefinition
    {
        private readonly ReadOnlyCollection<byte> pixels;

        public IndexedSpriteFrameDefinition(int index, int width, int height, byte[] pixels)
        {
            if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));

            var pixelCount = checked(width * height);
            if (pixels.Length != pixelCount)
            {
                throw new ArgumentException("Sprite frame pixels must equal width multiplied by height.", nameof(pixels));
            }

            var copiedPixels = new List<byte>(pixels.Length);
            for (var i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] > 15)
                {
                    throw new ArgumentException("A 4bpp sprite pixel must be in the range 0..15.", nameof(pixels));
                }

                copiedPixels.Add(pixels[i]);
            }

            Index = index;
            Width = width;
            Height = height;
            this.pixels = new ReadOnlyCollection<byte>(copiedPixels);
        }

        public int Index { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<byte> Pixels => pixels;
    }

    [Serializable]
    public struct SpriteAnimationStepDefinition
    {
        public SpriteAnimationStepDefinition(int frameIndex, bool horizontalFlip, bool verticalFlip, int durationTicks)
        {
            if (frameIndex < 0) throw new ArgumentOutOfRangeException(nameof(frameIndex));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));

            FrameIndex = frameIndex;
            HorizontalFlip = horizontalFlip;
            VerticalFlip = verticalFlip;
            DurationTicks = durationTicks;
        }

        public int FrameIndex { get; }
        public bool HorizontalFlip { get; }
        public bool VerticalFlip { get; }
        public int DurationTicks { get; }
    }

    [Serializable]
    public sealed class DirectionalSpriteAnimationDefinition
    {
        public DirectionalSpriteAnimationDefinition(
            SpriteDirection direction,
            SpriteAnimationState state,
            IList<SpriteAnimationStepDefinition> steps)
        {
            if (!IsCardinalDirection(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
            if (!IsKnownState(state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (steps == null || steps.Count == 0)
            {
                throw new ArgumentException("A sprite animation needs at least one step.", nameof(steps));
            }

            Direction = direction;
            State = state;
            Steps = new ReadOnlyCollection<SpriteAnimationStepDefinition>(new List<SpriteAnimationStepDefinition>(steps));
        }

        public SpriteDirection Direction { get; }
        public SpriteAnimationState State { get; }
        public IReadOnlyList<SpriteAnimationStepDefinition> Steps { get; }

        internal static bool IsCardinalDirection(SpriteDirection direction)
        {
            return direction == SpriteDirection.South
                || direction == SpriteDirection.North
                || direction == SpriteDirection.West
                || direction == SpriteDirection.East;
        }

        internal static bool IsKnownState(SpriteAnimationState state)
        {
            return state == SpriteAnimationState.Idle || state == SpriteAnimationState.Walking;
        }
    }

    /// <summary>
    /// Immutable 4bpp overworld sprite contract. It intentionally contains no engine or ROM types.
    /// </summary>
    [Serializable]
    public sealed class OverworldSpriteDefinition
    {
        public const int PaletteColorCount = 16;
        public const int RequiredAnimationCount = 8;

        public OverworldSpriteDefinition(
            string id,
            int width,
            int height,
            IList<Rgba32> palette,
            IList<IndexedSpriteFrameDefinition> frames,
            IList<DirectionalSpriteAnimationDefinition> animations)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A sprite id is required.", nameof(id));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (palette == null || palette.Count != PaletteColorCount)
            {
                throw new ArgumentException("An overworld sprite palette has exactly 16 colours.", nameof(palette));
            }

            if (frames == null || frames.Count == 0)
            {
                throw new ArgumentException("An overworld sprite needs at least one frame.", nameof(frames));
            }

            if (animations == null || animations.Count != RequiredAnimationCount)
            {
                throw new ArgumentException("An overworld sprite has exactly eight directional animations.", nameof(animations));
            }

            var frameIndices = new HashSet<int>();
            var copiedFrames = new List<IndexedSpriteFrameDefinition>(frames.Count);
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames[i] ?? throw new ArgumentException("Sprite frames cannot contain null.", nameof(frames));
                if (frame.Width != width || frame.Height != height)
                {
                    throw new ArgumentException("Every sprite frame must match the sprite dimensions.", nameof(frames));
                }

                if (!frameIndices.Add(frame.Index))
                {
                    throw new ArgumentException("Sprite frame indexes must be unique.", nameof(frames));
                }

                copiedFrames.Add(frame);
            }

            var animationKeys = new HashSet<string>(StringComparer.Ordinal);
            var copiedAnimations = new List<DirectionalSpriteAnimationDefinition>(animations.Count);
            for (var i = 0; i < animations.Count; i++)
            {
                var animation = animations[i] ?? throw new ArgumentException("Sprite animations cannot contain null.", nameof(animations));
                var key = ((int)animation.Direction).ToString() + ":" + ((int)animation.State).ToString();
                if (!animationKeys.Add(key))
                {
                    throw new ArgumentException("There must be exactly one animation for each direction and state.", nameof(animations));
                }

                for (var step = 0; step < animation.Steps.Count; step++)
                {
                    if (!frameIndices.Contains(animation.Steps[step].FrameIndex))
                    {
                        throw new ArgumentException("Sprite animation steps must reference an existing frame.", nameof(animations));
                    }
                }

                copiedAnimations.Add(animation);
            }

            for (var directionValue = (int)SpriteDirection.South; directionValue <= (int)SpriteDirection.East; directionValue++)
            {
                var direction = (SpriteDirection)directionValue;
                for (var stateValue = (int)SpriteAnimationState.Idle; stateValue <= (int)SpriteAnimationState.Walking; stateValue++)
                {
                    var state = (SpriteAnimationState)stateValue;
                    var key = ((int)direction).ToString() + ":" + ((int)state).ToString();
                    if (!animationKeys.Contains(key))
                    {
                        throw new ArgumentException("Every cardinal direction must have one idle and one walking animation.", nameof(animations));
                    }
                }
            }

            Id = id;
            Width = width;
            Height = height;
            Palette = new ReadOnlyCollection<Rgba32>(new List<Rgba32>(palette));
            Frames = new ReadOnlyCollection<IndexedSpriteFrameDefinition>(copiedFrames);
            Animations = new ReadOnlyCollection<DirectionalSpriteAnimationDefinition>(copiedAnimations);
        }

        public string Id { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<Rgba32> Palette { get; }
        public IReadOnlyList<IndexedSpriteFrameDefinition> Frames { get; }
        public IReadOnlyList<DirectionalSpriteAnimationDefinition> Animations { get; }
    }
}
