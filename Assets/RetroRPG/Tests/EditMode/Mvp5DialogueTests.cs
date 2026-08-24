using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RetroRPG.Core;
using RetroRPG.Importers.GBA.Common;
using RetroRPG.Importers.GBA.PokemonFireRed;
using RetroRPG.IR;
using RetroRPG.Renderers.Classic2D;
using RetroRPG.Runtime;
using UnityEngine;
using UnityEngine.UI;
using IrDialogueDefinition = RetroRPG.IR.DialogueDefinition;
using RuntimeDialogueDefinition = RetroRPG.Runtime.DialogueDefinition;

namespace RetroRPG.Tests.EditMode
{
    public sealed class Mvp5DialogueTests
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
        public void DialogueIr_IsDeterministicAndRejectsDuplicateTargetsOrMalformedTokens()
        {
            IrDialogueDefinition second = CreateIrDialogue("z-dialogue", "target-z", false);
            IrDialogueDefinition first = CreateIrDialogue("a-dialogue", "target-a", true);
            DialogueCatalogDefinition catalog = new DialogueCatalogDefinition(new[] { second, first });
            Assert.That(catalog.Dialogues[0].Id, Is.EqualTo("a-dialogue"));
            Assert.That(catalog.TryGetForTarget("target-a", out var found), Is.True);
            Assert.That(found.FacePlayer, Is.True);
            Assert.Throws<ArgumentException>(() => new DialogueCatalogDefinition(new[] { first, CreateIrDialogue("other", "target-a", false) }));
            Assert.Throws<ArgumentException>(() => new DialogueToken(DialogueTokenKind.Glyph));
            Assert.DoesNotThrow(() => new DialogueToken(DialogueTokenKind.Glyph, " "));
            Assert.Throws<ArgumentException>(() => new DialogueToken(DialogueTokenKind.Newline, "unexpected"));
            Assert.Throws<ArgumentException>(() => new DialoguePageDefinition(new DialogueToken[0]));
        }

        [Test]
        public void FireRedDialogueDecoder_DecodeTextTokenizesSyntheticGlyphControlsAndPlaceholders()
        {
            byte[] bytes = { 0xA1, (byte)FireRedRomLayoutRev1.FireRedTextNewline, (byte)FireRedRomLayoutRev1.FireRedTextPlaceholder, 1, (byte)FireRedRomLayoutRev1.FireRedTextExtendedControl, 1, 7, (byte)FireRedRomLayoutRev1.FireRedTextEnd };
            IList<DialogueToken> tokens = InvokeDecodeText(bytes, bytes.Length);
            Assert.That(tokens, Has.Count.EqualTo(4));
            Assert.That(tokens[0].Kind, Is.EqualTo(DialogueTokenKind.Glyph));
            Assert.That(tokens[0].Value, Is.EqualTo("0"));
            Assert.That(tokens[1].Kind, Is.EqualTo(DialogueTokenKind.Newline));
            Assert.That(tokens[2].Kind, Is.EqualTo(DialogueTokenKind.Placeholder));
            Assert.That(tokens[2].Value, Is.EqualTo("Player"));
            Assert.That(tokens[3].Kind, Is.EqualTo(DialogueTokenKind.ExtendedControl));
            Assert.That(tokens[3].Parameters, Has.Count.EqualTo(1));
            Assert.That(tokens[3].Parameters[0], Is.EqualTo(7));
        }

        [Test]
        public void FireRedDialogueDecoder_RejectsMissingEosTruncatedControlsUnknownControlsAndBadPointers()
        {
            Assert.Throws<RomReadException>(() => InvokeDecodeText(new byte[] { 0xA1, 0xA1 }, 2));
            Assert.Throws<RomReadException>(() => InvokeDecodeText(new byte[] { (byte)FireRedRomLayoutRev1.FireRedTextExtendedControl }, 1));
            Assert.Throws<RomReadException>(() => InvokeDecodeText(new byte[] { (byte)FireRedRomLayoutRev1.FireRedTextExtendedControl, 0xFF }, 2));
            Assert.Throws<RomReadException>(() => InvokeDecodeText(new byte[] { (byte)FireRedRomLayoutRev1.FireRedTextPlaceholder, 0xFF }, 2));

            byte[] script = new byte[16];
            script[0] = (byte)FireRedRomLayoutRev1.ScriptOpcodeLoadWord;
            script[1] = (byte)FireRedRomLayoutRev1.ScriptDataSlotZero;
            script[6] = (byte)FireRedRomLayoutRev1.ScriptOpcodeCallStd;
            script[7] = (byte)FireRedRomLayoutRev1.ScriptStandardMessageBoxNpc;
            script[8] = (byte)FireRedRomLayoutRev1.ScriptOpcodeEnd;
            Assert.Throws<RomReadException>(() => InvokeDecodeCircuit(script, 0, 0));
            script[2] = 1;
            script[3] = 0;
            script[4] = 0;
            script[5] = 8;
            Assert.Throws<RomReadException>(() => InvokeDecodeCircuit(script, 0, 0));
        }

