# Fleece Development Roadmap

## Phase 1: Documentation
- [x] README.md - Project overview and quick start
- [x] ROADMAP.md - Development tracking
- [x] docs/CLI.md - Complete CLI reference

## Phase 2: Foundation
- [x] Create Fleece.slnx solution
- [x] Configure Directory.Build.props
- [x] Configure Directory.Packages.props
- [x] Add LICENSE (MIT)
- [x] Add .gitignore
- [x] Add .editorconfig

## Phase 3: Core Models & ID Generation
- [x] Issue record model
- [x] IssueStatus enum
- [x] IssueType enum
- [x] ConflictRecord model
- [x] IIdGenerator interface
- [x] Sha256IdGenerator implementation
- [x] Sha256IdGenerator unit tests

## Phase 4: Serialization Layer
- [x] FleeceJsonContext (source-generated)
- [x] IJsonlSerializer interface
- [x] JsonlSerializer implementation
- [x] JsonlSerializer unit tests

## Phase 5: Storage Service
- [x] IStorageService interface
- [x] JsonlStorageService implementation
- [x] File locking for concurrent access
- [x] JsonlStorageService unit tests

## Phase 6: Business Logic
- [x] IIssueService interface
- [x] IssueService implementation
- [x] IConflictService interface
- [x] ConflictService implementation
- [x] IMergeService interface
- [x] MergeService implementation
- [x] IssueService unit tests
- [x] MergeService unit tests

## Phase 7: CLI Commands
- [x] Spectre.Console.Cli setup with DI
- [x] CreateCommand
- [x] ListCommand
- [x] EditCommand
- [x] DeleteCommand
- [x] SearchCommand
- [x] DiffCommand
- [x] MergeCommand
- [x] ClearConflictsCommand
- [x] InstallCommand
- [x] PrimeCommand
- [x] TableFormatter
- [x] JsonFormatter

## Phase 8: Final Polish
- [x] Add --help text to all commands
- [x] Add --json output option
- [x] Update ROADMAP.md completion status
- [x] Verify all tests pass
- [x] Manual CLI testing

## Future Enhancements

### Planned
- Web UI for issue visualization
- GitHub/GitLab issue sync
- Custom fields support
- Issue templates
- Bulk operations

### Under Consideration
- SQLite storage backend option
- Remote collaboration features
- Plugin system
- Export to various formats (CSV, Markdown)
