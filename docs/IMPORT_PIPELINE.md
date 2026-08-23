# Import pipeline

1. Read and fingerprint a user-selected local file.
2. Parse and validate the GBA header.
3. Select an exact game/revision adapter.
4. Parse all requested content into IR without changing Unity assets.
5. Validate IR invariants.
6. Generate or update stable local assets and a manifest.
7. Open the generated preview scene and report warnings/errors.

Any failure before step 6 leaves existing generated assets unchanged.

