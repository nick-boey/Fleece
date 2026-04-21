# prime-command Specification

## Purpose

The `fleece prime` command provides AI agents and humans with concise onboarding content about how to use Fleece in the current repository. It emits an overview of issue workflow, types, statuses, hierarchy, filtering, and JSON output by default, and can surface dedicated topic guidance (for example, OpenSpec integration) when invoked with a topic argument. The command adapts its output based on which directories are present in the current working directory (`.fleece/`, `openspec/`).

## Requirements

### Requirement: Silent exit when Fleece is not initialised

The `fleece prime` command SHALL produce no output and exit successfully when the current working directory does not contain a `.fleece/` directory. This applies regardless of whether an `openspec/` directory is present or a topic argument was provided.

#### Scenario: No .fleece directory with no topic
- **WHEN** `fleece prime` is invoked in a directory that does not contain a `.fleece/` folder
- **THEN** the command writes no output to stdout and returns exit code 0

#### Scenario: No .fleece directory with topic
- **WHEN** `fleece prime openspec` is invoked in a directory that does not contain a `.fleece/` folder
- **THEN** the command writes no output to stdout and returns exit code 0

### Requirement: Overview output when Fleece is initialised

When a `.fleece/` directory is present in the current working directory and no topic argument is supplied, `fleece prime` SHALL emit the Fleece overview content (covering issue workflow, types, statuses, hierarchy, filtering, JSON, and the list of available detailed topics).

#### Scenario: Overview in a Fleece-only repository
- **WHEN** `fleece prime` is invoked in a directory containing `.fleece/` but no `openspec/`
- **THEN** the command writes the Fleece overview content to stdout
- **AND** the command returns exit code 0
- **AND** the output does NOT contain the OpenSpec Integration section

### Requirement: OpenSpec integration section when openspec/ is present

When both a `.fleece/` directory and an `openspec/` directory exist in the current working directory and no topic argument is supplied, `fleece prime` SHALL append an "OpenSpec Integration" section to the overview output. This section MUST instruct the agent on:

- The `openspec={change-name}` keyed tag convention for linking a Fleece issue to an OpenSpec change, including that multiple `openspec=` tags on one issue are permitted but discouraged.
- The decision tree for selecting an existing Fleece issue to link when proposing a new OpenSpec change in a single-change session.
- The rule for multi-change sessions (one issue per change, arranged using Fleece hierarchy features).
- The rule that Fleece issues MUST NOT be created per task or per phase of an OpenSpec change.

#### Scenario: Overview in a repository with both Fleece and OpenSpec
- **WHEN** `fleece prime` is invoked in a directory containing both `.fleece/` and `openspec/`
- **THEN** the command writes the Fleece overview content to stdout
- **AND** the output additionally contains an "OpenSpec Integration" section
- **AND** the section names the `openspec={change-name}` tag convention
- **AND** the section describes the branch-suffix (`+<id>`) decision path
- **AND** the section describes the open-unlinked-issue scan fallback
- **AND** the section instructs the agent to ask the user when issue relevance is ambiguous
- **AND** the section describes hierarchy usage for multi-change sessions
- **AND** the section states that issues are per-change, never per-task

#### Scenario: Branch suffix issue selection logic
- **WHEN** the OpenSpec Integration section describes the single-change linking flow
- **THEN** it specifies that a branch name ending in `+<id>` refers to the Fleece issue with that 6-character id
- **AND** it specifies that the referenced issue is linked only if it is open, has no existing `openspec=` tag, and is relevant to the change being proposed
- **AND** it specifies that if any of those conditions fail, the agent falls through to scanning open issues for an unlinked relevant match
- **AND** it specifies that a new issue is created only if no suitable existing issue is found

#### Scenario: Multi-change hierarchy guidance
- **WHEN** the OpenSpec Integration section describes the multi-change flow
- **THEN** it states that one Fleece issue is created per change
- **AND** it states that issues are organised using `--parent-issues` with lex-order and `--execution-order`
- **AND** it states that flat fan-out is the default and intermediate grouping parents are created only when the hierarchy genuinely requires them

### Requirement: Dedicated openspec topic

`fleece prime openspec` SHALL print the OpenSpec integration guidance regardless of whether an `openspec/` directory exists in the current working directory. This requires only that `.fleece/` exists (per the silent-exit requirement).

#### Scenario: Explicit openspec topic with openspec directory present
- **WHEN** `fleece prime openspec` is invoked in a directory containing `.fleece/` and `openspec/`
- **THEN** the command writes the OpenSpec integration guidance content to stdout
- **AND** the command returns exit code 0

#### Scenario: Explicit openspec topic without openspec directory
- **WHEN** `fleece prime openspec` is invoked in a directory containing `.fleece/` but not `openspec/`
- **THEN** the command writes the OpenSpec integration guidance content to stdout
- **AND** the command returns exit code 0

#### Scenario: openspec listed among available topics in overview
- **WHEN** `fleece prime` is invoked in a directory containing `.fleece/`
- **THEN** the overview output's list of detailed topics at the end of the document includes `openspec`

### Requirement: Unknown topic handling is unchanged

When a topic argument is supplied that is not in the known topics dictionary, `fleece prime` SHALL continue to emit an "Unknown topic" message followed by the list of available topics and return a non-zero exit code. The addition of the `openspec` topic MUST be reflected in the list of available topics printed in this message.

#### Scenario: Unknown topic lists openspec among available topics
- **WHEN** `fleece prime not-a-real-topic` is invoked in a directory containing `.fleece/`
- **THEN** the command writes a message indicating the topic is unknown
- **AND** the printed list of available topics contains `openspec`
- **AND** the command returns a non-zero exit code
