# Settings persistence verification — 2026-11-21

## Scope

This commit contains only atomic configuration saving, rolling backups, recovery of missing or corrupt primary files, preservation of the known-good recovery backup on the first subsequent save, and application-level recovery/save-failure notifications. It keeps the existing single-group configuration model and interface.

Multi-group operations, search, entry display-name editing, appearance controls, DPI/layout recovery and their pending tests are excluded from this commit. Those working-tree changes must complete their own verification before being published.

## Exact candidate checks

The candidate was assembled from the existing committed source plus the isolated persistence changes. Its full regression runner reported 10 PASS lines, including atomic replacement, rolling backups, missing/corrupt primary recovery, recovery-backup retention, locked-file failure preservation, and preservation of both files when neither configuration copy can be read. The candidate also built successfully with the existing Windows .NET Framework build script.

## Running application checks

A separate copy of that candidate was built with unique mutex, settings-event, message-window and startup-registration identifiers. These were the only source changes in the live test copy. System features remained disabled and only three synthetic text-file entries were used.

For each check below, the process remained running after three seconds with no startup-failure log. The group title and all three entries were retained, and the primary file matched the expected SHA-256 exactly:

- Ordinary startup/restart did not rewrite the valid primary.
- A deliberately malformed primary (`<broken>`) was repaired from the valid backup. The backup bytes were unchanged.
- A missing primary was restored from the valid backup. The backup bytes were unchanged.

The isolated process was closed after each case. Other DeskDecor processes were preserved. The original working-tree source files were hash-checked and left unchanged during extraction and validation. Notifications were not visually verified; this record establishes running-process persistence behavior and automated storage regression results.
