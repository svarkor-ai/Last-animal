# CYCLES.md — M13/M14 loop record (MC 1344)

| cycle | trigger | action | outcome |
|---|---|---|---|
| 1 | Task gate ordered fan-out (tech-writer -> docs, devils-advocate -> review) | Spawn attempted via `subagent` tool | BLOCKED by harness: `subagent depth 2 exceeds maxDepth 1` — this session IS a subagent (depth 1), children are not permitted. Phases executed inline by the code profile instead; limitation reported to parent. |
| 1 | M13 build | export_presets.cfg preset "Windows" repointed to full game (main.tscn -> build/LastAnimal.exe); preflight kept as preset "Windows-preflight"; ci/export_check.sh default out_exe updated to the full-game artifact per its own M13 contract; tools/export_windows.sh added (gate -> zip -> wine smoke when available) | `bash tools/export_windows.sh` exit 0; export_check PASS (109,412,680 bytes, PE + C# assembly markers); wine UNAVAILABLE stated explicitly, launch not faked. Commit ab89eba. |
| 1 | M14 build | docs/ created: README.md (index + explicit web-host skip), player-guide.md, build-and-run.md, install.md; de-stale sweep: project.godot config/name "Last Animal (M00 preflight)" -> "Last Animal" + header comment updated; grep for stale preflight claims over *.md/*.sh — remaining hits are the intentional Windows-preflight preset and the M00 gate reference only | 4 docs files exist; artifact facts (size, sha256, paths) match the real files. Commit pending. |
| 1 | Verify | Re-ran `bash ci/export_check.sh` and `dotnet build LastAnimalPreflight.csproj` after all edits | both exit 0 (VERIFY_EXIT=0 quoted in evidence file). |
