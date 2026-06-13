<!-- This file is managed by `fleece install`. Manual edits will be overwritten on the next install. -->

# Issue Statuses

Active (block PR merge / `fleece seal`):
- **open**: An issue that has not been started
- **progress**: Currently being worked on
- **review**: Work complete, awaiting review

Inactive (terminal, do not block):
- **complete**: Work finished and verified
- **closed**: Abandoned or won't fix
- **promoted**: Escalated into a GitHub issue (carries a `promoted=<#>` keyed tag)

## Usage

Update status: `fleece edit <id> -s progress`

Filter by status: `fleece list -s progress`

Only active issues ({open, progress, review}) are shown in `fleece list`. To include
terminal statuses use `fleece list --all`.
