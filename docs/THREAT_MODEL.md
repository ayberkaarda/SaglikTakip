# Threat Model

A short threat model for the Sağlık Takip desktop application. The application handles health data, which
is a special category of personal data under the Turkish Personal Data Protection Law (KVKK, Law No. 6698) and
similar laws. This document lists technical measures only; it is not a legal assessment.

## Scope and assets

- **Application:** a single-user Windows Forms client that talks directly to SQL Server.
- **Assets:**
  - Health data: weight and pulse measurements with dates, free-text notes (which may contain diagnoses or
    medication), and exercise history.
  - Personal data: user names, ages and genders.
  - The connection string (server name, database, and possibly SQL credentials).
  - Log files under `%LOCALAPPDATA%\SaglikTakip\logs`.
  - Database backups made by the operator (`.bak` files).
- **Trust boundaries:** the Windows user session that runs the application; the network link to SQL Server;
  the local file system (settings, logs, backups).

## Assumptions

- The application runs on a trusted workstation used by the people whose data is recorded, or by staff
  authorised to see it.
- There is no sign-in or role model inside the application; access control is the Windows account and the
  SQL Server login in the connection string.
- The operator is the data controller and is responsible for consent, retention and deletion requests under KVKK.

## Threats and mitigations

| Threat | Mitigation | Status |
|---|---|---|
| Health or personal data written to log files | Error entries contain only exception types, error codes and method names; exception messages, measurement values, notes and names are never logged; a test checks the log file for them | Mitigated |
| Health data leaked through error dialogs | Users only see fixed Turkish messages; raw exception text is never shown | Mitigated |
| SQL injection through name, notes or exercise fields | All access goes through EF Core with parameters; no hand-built SQL from user input | Mitigated |
| Connection string leaked through the repository | `appsettings.json` is git-ignored; user secrets and environment variables are supported; only `appsettings.example.json` is committed | Mitigated |
| Database backups committed to the repository | `*.bak`, `*.mdf` and `*.ldf` are git-ignored; the old tracked backup was removed | Mitigated |
| Database backups stored unprotected on disk or shared drives | Not handled by the application; keep backups encrypted (for example SQL Server backup encryption or an encrypted volume) and restrict access | Documented |
| Implausible or corrupt health values | FluentValidation limits (age 0-150, weight 1-200, pulse 1-250, no future dates, lengths); database check constraints and foreign keys back them | Mitigated |
| Legacy upgrade altering or exposing existing health rows | The upgrade refuses before any change when rows break the new rules and reports only counts; it runs in one transaction under a checked `sp_getapplock` lock | Mitigated |
| Interception of the database connection | `TrustServerCertificate=True` in the example is for a local server only; for a remote server use `Encrypt=True` with a trusted certificate | Documented |
| Direct table access with the same SQL login (SSMS, `sqlcmd`) | Grant the application login only the table permissions it needs; prefer Windows authentication over SQL passwords | Documented |
| Unauthorised reading or changing of health data by anyone who can start the app | No in-app authentication; rely on Windows and SQL Server permissions | Accepted (out of scope for this version) |
| Log files read by other local users | Logs are under the user's own local application data folder | Mitigated by OS permissions |
| Log files growing without bound | Daily rolling files, last 30 kept | Mitigated |
| Data kept longer than needed | No retention or deletion feature yet; the operator deletes data in the database when required | Accepted (out of scope for this version) |

## Out of scope

- Multi-user access control, an audit trail of who read or changed which record, encryption of data at rest
  inside the application, automated backups and retention policies.
