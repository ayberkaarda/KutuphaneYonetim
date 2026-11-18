# Threat Model

A short threat model for the Kütüphane Yönetim desktop application.

## Scope and assets

- **Application:** a single-user Windows Forms client that talks directly to SQL Server.
- **Assets:** library data (books, loans) and member names (personal data); the connection string;
  log files under `%LOCALAPPDATA%\KutuphaneYonetim\logs`.
- **Trust boundaries:** the Windows user session that runs the application; the network link to SQL Server;
  the local file system (settings and logs).

## Assumptions

- The application runs on a trusted workstation used by library staff.
- There is no sign-in or role model inside the application; access control is the Windows account and the
  SQL Server login in the connection string.

## Threats and mitigations

| Threat | Mitigation | Status |
|---|---|---|
| SQL injection through book or member fields | All access goes through EF Core with parameters; no hand-built SQL from user input | Mitigated |
| Connection string leaked through the repository | `appsettings.json` is git-ignored; user secrets and environment variables are supported; only `appsettings.example.json` is committed | Mitigated |
| Personal data in log files | Exception messages are not logged (only types, error codes, stack traces); member names are never logged | Mitigated |
| Internal details shown to the user (stack traces, SQL errors) | Users only see fixed Turkish messages; details go to the log file | Mitigated |
| Data corruption from double lending or deleting records with history | Rules in the Business layer, checked in a transaction, backed by a filtered unique index, restrict foreign keys and a check constraint | Mitigated |
| Oversized or empty input | FluentValidation limits match the column lengths; database columns are length limited and not null | Mitigated |
| Unauthorised data changes by anyone who can start the app | No in-app authentication; rely on Windows and SQL Server permissions | Accepted (out of scope for this version) |
| Interception of the database connection | `TrustServerCertificate=True` in the example is for a local server only; use a trusted certificate for remote servers | Documented |
| Direct table access with the same SQL login (SSMS, `sqlcmd`) | Grant the application login only the table permissions it needs | Documented |
| Log files read by other local users | Logs are under the user's own local application data folder | Mitigated by OS permissions |
| Log files growing without bound | Daily rolling files, last 30 kept | Mitigated |

## Out of scope

- Multi-user access control, auditing of who changed what, and backups of the database.
