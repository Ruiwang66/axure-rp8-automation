---
name: axure-rp8-design
description: Work in a dedicated visible Axure RP 8 window through native JSON editing commands, preserving user focus and project copies. Project target is 8.0-8.1; only a specific Windows build 3400 binary set is verified and hash-pinned.
---

# Axure RP 8 visible design workflow

This skill is for the Windows adapter in this repository. The project targets Axure RP 8.0-8.1, but only a specific Windows build 3400 binary set is verified and hash-pinned. The official release label is 8.1.0.3400; the signed local files report FileVersion / ProductVersion 8.0.0.3400. See ../../docs/COMPATIBILITY_EN.md. Do not use another binary set, RP9+, or non-Windows hosts unless separately ported and verified.

## Safe workflow

1. Ask the user to start the dedicated launcher with an existing `.rp` file, or use an already authorized running instance.
2. Call `status`; confirm the host is ready and identify the current working-copy path. Never infer that an old `session.json` heartbeat means the process is alive.
3. Read `pages` and the current page's `widgets` or `page.content`; target objects by GUID, not by display name alone.
4. Send one bounded native command at a time through `live/live_cli.py`. Use a JSON file for Chinese or multiline content.
5. After a timeout, inspect the matching request and result files before retrying a mutation.
6. After modifying the current page, save before switching pages. Save As must target a new `.rp` path; the adapter refuses existing output files.
7. Verify the response and inspect the visible canvas. When persistence matters, reopen the saved file in a separate RP8 reader and compare page/widget IDs and interactions.
8. Report unsupported features explicitly. Dynamic panel states, repeaters, adaptive views and arbitrary Axure expression syntax are outside the current implementation.

## Operational constraints

- The launcher copies the source `.rp` into a session-specific working folder. Edit that copy only.
- The visible RP8 window is deliberately non-topmost. The adapter does not use mouse/keyboard simulation or clipboard injection.
- If the dedicated process already exists, the launcher reuses it without activating it or switching its file.
- The host verifies SHA-256 hashes for the exact build 3400 binary set. A matching release label or file version does not replace matching hashes. Never bypass a mismatch.
- Do not force-close RP8 with unsaved work. `close` refuses when there are unsaved changes.
- Runtime requests and results may include user design text. Keep them out of public commits.

