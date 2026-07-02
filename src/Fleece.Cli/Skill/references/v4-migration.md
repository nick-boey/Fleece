<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Migrating a Legacy Durable Repository

Older Fleece repositories used `.fleece/` as a DURABLE issue tracker, persisting a
snapshot at `.fleece/issues.jsonl` (optionally layered with per-session change files under
`.fleece/changes/`). v4 treats Fleece as ephemeral branch memory, so that legacy layout
must be migrated once. When Fleece detects `.fleece/issues.jsonl` it prints a
non-destructive warning pointing here.

## Steps

1. **Convert** the durable layout into v4 per-issue logs:

   ```
   fleece migrate
   ```

   `fleece migrate` auto-detects the legacy durable snapshot (and the even older
   hashed-file layout) and rewrites it as per-issue logs under `.fleece/issues/`,
   consuming the `.fleece/issues.jsonl` snapshot and the `.fleece/changes/` directory.
   Until you run it the durable issues are invisible to `fleece list`, so the next step
   shows nothing without it.

2. **Review** the converted issues: `fleece list --all` to see everything, including
   terminal statuses.

3. **Promote** the long-running / still-relevant issues to {{TRACKER_TITLE}} so they outlive
   the repository's branches:

   ```
   fleece promote <id> [<id>...]
   ```

   Bundle related issues where it makes sense. See `references/{{TRACKER}}.md` for the exact
   promote workflow for this repository's tracker (GitHub: confirm credentials first with
   `fleece auth`; Linear: emit the payload, create the issue, then re-run with `--ref`).

4. **Resolve or close** anything that is already done or no longer relevant
   (`fleece edit <id> -s complete` / `-s closed`).

5. **Seal** to archive and clear the remaining inactive issues:

   ```
   fleece seal
   ```

   `seal` refuses while any issue is still active ({open, progress, review}), so finish
   step 3/4 first. It writes `.fleece/archive/issues_<contenthash>.jsonl` and clears
   `.fleece/issues/`.

6. **Commit** the resulting `.fleece/` changes.

After migrating and sealing, the legacy `.fleece/issues.jsonl` snapshot is gone and the
migration warning stops appearing.
