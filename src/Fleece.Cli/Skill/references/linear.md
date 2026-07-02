<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Linear Round-trip

This reference is installed because the repository's durable tracker is **Linear**
(`fleece config --get tracker` → `linear`). Fleece is ephemeral branch memory; Linear is the
durable home for long-running work.

**Linear is agent-realized.** The `fleece` CLI never calls Linear — it cannot reach Linear's
API. You (the agent) create and update Linear issues through your Linear MCP tooling; the CLI
only formats payloads, records the reference you supply, and prints guidance. Never ask the
CLI to authenticate to Linear.

## Authentication

`fleece auth` reports that the tracker is `linear` and that the CLI does not authenticate to
Linear (exit `0`). Authentication happens through your Linear MCP tools, not the CLI.

## Promote: Fleece → Linear (emit, then record)

Promotion is two steps because the CLI cannot create the Linear issue itself.

1. **Emit** the escalation payload — run without `--ref`:

   ```
   fleece promote <id> [<id>...]
   ```

   This changes NO issue state. It prints the bundle's root title and a task-list body (add
   `--json` to get `{title, body, issueIds}`), plus the exact `--ref` command to re-run.

2. **Create the Linear issue** via your Linear MCP tools, using the emitted title and body.

3. **Record** the Linear reference the created issue returned (its identifier like `ENG-42`
   or its URL):

   ```
   fleece promote <id> [<id>...] --ref ENG-42
   ```

   This sets each supplied issue to `promoted` and tags it `promoted=ENG-42`. Already-promoted
   issues are skipped with a warning. No auth check runs and no network call is made.

Until you record the reference, the promoted issues stay active — the CI gate still catches an
un-recorded promotion, so a bundle is only "escaped" once step 3 runs.

### Write a complete description BEFORE you promote

The Linear issue is only as useful as the descriptions it carries. Before emitting, make sure
every issue in the bundle has a description that stands on its own outside this branch
(problem/goal, what "done" looks like, constraints a reader without this branch's history
would need). Persist it with `fleece edit <id> -d "<full description>"`, then promote.

## Absorb: Linear → Fleece (guidance)

`fleece absorb <ref>`

The CLI cannot comment on or assign a Linear issue, so `absorb` prints guidance rather than
acting. It directs you to create the Fleece issue yourself:

```
fleece create -t "<title>" -y task -d "<description>" --tag absorbed-from=<ref>
```

Then comment on and assign the source Linear issue with your Linear MCP tooling. `absorb`
makes no Fleece state change and no API call.
