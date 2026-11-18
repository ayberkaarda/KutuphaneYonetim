# Changelog

🇬🇧 **English** · 🇹🇷 [Türkçe](CHANGELOG.tr.md)

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Daily rolling file logs with Serilog under `%LOCALAPPDATA%\KutuphaneYonetim\logs` (last 30 files kept).
  Exception messages and personal data are not logged.
- Central FluentValidation validators for book, member and loan input in the Business layer.
- Unit tests for the Business services with Moq: lending conflicts, returns, delete rules and validation.
- Repository and service tests on SQLite in-memory.
- Tests for Turkish error messages and log sanitising.
- Release job in CI: on `v*` tags a win-x64 build is published and uploaded as a zip artifact.
- English and Turkish README with architecture diagram, threat model (`docs/THREAT_MODEL.md`) and this changelog.

### Changed

- The hand-written validation helper was replaced by the validators; messages and trimming are unchanged.

### Fixed

- Unexpected error dialogs no longer show the raw (often English) exception message; they show a Turkish
  message and point to the log file.

## Layered .NET 10 foundation - 2026-11-19 (#1)

### Added

- Layered solution: `Domain`, `Business`, `Data` and `UI` projects under `src/`.
- Business services (`BookService`, `MemberService`, `LoanService`) with rules: a book cannot be lent twice at
  the same time; books and members with open loans or loan history cannot be deleted.
- EF Core data access with entity configurations, the `InitialCreate` migration and sample data; migrations are
  applied on startup.
- In-place upgrade of databases created by the original application, keeping their data.
- Configuration from `appsettings.json`, user secrets and the `KUTUPHANE_CONNECTION` environment variable;
  `appsettings.example.json` is committed instead of real settings.
- Global error handling with Turkish messages.
- Test projects for the example configuration and the legacy database upgrade.
- CI workflow that builds and tests on every push and pull request.

### Changed

- Converted to SDK-style projects targeting .NET 10 with nullable reference types.
- Forms call the Business services through dependency injection instead of running SQL directly.
- README rewritten in English, with a Turkish README.

### Fixed

- Backslash in the example connection string is escaped correctly.

### Removed

- Tracked build output, IDE caches and the database backup.
