# RetroRPG Reconstruction Framework

Unity 6 tooling that reads a user-owned retro RPG ROM into a game-agnostic intermediate
representation and generates local Unity assets. The first supported target is Pokemon
FireRed USA revision 1. The implementation checkpoint now covers Pallet Town, three
interiors and Route 1; movement, collision, warps, NPCs, dialogue, encounters, and a
minimal one-on-one battle all share the same generated scene.

## Requirements

- Unity `6000.5.9f1`.
- A legally obtained FireRed USA rev1 ROM with SHA-1
  `dd5945db9b930750cb39d00c84da8571feebf417`.
- Git for Windows for source control. ROMs and generated assets are never committed.

## Open and inspect

1. Open this directory as a Unity project.
2. Wait for Package Manager and script compilation to finish.
3. Open **Tools > Retro RPG > ROM Inspector**.
4. Select the local `.gba` file and confirm the supported revision is detected.

The importer stores its last selected path only in Unity `EditorPrefs`. Generated content
is written beneath `Assets/Imported`, which is ignored by Git.

## Play the current slice

1. In **Tools > Retro RPG > ROM Inspector**, select the supported ROM and click
   **Import Pallet Town**.
2. Click **Open Pallet Town Scene** in the Inspector.
3. Press Play and move Red with WASD or the arrow keys.
4. Use Z, X, or E while facing an interactive NPC/prop. Use Z, Space, or Enter to
   advance dialogue.
5. Press R to jump to the audited Route 1 encounter area (P returns to Pallet Town).
   Walk through grass; in battle select **Attack**, then **Return to map**.

The MVP 7 battle is intentionally small: Bulbasaur level 5 versus the Pidgey/Rattata
and level selected by Route 1, with Tackle as the only action. Party HP persists in
memory between battles while the scene remains loaded. It is a deterministic preview,
not an emulation of the native battle formula.

## Tests

Run EditMode tests in the Unity Test Runner or in batch mode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'D:\rom_remake' `
  -runTests -testPlatform EditMode -testResults 'D:\rom_remake\TestResults\editmode.xml'
```

The `-runTests` invocation deliberately omits `-quit`; the Test Framework owns the
batch-run lifecycle. A compile-only check may use `-quit` without `-runTests`.

The last executed Unity gate remains the MVP 2 baseline: 48 EditMode tests (46 passed,
2 explicit skipped), one explicit ROM
parse, one explicit deterministic double-import check, and one explicit PlayMode scene
smoke test (`mvp2-playmode-4`) covering movement, collision, animation, and camera.
MVP 3–7 are implementation checkpoints with static review; their integrated Unity
gate is deliberately deferred until the accelerated renderer pass is complete.
Explicit tests require `RETRO_RPG_TEST_ROM` to point to the locally owned
ROM. Generated output is under `Assets/Imported/FireRed/rev1/PalletTown/`; ROMs,
generated assets, Unity caches, and test results remain ignored by Git.

See `docs/AI_WORKFLOW.md` for the milestone workflow and agent roles.
