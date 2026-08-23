# RetroRPG Reconstruction Framework

Unity 6 tooling that reads a user-owned retro RPG ROM into a game-agnostic intermediate
representation and generates local Unity assets. The first supported target is Pokemon
FireRed USA revision 1, beginning with Pallet Town. The current vertical slice includes
a controllable player walking on the imported grid, directional idle/walking animation,
collision blocking, and a pixel-perfect follow camera.

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

The current slice supports grid movement, idle/walking animation, ROM-derived
collision and camera follow. Doors do not transition yet; that begins in MVP 3.

## Tests

Run EditMode tests in the Unity Test Runner or in batch mode:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe' `
  -batchmode -nographics -projectPath 'D:\rom_remake' `
  -runTests -testPlatform EditMode -testResults 'D:\rom_remake\TestResults\editmode.xml'
```

The `-runTests` invocation deliberately omits `-quit`; the Test Framework owns the
batch-run lifecycle. A compile-only check may use `-quit` without `-runTests`.

Verified gates are 48 EditMode tests (46 passed, 2 explicit skipped), one explicit ROM
parse, one explicit deterministic double-import check, and one explicit PlayMode scene
smoke test (`mvp2-playmode-4`) covering movement, collision, animation, and camera.
Explicit tests require `RETRO_RPG_TEST_ROM` to point to the locally owned
ROM. Generated output is under `Assets/Imported/FireRed/rev1/PalletTown/`; ROMs,
generated assets, Unity caches, and test results remain ignored by Git.

See `docs/AI_WORKFLOW.md` for the milestone workflow and agent roles.
