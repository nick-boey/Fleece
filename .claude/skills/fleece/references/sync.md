<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Keeping Issues in Sync

Issues are stored locally as append-only logs under `.fleece/issues/`. Always commit
changes so the branch's working memory travels with the code.

## Commit Changes

Commit `.fleece/` changes alongside related code changes:

```
git add .fleece/
git commit -m "Update issues"
```

Otherwise use `fleece commit` to create a separate commit containing just the issues.

## Before a PR merges

Resolve (`complete`/`closed`), `promote`, or `seal` every issue, then commit `.fleece/`.
The CI gate fails the PR while any live issue remains under `.fleece/issues/`.

## Best Practices

- Commit issue changes with related code changes
- Pull before starting new work to get the latest issues
- Per-issue logs rarely conflict; if `.fleece/issues/<id>.jsonl` does conflict, keep both
  sides' events (the log is append-only) rather than discarding either version
