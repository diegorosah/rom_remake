using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RetroRPG.IR;
using RetroRPG.Importers.GBA.PokemonFireRed;
using RetroRPG.Runtime;
using UnityEngine;
using IrEncounterTableDefinition = RetroRPG.IR.EncounterTableDefinition;
using RuntimeDialogueDefinition = RetroRPG.Runtime.DialogueDefinition;
using RuntimeEncounterTableDefinition = RetroRPG.Runtime.EncounterTableDefinition;

namespace RetroRPG.Tests.EditMode
{
    public sealed class Mvp6EncounterTests
    {
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
            {
                if (objects[index] != null) UnityEngine.Object.DestroyImmediate(objects[index]);
            }
            objects.Clear();
        }

        [Test]
        public void EncounterTable_UsesDeterministicWeightedSelectionAndInclusiveLevels()
        {
            var slots = new List<EncounterSlotDefinition>();
            for (var index = 0; index < 5; index++) slots.Add(new EncounterSlotDefinition("creature-" + index, 20, 5, 7));
            var table = new RuntimeEncounterTableDefinition("route-land", 10000, slots);
            Assert.That(table.TotalWeight, Is.EqualTo(100));
            var weightRolls = new[] { 0, 20, 40, 60, 80 };
            for (var index = 0; index < weightRolls.Length; index++)
            {
                var random = new QueueEncounterRandom(0, weightRolls[index], 2);
                Assert.That(table.Roll(random, out var selection), Is.True);
                Assert.That(selection.CreatureKey, Is.EqualTo("creature-" + index));
                Assert.That(selection.Level, Is.EqualTo(7), "the maximum level is inclusive");
            }
            var halfChanceTable = new RuntimeEncounterTableDefinition("half", 5000, slots);
            Assert.That(halfChanceTable.Roll(new QueueEncounterRandom(9999), out _), Is.False, "a roll outside the configured encounter chance does not select");
            Assert.Throws<ArgumentOutOfRangeException>(() => new EncounterSlotDefinition("invalid", 1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RuntimeEncounterTableDefinition("invalid", 10001, slots));
        }

        [Test]
        public void EncounterCatalog_GatesByMapCellElevationAndExplorationFlag()
        {
            var table = new RuntimeEncounterTableDefinition("land", 10000, new[] { new EncounterSlotDefinition("creature", 100, 2, 2) });
            var catalogObject = Track(new GameObject("encounters"));
            var catalog = catalogObject.AddComponent<RuntimeEncounterCatalog>();
            catalog.Configure(new[] { table }, new[]
            {
                new EncounterCellDefinition("route", new Vector2Int(2, 3), 1, "land", true),
                new EncounterCellDefinition("route", new Vector2Int(3, 3), 1, "land", false),
            });
            Assert.That(catalog.TryResolve("route", new Vector2Int(2, 3), 1, out var cell, out var resolved), Is.True);
            Assert.That(cell.TableId, Is.EqualTo("land"));
            Assert.That(resolved, Is.SameAs(table));
            Assert.That(catalog.TryResolve("other-map", new Vector2Int(2, 3), 1, out _, out _), Is.False);
            Assert.That(catalog.TryResolve("route", new Vector2Int(2, 3), 2, out _, out _), Is.False);
            Assert.That(catalog.TryResolve("route", new Vector2Int(3, 3), 1, out _, out _), Is.False);
        }

        [Test]
        public void EncounterRuntime_TriggersOnlyAfterMovementCompletedAndPresentsDebugEvent()
        {
            var collision = CreateCollisionMap(4, 3);
            var map = Track(new GameObject("map")).AddComponent<MapRuntimeRoot>();
            map.Configure("route", collision, new MapRuntimeWarp[0]);
            var mapCatalog = Track(new GameObject("maps")).AddComponent<RuntimeMapCatalog>();
            mapCatalog.Configure(new[] { map });
            var table = new RuntimeEncounterTableDefinition("land", 10000, new[] { new EncounterSlotDefinition("creature", 100, 2, 2) });
            var encounterCatalog = Track(new GameObject("encounter-catalog")).AddComponent<RuntimeEncounterCatalog>();
            encounterCatalog.Configure(new[] { table }, new[] { new EncounterCellDefinition("route", new Vector2Int(2, 1), 0, "land", true) });
            var player = Track(new GameObject("player")).AddComponent<PlayerController>();
            player.Configure(collision, new Vector2Int(1, 1), 0, 2f);
            Assert.That(player.TryMove(GridDirection.Right), Is.True);
            var debug = new RecordingEncounterView();
            var encounterSystem = Track(new GameObject("encounter-system")).AddComponent<EncounterSystem>();
            encounterSystem.Configure(player, null, mapCatalog, encounterCatalog, null, new QueueEncounterRandom(0, 0, 0), debug);
            var triggerCount = 0;
            encounterSystem.EncounterTriggered += _ => triggerCount++;
            Assert.That(triggerCount, Is.Zero, "movement has not completed yet");
            player.Advance(0.5f);
            Assert.That(triggerCount, Is.EqualTo(1));
            Assert.That(debug.Count, Is.EqualTo(1));
            Assert.That(debug.Last.MapId, Is.EqualTo("route"));
            Assert.That(debug.Last.Cell, Is.EqualTo(new Vector2Int(2, 1)));
        }

        [Test]
        public void EncounterRuntime_IsBlockedDuringDialogueAndUsesAnIndependentRandomSource()
        {
            var collision = CreateCollisionMap(4, 3);
            var map = Track(new GameObject("map")).AddComponent<MapRuntimeRoot>();
            map.Configure("route", collision, new MapRuntimeWarp[0]);
            var mapCatalog = Track(new GameObject("maps")).AddComponent<RuntimeMapCatalog>();
            mapCatalog.Configure(new[] { map });
            var table = new RuntimeEncounterTableDefinition("land", 10000, new[] { new EncounterSlotDefinition("creature", 100, 2, 2) });
            var encounterCatalog = Track(new GameObject("encounter-catalog")).AddComponent<RuntimeEncounterCatalog>();
            encounterCatalog.Configure(new[] { table }, new[] { new EncounterCellDefinition("route", new Vector2Int(2, 1), 0, "land", true) });
            var player = Track(new GameObject("player")).AddComponent<PlayerController>();
            player.Configure(collision, new Vector2Int(1, 1), 0, 2f);
            var dialogueCatalog = Track(new GameObject("dialogue-catalog")).AddComponent<DialogueCatalog>();
            dialogueCatalog.Configure(new[] { new RuntimeDialogueDefinition("talk", new[] { "synthetic" }) });
            var dialogueController = Track(new GameObject("dialogue-controller")).AddComponent<DialogueController>();
            dialogueController.Configure(dialogueCatalog, player);
            var encounterRandom = new QueueEncounterRandom(0, 0, 0);
            var encounterSystem = Track(new GameObject("encounter-system")).AddComponent<EncounterSystem>();
            encounterSystem.Configure(player, null, mapCatalog, encounterCatalog, dialogueController, encounterRandom, null);
            Assert.That(encounterSystem.RandomSource, Is.SameAs(encounterRandom));
            Assert.That(dialogueController.TryOpen("talk", map), Is.True);
            Assert.That(player.TryMove(GridDirection.Right), Is.True);
            player.Advance(0.5f);
            Assert.That(dialogueController.IsOpen, Is.True);
            Assert.That(player.InputEnabled, Is.False);
            dialogueController.Close();
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(encounterSystem.RandomSource, Is.SameAs(encounterRandom));
        }

        [Test]
        public void EncounterRuntime_IsBlockedWhileMapTransitionIsInProgress()
        {
            var collision = CreateCollisionMap(4, 3);
            var map = Track(new GameObject("map")).AddComponent<MapRuntimeRoot>();
            map.Configure("route", collision, new MapRuntimeWarp[0]);
            var mapCatalog = Track(new GameObject("maps")).AddComponent<RuntimeMapCatalog>();
            mapCatalog.Configure(new[] { map });
            var encounterCatalog = Track(new GameObject("encounters")).AddComponent<RuntimeEncounterCatalog>();
            encounterCatalog.Configure(
                new[] { new RuntimeEncounterTableDefinition("land", 10000, new[] { new EncounterSlotDefinition("creature", 100, 1, 1) }) },
                new[] { new EncounterCellDefinition("route", new Vector2Int(2, 1), 0, "land", true) });
            var player = Track(new GameObject("player")).AddComponent<PlayerController>();
            player.Configure(collision, new Vector2Int(1, 1), 0, 2f);
            var transitions = Track(new GameObject("transitions")).AddComponent<MapTransitionSystem>();
            transitions.Configure(mapCatalog, player, null, map);
            var encounterSystem = Track(new GameObject("encounter-system")).AddComponent<EncounterSystem>();
            encounterSystem.Configure(player, transitions, mapCatalog, encounterCatalog, null, new QueueEncounterRandom(0, 0, 0), null);
            var triggered = 0;
            encounterSystem.EncounterTriggered += _ => triggered++;
            FieldInfo transitionState = typeof(MapTransitionSystem).GetField("isTransitioning", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(transitionState, Is.Not.Null);
            transitionState.SetValue(transitions, true);
            Assert.That(player.TryMove(GridDirection.Right), Is.True);
            player.Advance(0.5f);
            Assert.That(triggered, Is.Zero);
        }

        [Test]
        public void EncounterRandom_IsDeterministicAndSeparateFromNpcRandomStream()
        {
            var first = new DeterministicEncounterRandomSource(123u);
            var second = new DeterministicEncounterRandomSource(123u);
            for (var index = 0; index < 8; index++) Assert.That(first.NextInt(10000), Is.EqualTo(second.NextInt(10000)));
            Assert.That(first, Is.Not.TypeOf<DeterministicNpcRandomSource>());
            Assert.Throws<ArgumentOutOfRangeException>(() => first.NextInt(0));
        }

        [Test]
        public void EncounterIr_IsDeterministicAndRejectsDuplicateSlotsOrZoneCells()
        {
            var zone = new EncounterZoneDefinition("zone", "route", EncounterMethod.Land, new[]
            {
                new MapCellCoordinate(4, 2), new MapCellCoordinate(1, 1), new MapCellCoordinate(2, 1),
            });
            Assert.That(zone.Cells[0].Equals(new MapCellCoordinate(1, 1)), Is.True);
            Assert.Throws<ArgumentException>(() => new EncounterZoneDefinition("duplicate", "route", EncounterMethod.Land, new[] { new MapCellCoordinate(1, 1), new MapCellCoordinate(1, 1) }));
            var table = new IrEncounterTableDefinition("table", "route", EncounterMethod.Land, 21, new[]
            {
                new EncounterWeightedEntryDefinition(1, 50, 16, 3, 3), new EncounterWeightedEntryDefinition(0, 50, 19, 2, 4),
            });
            Assert.That(table.Entries[0].SlotIndex, Is.Zero);
            Assert.That(table.TotalWeight, Is.EqualTo(100));
            Assert.Throws<ArgumentException>(() => new IrEncounterTableDefinition("duplicate", "route", EncounterMethod.Land, 21, new[]
            {
                new EncounterWeightedEntryDefinition(0, 50, 16, 3, 3), new EncounterWeightedEntryDefinition(0, 50, 19, 2, 4),
            }));
            var catalog = new EncounterCatalogDefinition(new[] { zone }, new[] { table });
            Assert.That(catalog.TryGetZone("zone", out _), Is.True);
            Assert.That(catalog.TryGetTable("table", out _), Is.True);
        }

        [Test, Explicit("Requires RETRO_RPG_TEST_ROM to point to the user's legal FireRed USA rev1 ROM.")]
        public void SupportedRom_ParsesRoute1EncounterCatalog()
        {
            var path = Environment.GetEnvironmentVariable("RETRO_RPG_TEST_ROM");
            Assert.That(path, Is.Not.Null.And.Not.Empty);
            var result = new FireRedMapBundleParser().Parse(RomFile.Load(path));
            Assert.That(result.Succeeded, Is.True, "Route 1 parse failed with " + result.Report.Diagnostics.Count + " diagnostics.");
            Assert.That(result.EncounterCatalog, Is.Not.Null);
            Assert.That(result.Bundle.GetMap(FireRedRomLayoutRev1.Route1MapId).Width, Is.EqualTo(24));
            Assert.That(result.Bundle.GetMap(FireRedRomLayoutRev1.Route1MapId).Height, Is.EqualTo(40));
            Assert.That(result.Bundle.GetMap(FireRedRomLayoutRev1.Route1MapId).Cells, Has.Count.EqualTo(960));
            Assert.That(result.EncounterCatalog.Zones, Has.Count.EqualTo(1));
            Assert.That(result.EncounterCatalog.Zones[0].Cells, Has.Count.EqualTo(178));
            Assert.That(result.EncounterCatalog.Tables, Has.Count.EqualTo(1));
            var table = result.EncounterCatalog.Tables[0];
            Assert.That(table.BaseRate, Is.EqualTo(21));
            Assert.That(table.Entries, Has.Count.EqualTo(12));
            Assert.That(table.TotalWeight, Is.EqualTo(100));
            var pidgeyWeight = 0;
            var rattataWeight = 0;
            for (var index = 0; index < table.Entries.Count; index++)
            {
                if (table.Entries[index].SpeciesId == 16) pidgeyWeight += table.Entries[index].Weight;
                if (table.Entries[index].SpeciesId == 19) rattataWeight += table.Entries[index].Weight;
            }
            Assert.That(pidgeyWeight, Is.EqualTo(50));
            Assert.That(rattataWeight, Is.EqualTo(50));
        }

        private GridCollisionMap CreateCollisionMap(int width, int height)
        {
            var map = Track(new GameObject("collision")).AddComponent<GridCollisionMap>();
            map.Configure(width, height, new byte[width * height], new byte[width * height], new GridDirectionMask[width * height]);
            return map;
        }

        private T Track<T>(T unityObject) where T : UnityEngine.Object
        {
            objects.Add(unityObject);
            return unityObject;
        }

        private sealed class QueueEncounterRandom : IEncounterRandomSource
        {
            private readonly Queue<int> values;
            public QueueEncounterRandom(params int[] configuredValues) { values = new Queue<int>(configuredValues); }
            public int NextInt(int exclusiveUpperBound)
            {
                if (values.Count == 0) return 0;
                var value = values.Dequeue();
                if (value < 0 || value >= exclusiveUpperBound) throw new ArgumentOutOfRangeException(nameof(value));
                return value;
            }
        }

        private sealed class RecordingEncounterView : IEncounterDebugView
        {
            public int Count;
            public EncounterTrigger Last;
            public void Present(EncounterTrigger trigger) { Count++; Last = trigger; }
        }
    }
}