        [Test]
        public void DialogueSession_ProgressesTypingCompletionAdvanceAndClose()
        {
            DialogueSession session = new DialogueSession(new RuntimeDialogueDefinition("talk", new[] { "ABCD", "EF" }, 2f));
            Assert.That(session.State, Is.EqualTo(DialogueSessionState.Typing));
            session.Advance(0.5f);
            Assert.That(session.VisibleText, Is.EqualTo("A"));
            Assert.That(session.AdvanceOrComplete(), Is.EqualTo(DialogueAdvanceResult.CompletedPage));
            Assert.That(session.VisibleText, Is.EqualTo("ABCD"));
            Assert.That(session.AdvanceOrComplete(), Is.EqualTo(DialogueAdvanceResult.AdvancedPage));
            Assert.That(session.PageIndex, Is.EqualTo(1));
            Assert.That(session.VisibleText, Is.Empty);
            session.Advance(1f);
            Assert.That(session.State, Is.EqualTo(DialogueSessionState.WaitingForAdvance));
            Assert.That(session.AdvanceOrComplete(), Is.EqualTo(DialogueAdvanceResult.Closed));
            Assert.That(session.IsClosed, Is.True);
            Assert.That(session.AdvanceOrComplete(), Is.EqualTo(DialogueAdvanceResult.Ignored));
        }

        [Test]
        public void DialogueCatalog_IsSerializableThroughConfiguredEntries()
        {
            GameObject catalogObject = Track(new GameObject("dialogue-catalog"));
            DialogueCatalog catalog = catalogObject.AddComponent<DialogueCatalog>();
            catalog.Configure(new[]
            {
                new RuntimeDialogueDefinition("key-a", new[] { "synthetic A" }, 20f),
                new RuntimeDialogueDefinition("key-b", new[] { "synthetic B", "synthetic C" }, 10f),
            });
            Assert.That(catalog.TryResolve("key-a", out var first), Is.True);
            Assert.That(first.GetPage(0), Is.EqualTo("synthetic A"));
            Assert.That(first.CharactersPerSecond, Is.EqualTo(20f));
            Assert.Throws<ArgumentException>(() => catalog.Configure(new[] { new RuntimeDialogueDefinition("same", new[] { "A" }), new RuntimeDialogueDefinition("same", new[] { "B" }) }));
        }

        [Test]
        public void InteractionSystem_ResolvesCellAheadAndRequiresSameElevationAndActiveMap()
        {
            GridCollisionMap collision = CreateCollisionMap(4, 3);
            byte[] blocked = new byte[12];
            blocked[2 + (1 * 4)] = 1;
            collision.Configure(4, 3, blocked, new byte[12], new GridDirectionMask[12]);
            MapRuntimeRoot map = Track(new GameObject("map")).AddComponent<MapRuntimeRoot>();
            map.Configure("map", collision, new MapRuntimeWarp[0]);
            NpcSimulationDriver driver = map.gameObject.AddComponent<NpcSimulationDriver>();
            driver.Configure(map);
            driver.SetSuspended(false);
            PlayerController player = Track(new GameObject("player")).AddComponent<PlayerController>();
            player.Configure(collision, new Vector2Int(1, 1), 0);
            Assert.That(player.TryMove(GridDirection.Right), Is.False);
            InteractionTarget target = Track(new GameObject("target")).AddComponent<InteractionTarget>();
            target.Configure("talk", new Vector2Int(2, 1), 0, true);
            MapInteractionCatalog mapCatalog = Track(new GameObject("map-interactions")).AddComponent<MapInteractionCatalog>();
            mapCatalog.Configure(map, new[] { target });
            RuntimeInteractionCatalog runtimeCatalog = Track(new GameObject("runtime-interactions")).AddComponent<RuntimeInteractionCatalog>();
            runtimeCatalog.Configure(new[] { mapCatalog });
            DialogueCatalog dialogues = Track(new GameObject("dialogues")).AddComponent<DialogueCatalog>();
            dialogues.Configure(new[] { new RuntimeDialogueDefinition("talk", new[] { "synthetic" }) });
            DialogueController controller = Track(new GameObject("dialogue-controller")).AddComponent<DialogueController>();
            controller.Configure(dialogues, player);
            InteractionSystem interactions = Track(new GameObject("interactions")).AddComponent<InteractionSystem>();
            interactions.Configure(player, null, null, runtimeCatalog, controller);

            Assert.That(interactions.TryInteract(), Is.True);
            Assert.That(controller.IsOpen, Is.True);
            controller.Close();
            target.Configure("talk", new Vector2Int(2, 1), 1, true);
            Assert.That(interactions.TryInteract(), Is.False);
            target.Configure("talk", new Vector2Int(2, 1), 0, true);
            map.SetRuntimeActive(false);
            Assert.That(interactions.TryInteract(), Is.False);
        }

