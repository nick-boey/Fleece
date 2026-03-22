# Fleece Documentation

Fleece is a lightweight, file-based issue tracking system designed to live alongside your code. Issues are stored as JSONL files in your repository, making them versionable, portable, and always accessible - even offline.

## Quick Start

Install Fleece as a .NET global tool:

```bash
dotnet tool install --global Fleece.Cli
```

Create your first issue:

```bash
fleece create --title "My first issue" --type task
```

## Key Features

- **Local-first**: Issues live in your repository, not a remote server
- **Version-controlled**: Track issue changes alongside code changes
- **Simple**: No database, no server, just files
- **AI-friendly**: Built-in integration with Claude Code for AI-assisted development

## Documentation Sections

### [Getting Started](articles/getting-started.md)

Installation guide, basic usage, and quick start tutorial.

### [Architecture](articles/architecture.md)

Overview of Fleece.Core and Fleece.Cli components and design principles.

### [CLI Reference](articles/cli-reference.md)

Complete documentation for all Fleece CLI commands and options.

### [CI/CD](articles/ci-cd.md)

Continuous deployment setup and release automation.

### [API Reference](api/index.md)

Programmatic API documentation for Fleece.Core and Fleece.Cli namespaces.

## Source Code

Fleece is open source and available on [GitHub](https://github.com/nick-boey/Fleece).
