# Editor instructions

- Editor code may consume IR and importer APIs but must not leak into Runtime assemblies.
- Generated assets and manifests must be deterministic where practical.
- Parse and validate before changing generated assets.
- Reimport may delete only stale outputs owned by the previous import manifest.