        [Test]
        public void DialogueController_RestoresPlayerAndNpcDriverExactlyOnceAndViewLifecycleIsStable()
        {
            GridCollisionMap collision = CreateCollisionMap(4, 4);
            MapRuntimeRoot map = Track(new GameObject("map")).AddComponent<MapRuntimeRoot>();
            map.Configure("map", collision, new MapRuntimeWarp[0]);
            NpcSimulationDriver driver = map.gameObject.AddComponent<NpcSimulationDriver>();
            driver.Configure(map);
            PlayerController player = Track(new GameObject("player")).AddComponent<PlayerController>();
            player.Configure(collision, Vector2Int.one, 0);
            DialogueCatalog catalog = Track(new GameObject("catalog")).AddComponent<DialogueCatalog>();
            catalog.Configure(new[] { new RuntimeDialogueDefinition("talk", new[] { "synthetic" }) });
            DialogueController controller = Track(new GameObject("controller")).AddComponent<DialogueController>();
            RecordingView view = new RecordingView();
            controller.Configure(catalog, player, view);
            player.InputEnabled = false;
            Assert.That(controller.TryOpen("talk", map), Is.True);
            Assert.That(player.InputEnabled, Is.False);
            Assert.That(driver.IsSuspended, Is.True);
            controller.Close();
            controller.Close();
            Assert.That(player.InputEnabled, Is.False, "the prior disabled state is restored, not forced on");
            Assert.That(driver.IsSuspended, Is.False);
            Assert.That(view.PresentCount, Is.EqualTo(1));
            Assert.That(view.HideCount, Is.EqualTo(1));

            player.InputEnabled = true;
            Assert.That(controller.TryOpen("talk", map), Is.True);
            Assert.That(controller.AdvanceOrClose(), Is.EqualTo(DialogueAdvanceResult.CompletedPage));
            Assert.That(controller.AdvanceOrClose(), Is.EqualTo(DialogueAdvanceResult.Closed));
            Assert.That(player.InputEnabled, Is.True);
            Assert.That(view.HideCount, Is.EqualTo(2));
        }

        [Test]
        public void ClassicDialogueView_PresentsAndHidesSyntheticSession()
        {
            GameObject root = Track(new GameObject("view"));
            ClassicDialogueView view = root.AddComponent<ClassicDialogueView>();
            CanvasGroup group = root.AddComponent<CanvasGroup>();
            Text text = new GameObject("text", typeof(RectTransform)).AddComponent<Text>();
            text.transform.SetParent(root.transform, false);
            Text prompt = new GameObject("prompt", typeof(RectTransform)).AddComponent<Text>();
            prompt.transform.SetParent(root.transform, false);
            view.Configure(group, text, prompt);
            DialogueSession session = new DialogueSession(new RuntimeDialogueDefinition("talk", new[] { "HELLO" }, 30f));
            view.Present(session);
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.VisibleText, Is.Empty);
            session.Advance(1f);
            view.Present(session);
            Assert.That(view.VisibleText, Is.EqualTo("HELLO"));
            view.Hide();
            Assert.That(view.IsVisible, Is.False);
            Assert.That(view.VisibleText, Is.Empty);
        }

        private static IrDialogueDefinition CreateIrDialogue(string id, string target, bool facePlayer)
        {
            return new IrDialogueDefinition(id, target, DialoguePresentation.Npc, facePlayer, new[]
            {
                new DialoguePageDefinition(new[] { new DialogueToken(DialogueTokenKind.Glyph, "synthetic") }),
            });
        }

        private static IList<DialogueToken> InvokeDecodeText(byte[] bytes, int expectedLength)
        {
            MethodInfo method = typeof(FireRedMapBundleParser).Assembly.GetType("RetroRPG.Importers.GBA.PokemonFireRed.FireRedDialogueDecoder")
                .GetMethod("DecodeText", BindingFlags.NonPublic | BindingFlags.Static);
            return (IList<DialogueToken>)Invoke(method, new RomReader(bytes), 0, expectedLength);
        }

        private static object InvokeDecodeCircuit(byte[] bytes, int scriptOffset, int expectedTextOffset)
        {
            MethodInfo method = typeof(FireRedMapBundleParser).Assembly.GetType("RetroRPG.Importers.GBA.PokemonFireRed.FireRedDialogueDecoder")
                .GetMethod("DecodeCircuit", BindingFlags.NonPublic | BindingFlags.Static);
            return Invoke(method, new RomReader(bytes), "synthetic", "target", scriptOffset, expectedTextOffset, 1, DialoguePresentation.Npc, true);
        }

        private static object Invoke(MethodInfo method, params object[] arguments)
        {
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        }

        private GridCollisionMap CreateCollisionMap(int width, int height)
        {
            GridCollisionMap map = Track(new GameObject("collision")).AddComponent<GridCollisionMap>();
            map.Configure(width, height, new byte[width * height], new byte[width * height], new GridDirectionMask[width * height]);
            return map;
        }

        private T Track<T>(T unityObject) where T : UnityEngine.Object
        {
            objects.Add(unityObject);
            return unityObject;
        }

        private sealed class RecordingView : IDialogueView
        {
            public int PresentCount;
            public int HideCount;
            public void Present(DialogueSession session) { PresentCount++; }
            public void Hide() { HideCount++; }
        }
    }
}
