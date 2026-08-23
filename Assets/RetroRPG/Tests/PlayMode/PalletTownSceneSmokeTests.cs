using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RetroRPG.Unity;
using RetroRPG.Runtime;
using RetroRPG.Renderers.Classic2D;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace RetroRPG.Tests.PlayMode
{
    public sealed class PalletTownSceneSmokeTests : IPrebuildSetup, IPostBuildCleanup
    {
        private const string SceneAssetPath = "Assets/Imported/FireRed/rev1/PalletTown/PalletTown.unity";
#if UNITY_EDITOR
        private const string BuildSettingsBackupKey = "RetroRPG.Tests.PalletTownSceneSmoke.BuildSettings";
#endif

        public void Setup()
        {
#if UNITY_EDITOR
            var previousScenes = EditorBuildSettings.scenes;
            var backup = new System.Text.StringBuilder();
            for (var i = 0; i < previousScenes.Length; i++)
            {
                if (i > 0) backup.Append('\n');
                backup.Append(previousScenes[i].enabled ? '1' : '0').Append(previousScenes[i].path);
            }
            SessionState.SetString(BuildSettingsBackupKey, backup.ToString());
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SceneAssetPath, true) };
#endif
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            var backup = SessionState.GetString(BuildSettingsBackupKey, string.Empty);
            if (string.IsNullOrEmpty(backup))
            {
                EditorBuildSettings.scenes = System.Array.Empty<EditorBuildSettingsScene>();
            }
            else
            {
                var lines = backup.Split('\n');
                var restored = new EditorBuildSettingsScene[lines.Length];
                for (var i = 0; i < lines.Length; i++)
                {
                    restored[i] = new EditorBuildSettingsScene(lines[i].Substring(1), lines[i][0] == '1');
                }
                EditorBuildSettings.scenes = restored;
            }
            SessionState.EraseString(BuildSettingsBackupKey);
#endif
        }

        [UnityTest, Explicit("Run after the local RETRO_RPG_TEST_ROM integration import has generated Pallet Town.")]
        public IEnumerator GeneratedScene_HasLayeredTilemapsAndSynchronizedAnimation()
        {
            Assert.That(File.Exists(Path.Combine(Directory.GetCurrentDirectory(), SceneAssetPath)), Is.True, "Generate Pallet Town through the local integration import first.");
            var load = SceneManager.LoadSceneAsync(SceneAssetPath, LoadSceneMode.Single);
            yield return load;

            var maps = Object.FindObjectsByType<Tilemap>();
            Assert.That(maps, Has.Length.EqualTo(3));
            var byName = new Dictionary<string, Tilemap>();
            for (var i = 0; i < maps.Length; i++) byName.Add(maps[i].name, maps[i]);
            Assert.That(byName.ContainsKey("Bottom"), Is.True);
            Assert.That(byName.ContainsKey("Middle"), Is.True);
            Assert.That(byName.ContainsKey("Top"), Is.True);
            Assert.That(Camera.main, Is.Not.Null);

            var collision = Object.FindAnyObjectByType<GridCollisionMap>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var animator = Object.FindAnyObjectByType<DirectionalSpriteAnimator>();
            var follow = Object.FindAnyObjectByType<PixelPerfectCameraFollow>();
            var catalog = Object.FindAnyObjectByType<RuntimeMapCatalog>();
            var transitions = Object.FindAnyObjectByType<MapTransitionSystem>();
            var dialogueCatalog = Object.FindAnyObjectByType<DialogueCatalog>();
            var dialogueController = Object.FindAnyObjectByType<DialogueController>();
            var interactionSystem = Object.FindAnyObjectByType<InteractionSystem>();
            var dialogueView = Object.FindAnyObjectByType<ClassicDialogueView>();
            var encounterSystem = Object.FindAnyObjectByType<EncounterSystem>();
            var encounterCatalog = Object.FindAnyObjectByType<RuntimeEncounterCatalog>();
            var encounterView = Object.FindAnyObjectByType<ClassicEncounterDebugView>();
            var battleContent = Object.FindAnyObjectByType<RuntimeBattleContentCatalog>();
            var battleCoordinator = Object.FindAnyObjectByType<BattleCoordinator>();
            var battleView = Object.FindAnyObjectByType<ClassicBattleView>();
            var debugMaps = Object.FindAnyObjectByType<DebugMapHotkeys>();
            Assert.That(collision, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(animator, Is.Not.Null);
            Assert.That(player.SpriteAnimator, Is.SameAs(animator));
            Assert.That(follow, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Maps, Has.Count.EqualTo(5));
            Assert.That(transitions, Is.Not.Null);
            Assert.That(transitions.ActiveMap, Is.Not.Null);
            Assert.That(transitions.ActiveMap.MapId, Is.EqualTo("MAP_PALLET_TOWN"));
            Assert.That(dialogueCatalog, Is.Not.Null);
            Assert.That(dialogueController, Is.Not.Null);
            Assert.That(interactionSystem, Is.Not.Null);
            Assert.That(dialogueView, Is.Not.Null);
            Assert.That(encounterSystem, Is.Not.Null);
            Assert.That(encounterCatalog, Is.Not.Null);
            Assert.That(encounterView, Is.Null);
            Assert.That(battleContent, Is.Not.Null);
            Assert.That(battleCoordinator, Is.Not.Null);
            Assert.That(battleView, Is.Not.Null);
            Assert.That(debugMaps, Is.Not.Null);
            Assert.That(transitions.ActiveMap.Npcs, Has.Count.EqualTo(3));
            Assert.That(transitions.ActiveMap.Occupancy, Is.Not.Null);
            Assert.That(transitions.ActiveMap.GetComponent<NpcSimulationDriver>(), Is.Not.Null);
            var visiblePalletNpcs = 0;
            for (var npcIndex = 0; npcIndex < transitions.ActiveMap.Npcs.Count; npcIndex++)
            {
                var npc = transitions.ActiveMap.Npcs[npcIndex];
                Assert.That(npc.SpriteAnimator, Is.Not.Null);
                Assert.That(npc.SpriteAnimator.CurrentSprite, Is.Not.Null);
                if (npc.IsVisible) visiblePalletNpcs++;
            }
            Assert.That(visiblePalletNpcs, Is.EqualTo(2));

            NpcController dialogueNpc = null;
            for (var npcIndex = 0; npcIndex < transitions.ActiveMap.Npcs.Count; npcIndex++)
            {
                if (transitions.ActiveMap.Npcs[npcIndex].NpcId == "MAP_PALLET_TOWN:object:2")
                {
                    dialogueNpc = transitions.ActiveMap.Npcs[npcIndex];
                    break;
                }
            }
            Assert.That(dialogueNpc, Is.Not.Null);
            dialogueNpc.CancelPendingMove();
            var npcDriver = transitions.ActiveMap.NpcSimulationDriver;
            npcDriver.SetSuspended(true);
            var interactionPositioned = false;
            foreach (var direction in new[] { GridDirection.Right, GridDirection.Left, GridDirection.Up, GridDirection.Down })
            {
                var candidate = dialogueNpc.CurrentCell - GridDirections.ToOffset(direction);
                if (!collision.IsInBounds(candidate) || collision.GetCollision(candidate) != 0 || transitions.ActiveMap.Occupancy.IsOccupied(candidate)) continue;
                player.PlaceAfterTransition(collision, candidate, dialogueNpc.Elevation, direction, transitions.ActiveMap.Occupancy);
                interactionPositioned = true;
                break;
            }
            Assert.That(interactionPositioned, Is.True, "Fat Man must have an adjacent interaction cell.");
            npcDriver.SetSuspended(false);
            player.InputEnabled = true;
            Assert.That(interactionSystem.TryInteract(), Is.True);
            Assert.That(dialogueController.IsOpen, Is.True);
            Assert.That(dialogueView.IsVisible, Is.True);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(npcDriver.IsSuspended, Is.True);
            for (var advance = 0; advance < 8 && dialogueController.IsOpen; advance++)
            {
                dialogueController.Advance(10f);
                dialogueController.AdvanceOrClose();
            }
            Assert.That(dialogueController.IsOpen, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(npcDriver.IsSuspended, Is.False);
            Assert.That(dialogueView.IsVisible, Is.False);
            var encounterCount = 0;
            encounterSystem.SetRandomSource(new ZeroEncounterRandom());
            encounterSystem.EncounterTriggered += _ => encounterCount++;
            Assert.That(debugMaps.EnterRoute(), Is.True);
            Assert.That(transitions.ActiveMap.MapId, Is.EqualTo("MAP_ROUTE1"));
            var routeCollision = transitions.ActiveMap.CollisionMap;
            var encounterStepFound = false;
            foreach (var direction in new[] { GridDirection.Right, GridDirection.Left, GridDirection.Up, GridDirection.Down })
            {
                if (!routeCollision.CanMove(player.CurrentCell, player.Elevation, direction, out var next, out var nextElevation) ||
                    !encounterCatalog.TryResolve("MAP_ROUTE1", next, nextElevation, out _, out _)) continue;
                Assert.That(player.TryMove(direction), Is.True);
                player.Advance(1f / player.CellsPerSecond);
                encounterStepFound = true;
                break;
            }
            Assert.That(encounterStepFound, Is.True, "Debug Route 1 spawn must have an adjacent encounter cell.");
            Assert.That(encounterCount, Is.EqualTo(1));
            Assert.That(battleCoordinator.IsBattleActive, Is.True);
            Assert.That(battleView.IsVisible, Is.True);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(battleCoordinator.TrySubmitPrimaryAttack(), Is.True);
            Assert.That(battleCoordinator.IsAwaitingReturn, Is.True);
            Assert.That(battleView.IsVisible, Is.True, "outcome remains visible until the player returns");
            battleCoordinator.ReturnToMap();
            Assert.That(battleView.IsVisible, Is.False);
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(debugMaps.ReturnToTown(), Is.True);
            Assert.That(transitions.ActiveMap.MapId, Is.EqualTo("MAP_PALLET_TOWN"));

            for (var mapIndex = 0; mapIndex < catalog.Maps.Count; mapIndex++)
            {
                var runtimeMap = catalog.Maps[mapIndex];
                Assert.That(runtimeMap, Is.Not.Null);
                Assert.That(runtimeMap.CollisionMap, Is.Not.Null);
                for (var warpIndex = 0; warpIndex < runtimeMap.Warps.Count; warpIndex++)
                {
                    var warp = runtimeMap.Warps[warpIndex];
                    Assert.That(warp, Is.Not.Null);
                    Assert.That(warp.HasValidIdentity(), Is.True);
                    if (catalog.TryResolve(warp.DestinationMapId, out var destinationMap))
                    {
                        Assert.That(destinationMap.TryGetWarp(warp.DestinationWarpId, out _), Is.True);
                    }
                }
            }
            player.InputEnabled = false;
            Assert.That(player.CurrentCell, Is.EqualTo(new Vector2Int(6, 6)));
            Assert.That(player.Elevation, Is.EqualTo(3));
            Assert.That(collision.GetCollision(player.CurrentCell), Is.Zero);
            Assert.That(collision.GetElevation(player.CurrentCell), Is.EqualTo(3));
            var spawnPosition = player.transform.position;
            var moved = false;
            foreach (var direction in new[] { GridDirection.Right, GridDirection.Left, GridDirection.Up, GridDirection.Down })
            {
                if (!collision.CanMove(player.CurrentCell, player.Elevation, direction, out _, out _)) continue;
                Assert.That(player.TryMove(direction), Is.True);
                Assert.That(animator.IsWalking, Is.True);
                Assert.That(animator.Facing, Is.EqualTo(direction));
                player.Advance(1f / player.CellsPerSecond);
                Assert.That(player.IsMoving, Is.False);
                Assert.That(animator.IsWalking, Is.False);
                Assert.That(player.transform.position, Is.Not.EqualTo(spawnPosition));
                moved = true;
                break;
            }
            Assert.That(moved, Is.True, "The verified spawn must have an open cardinal neighbor.");
            Assert.That(animator.CurrentSprite, Is.Not.Null);

            var blockedProbeFound = false;
            var cardinalDirections = new[] { GridDirection.Right, GridDirection.Left, GridDirection.Up, GridDirection.Down };
            for (var y = 0; y < collision.Height && !blockedProbeFound; y++)
            {
                for (var x = 0; x < collision.Width && !blockedProbeFound; x++)
                {
                    var probeCell = new Vector2Int(x, y);
                    if (collision.GetCollision(probeCell) != 0) continue;
                    for (var directionIndex = 0; directionIndex < cardinalDirections.Length; directionIndex++)
                    {
                        var direction = cardinalDirections[directionIndex];
                        var blockedCell = probeCell + GridDirections.ToOffset(direction);
                        if (!collision.IsInBounds(blockedCell) || collision.GetCollision(blockedCell) == 0) continue;

                        var probeElevation = collision.GetElevation(probeCell);
                        if (probeElevation == 0 || probeElevation == 15) probeElevation = 3;
                        player.Configure(collision, probeCell, probeElevation, player.CellsPerSecond);
                        var blockedStartPosition = player.transform.position;
                        Assert.That(player.TryMove(direction), Is.False);
                        Assert.That(player.IsMoving, Is.False);
                        Assert.That(player.CurrentCell, Is.EqualTo(probeCell));
                        Assert.That(player.transform.position, Is.EqualTo(blockedStartPosition));
                        blockedProbeFound = true;
                        break;
                    }
                }
            }
            Assert.That(blockedProbeFound, Is.True, "The generated map must expose a passable cell adjacent to ROM collision data.");

            var cameraPosition = Camera.main.transform.position;
            Assert.That(cameraPosition.x * 16f, Is.EqualTo(Mathf.Round(cameraPosition.x * 16f)).Within(0.001f));
            Assert.That(cameraPosition.y * 16f, Is.EqualTo(Mathf.Round(cameraPosition.y * 16f)).Within(0.001f));

            Tilemap animatedMap = null;
            Vector3Int animatedPosition = default;
            DeterministicAnimatedTile animatedTile = null;
            foreach (var map in maps)
            {
                foreach (var position in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(position);
                    if (tile == null) continue;
                    Assert.That(map.GetSprite(position), Is.Not.Null, "Missing sprite at " + position);
                    if (tile is DeterministicAnimatedTile)
                    {
                        animatedMap = map;
                        animatedPosition = position;
                        animatedTile = (DeterministicAnimatedTile)tile;
                        break;
                    }
                }
                if (animatedMap != null) break;
            }

            Assert.That(animatedMap, Is.Not.Null, "No generated animated tile was found.");
            var animationData = default(TileAnimationData);
            Assert.That(animatedTile.GetTileAnimationData(animatedPosition, null, ref animationData), Is.True);
            Assert.That(animationData.animatedSprites, Has.Length.GreaterThan(1));
            Assert.That(animationData.animationSpeed, Is.GreaterThan(0f));

            var firstFrame = Mathf.FloorToInt(Time.time * animationData.animationSpeed)
                % animationData.animatedSprites.Length;
            yield return new WaitForSeconds((1f / animationData.animationSpeed) + 0.05f);
            var secondFrame = Mathf.FloorToInt(Time.time * animationData.animationSpeed)
                % animationData.animatedSprites.Length;
            Assert.That(secondFrame, Is.Not.EqualTo(firstFrame), "Animated tile clock did not advance.");
            Assert.That(
                animationData.animatedSprites[secondFrame],
                Is.Not.SameAs(animationData.animatedSprites[firstFrame]),
                "Animated tile did not select a different sprite frame.");
        }

        private sealed class ZeroEncounterRandom : IEncounterRandomSource
        {
            public int NextInt(int exclusiveUpperBound) { return 0; }
        }
    }
}
