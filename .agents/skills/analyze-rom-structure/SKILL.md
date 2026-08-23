---
name: analyze-rom-structure
description: Investigate an uncertain RetroRPG ROM structure and produce a bounds-safe, evidence-based parser specification without implementing it.
---

Work read-only and never expose ROM bytes or extracted proprietary assets. Record the ROM
revision and fingerprint, hypothesis, evidence source, pointer/address conversion,
verified offset or range, field layout, bounds rules, expected result, IR mapping,
confidence, and open questions. Cross-check conclusions against the exact supported ROM
and primary technical sources. Stop at a parser specification suitable for
`parser_worker`; do not guess unknown fields.

