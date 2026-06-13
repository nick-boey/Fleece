## MODIFIED Requirements

### Requirement: Dedicated v4-migration topic

`fleece prime v4-migration` SHALL emit instructions for migrating a repository that still holds a legacy durable `.fleece/issues.jsonl` snapshot. The instructions SHALL direct the user to **first** run `fleece migrate` to convert the durable layout (`.fleece/issues.jsonl` + `.fleece/changes/`) into v4 per-issue logs, because the durable issues are not otherwise visible to `fleece list`. The instructions SHALL then direct the user to review the converted issues (`fleece list --all`), `promote` the long-running ones to GitHub Issues, resolve or close the rest, and `seal` to archive and clear remaining inactive issues.

#### Scenario: v4-migration topic guides conversion before review
- **WHEN** `fleece prime v4-migration` runs
- **THEN** output instructs the user to convert the durable snapshot layout into per-issue logs before listing issues
- **AND** output instructs the user to promote long-running legacy issues to GitHub Issues and then seal
