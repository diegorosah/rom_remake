---
name: implement-unity-task
description: Implement a bounded, already-specified RetroRPG Unity or C# task while preserving assembly boundaries and deterministic generated assets.
---

Read the assigned contract and applicable `AGENTS.md`. Keep Core and IR independent from
Unity and FireRed, keep parsing in Importers, and keep `UnityEditor` out of runtime
assemblies. Limit changes to the current milestone, make generated paths and reimport
deterministic, add relevant tests, run Unity validation, and report changed files and
results. Use `parser_worker` instead when the assigned work is a documented pure parser.

