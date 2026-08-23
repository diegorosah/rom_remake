using NUnit.Framework;
using RetroRPG.Runtime;
using UnityEngine;

namespace RetroRPG.Tests.EditMode
{
    public sealed class RuntimeControllerTests
    {
        [Test]
        public void PlayerController_SpawnsPersistsAndMovesOneExactCardinalCell()
        {
            var mapObject = new GameObject("map");
            var playerObject = new GameObject("player");
            try
            {
                var map = mapObject.AddComponent<GridCollisionMap>();
                map.Configure(4, 4, new byte[16], new byte[16], new GridDirectionMask[16]);
                var player = playerObject.AddComponent<PlayerController>();
                player.Configure(map, new Vector2Int(1, 1), 3, 2f);
                Assert.That(player.CurrentCell, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(player.Elevation, Is.EqualTo(3));
                Assert.That(player.transform.position, Is.EqualTo(map.CellCenter(new Vector2Int(1, 1))));

                Assert.That(player.TryMove(GridDirection.Right), Is.True);
                Assert.That(player.IsMoving, Is.True);
                Assert.That(player.TryMove(GridDirection.Up), Is.False, "a second cardinal command cannot overlap a step");
                player.Advance(0.5f);
                Assert.That(player.IsMoving, Is.False);
                Assert.That(player.CurrentCell, Is.EqualTo(new Vector2Int(2, 1)));
                Assert.That(player.transform.position, Is.EqualTo(map.CellCenter(new Vector2Int(2, 1))));
                Assert.That(player.Facing, Is.EqualTo(GridDirection.Right));
            }
            finally { Object.DestroyImmediate(playerObject); Object.DestroyImmediate(mapObject); }
        }

        [Test]
        public void PlayerController_ChangesFacingWhenBlockedWithoutDisplacement()
        {
            var mapObject = new GameObject("map");
            var playerObject = new GameObject("player");
            try
            {
                var map = mapObject.AddComponent<GridCollisionMap>();
                var collision = new byte[9];
                collision[5] = 1;
                map.Configure(3, 3, collision, new byte[9], new GridDirectionMask[9]);
                var player = playerObject.AddComponent<PlayerController>();
                player.Configure(map, new Vector2Int(1, 1), 0, 4f);
                var start = player.transform.position;

                Assert.That(player.TryMove(GridDirection.Right), Is.False);
                Assert.That(player.Facing, Is.EqualTo(GridDirection.Right));
                Assert.That(player.CurrentCell, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(player.transform.position, Is.EqualTo(start));
                Assert.That(player.TryMove(GridDirection.None), Is.False);
            }
            finally { Object.DestroyImmediate(playerObject); Object.DestroyImmediate(mapObject); }
        }

        [Test]
        public void PlayerController_ValidatesSpawnAndConfigurableSpeed()
        {
            var mapObject = new GameObject("map");
            var playerObject = new GameObject("player");
            try
            {
                var map = mapObject.AddComponent<GridCollisionMap>();
                var collision = new byte[4]; collision[0] = 1;
                map.Configure(2, 2, collision, new byte[4], new GridDirectionMask[4]);
                var player = playerObject.AddComponent<PlayerController>();
                Assert.Throws<System.ArgumentOutOfRangeException>(() => player.Configure(map, new Vector2Int(2, 0), 0));
                Assert.Throws<System.ArgumentException>(() => player.Configure(map, Vector2Int.zero, 0));
                Assert.Throws<System.ArgumentOutOfRangeException>(() => player.Configure(map, new Vector2Int(1, 1), 0, 0f));
            }
            finally { Object.DestroyImmediate(playerObject); Object.DestroyImmediate(mapObject); }
        }

        [Test]
        public void DirectionalAnimator_ChangesDirectionAndAdvancesDeterministicTicks()
        {
            var objectWithRenderer = new GameObject("sprite");
            var texture = new Texture2D(1, 1);
            var sprites = new[]
            {
                Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero),
                Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero),
                Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero),
            };
            try
            {
                var renderer = objectWithRenderer.AddComponent<SpriteRenderer>();
                var animator = objectWithRenderer.AddComponent<DirectionalSpriteAnimator>();
                var idle = CreateSequences(sprites[0]);
                var walk = CreateSequences(sprites[1], sprites[2]);
                animator.Configure(renderer, idle, walk);
                Assert.That(animator.CurrentSprite, Is.SameAs(sprites[0]));
                animator.SetState(GridDirection.Left, true);
                Assert.That(animator.Facing, Is.EqualTo(GridDirection.Left));
                Assert.That(animator.IsWalking, Is.True);
                Assert.That(animator.CurrentSprite, Is.SameAs(sprites[1]));
                animator.Advance(8f / DirectionalSpriteAnimator.TickRate);
                Assert.That(animator.CurrentFrameIndex, Is.EqualTo(1));
                Assert.That(animator.CurrentSprite, Is.SameAs(sprites[2]));
                animator.SetState(GridDirection.Up, false);
                Assert.That(animator.CurrentSprite, Is.SameAs(sprites[0]));
                Assert.That(animator.ElapsedTicks, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(objectWithRenderer);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void PixelPerfectCameraFollow_ClampsAndQuantizesToSixteenth()
        {
            var cameraObject = new GameObject("camera");
            var targetObject = new GameObject("target");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 2f;
                camera.aspect = 1f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                var follow = cameraObject.AddComponent<PixelPerfectCameraFollow>();
                follow.Configure(camera, targetObject.transform, new Rect(0f, 0f, 12f, 12f));
                var position = follow.CalculateFollowPosition(new Vector3(20.03f, -4.01f, 0f));
                Assert.That(position.x, Is.EqualTo(10f).Within(0.0001f));
                Assert.That(position.y, Is.EqualTo(2f).Within(0.0001f));
                var quantized = follow.CalculateFollowPosition(new Vector3(6.13f, 6.19f, 0f));
                Assert.That(quantized.x * 16f, Is.EqualTo(Mathf.Round(quantized.x * 16f)).Within(0.0001f));
                Assert.That(quantized.y * 16f, Is.EqualTo(Mathf.Round(quantized.y * 16f)).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(targetObject); Object.DestroyImmediate(cameraObject); }
        }

        private static DirectionalSpriteSequence[] CreateSequences(Sprite first, Sprite second = null)
        {
            var result = new DirectionalSpriteSequence[4];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = second == null
                    ? new DirectionalSpriteSequence(new[] { first }, 1)
                    : new DirectionalSpriteSequence(new[] { first, second }, 8);
            }
            return result;
        }
    }
}
