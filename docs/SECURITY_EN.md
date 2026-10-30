# Security and privacy

- Use the working copy created by the launcher and keep the original .rp as a rollback copy.
- live/session.json, request/response JSON, screenshots, and exported files may contain prototype text, GUIDs, personal file paths, and window state. Keep them local; do not attach them to issues or commit them to Git.
- This repository contains no Axure executable, DLL, configuration, icon, personal .rp project, or preview screenshot. Users must provide their own authorized Axure RP 8 installation.
- The SHA-256 version checks prevent reflective internal calls from running against a different Axure build. Stop on a mismatch; do not change the expected hashes to force startup.
- Read existing cases before editing interactions. interaction.replaceActions replaces every action in the selected case.
- save refuses to overwrite an existing Save As target. Choose a new output filename.
- The program reads and writes its own user settings directory as Axure normally does. Do not run it as administrator unless your Axure installation directory actually requires that permission.

Before publishing an issue, remove request logs, screenshots, project names, usernames, and other personal information. Include only the smallest reproducible example.
