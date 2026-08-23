using System;
using System.Collections.Generic;
using NUnit.Framework;
using RetroRPG.IR;

namespace RetroRPG.Tests.EditMode
{
    public sealed class SpriteDefinitionTests
    {
        [Test]
        public void OverworldSprite_CopiesCollectionsAndPreservesArbitraryFrameIds()
        {
            var pixels = new byte[16 * 32];
            pixels[0] = 15;
            var frames = new List<IndexedSpriteFrameDefinition>
            {
                new IndexedSpriteFrameDefinition(2, 16, 32, pixels),
                new IndexedSpriteFrameDefinition(7, 16, 32, pixels),
                new IndexedSpriteFrameDefinition(11, 16, 32, pixels),
            };
            var palette = CreatePalette();
            var animations = CreateAnimations();
            var sprite = new OverworldSpriteDefinition("synthetic", 16, 32, palette, frames, animations);

            pixels[0] = 0;
            frames.Clear();
            animations.Clear();
            palette.Clear();

            Assert.That(sprite.Frames, Has.Count.EqualTo(3));
            Assert.That(sprite.Frames[0].Index, Is.EqualTo(2));
            Assert.That(sprite.Frames[0].Pixels[0], Is.EqualTo(15));
            Assert.That(sprite.Palette, Has.Count.EqualTo(16));
            Assert.That(sprite.Animations, Has.Count.EqualTo(8));
            Assert.That(sprite.Animations[0].Steps[0].FrameIndex, Is.EqualTo(2));
            Assert.That(sprite.Animations[5].Steps[2].FrameIndex, Is.EqualTo(11));
        }

        [Test]
        public void SpriteFrames_RequireDimensionsAnd4BppPixels()
        {
            var pixels = new byte[16 * 32];
            Assert.Throws<ArgumentException>(() => new IndexedSpriteFrameDefinition(0, 8, 32, pixels));
            pixels[3] = 16;
            Assert.Throws<ArgumentException>(() => new IndexedSpriteFrameDefinition(0, 16, 32, pixels));
            Assert.Throws<ArgumentException>(() => new OverworldSpriteDefinition(
                "bad", 16, 32, CreatePalette(),
                new List<IndexedSpriteFrameDefinition> { new IndexedSpriteFrameDefinition(0, 16, 32, new byte[16 * 32]) },
                CreateAnimationsWithMissingFrame()));
        }

        [Test]
        public void SpriteAnimationSteps_RequireAnExistingFrameAndPositiveDuration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteAnimationStepDefinition(-1, false, false, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SpriteAnimationStepDefinition(0, false, false, 0));

            var frames = new List<IndexedSpriteFrameDefinition>
            {
                new IndexedSpriteFrameDefinition(0, 16, 32, new byte[16 * 32]),
            };
            var animations = CreateAnimations();
            animations[0] = new DirectionalSpriteAnimationDefinition(
                SpriteDirection.South,
                SpriteAnimationState.Idle,
                new List<SpriteAnimationStepDefinition> { new SpriteAnimationStepDefinition(9, false, false, 1) });
            Assert.Throws<ArgumentException>(() => new OverworldSpriteDefinition("bad", 16, 32, CreatePalette(), frames, animations));
        }

        private static List<Rgba32> CreatePalette()
        {
            var result = new List<Rgba32>(16);
            for (var i = 0; i < 16; i++) result.Add(new Rgba32((byte)i, (byte)(i + 1), (byte)(i + 2), 255));
            return result;
        }

        private static List<DirectionalSpriteAnimationDefinition> CreateAnimations()
        {
            var result = new List<DirectionalSpriteAnimationDefinition>(8);
            var directions = new[] { SpriteDirection.South, SpriteDirection.North, SpriteDirection.West, SpriteDirection.East };
            for (var direction = 0; direction < directions.Length; direction++)
            {
                result.Add(new DirectionalSpriteAnimationDefinition(
                    directions[direction], SpriteAnimationState.Idle,
                    new List<SpriteAnimationStepDefinition> { new SpriteAnimationStepDefinition(2, false, false, 16) }));
                result.Add(new DirectionalSpriteAnimationDefinition(
                    directions[direction], SpriteAnimationState.Walking,
                    new List<SpriteAnimationStepDefinition>
                    {
                        new SpriteAnimationStepDefinition(7, false, false, 8),
                        new SpriteAnimationStepDefinition(2, false, false, 8),
                        new SpriteAnimationStepDefinition(11, false, false, 8),
                    }));
            }
            return result;
        }

        private static List<DirectionalSpriteAnimationDefinition> CreateAnimationsWithMissingFrame()
        {
            var result = CreateAnimations();
            result[0] = new DirectionalSpriteAnimationDefinition(
                SpriteDirection.South, SpriteAnimationState.Idle,
                new List<SpriteAnimationStepDefinition> { new SpriteAnimationStepDefinition(1, false, false, 1) });
            return result;
        }
    }
}
