# Changelog

🇬🇧 **English** · 🇹🇷 [Türkçe](CHANGELOG.tr.md)

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Trend charts ("Trend Grafikleri") for weight and pulse over a date range, drawn with GDI+, with count,
  minimum, maximum, average and a trailing moving average (1-90 days); same-day measurements are combined.
- Goals and alert limits ("Hedefler"): target and start weight, weight and pulse ranges (inclusive), a warning
  when a saved measurement is outside the limits and progress towards the target weight. New `Hedefler` table
  (migration `AddHealthGoals`).
- Reminders ("Hatırlatıcılar"): stored periodic measurement and exercise reminders, a balloon from the
  notification area while the application is open (checked every minute, no window is brought to the front)
  and a list of reminders missed while it was closed at startup. New `Hatirlaticilar` table (migration
  `AddReminders`).
- Weekly / monthly period report ("Dönem Raporu") with CSV export (UTF-8 with BOM, `;`, formula injection
  guard) and a printable, self-contained HTML report for "Save as PDF"; files are written to a temporary file
  and moved into place.
- Tests for moving average windows, empty and single-day ranges, same-day measurements, limits exactly on the
  boundary, goal progress, reminder due times, CSV escaping, atomic export, the new tables on SQLite and the
  legacy upgrade, and latest-wins loading and double-click guards on the new screens.
- Daily rolling file logs with Serilog under `%LOCALAPPDATA%\SaglikTakip\logs` (last 30 files kept).
  Health data (weights, pulses, notes), user names and exception messages are not logged.
- Central FluentValidation validators for user, health record and exercise input in the Business layer.
- Validator boundary tests (age 0/150, weight 1/200, pulse 1/250, future date, duration, repetitions, lengths)
  and service tests with strict Moq mocks.
- Repository and service tests on SQLite in-memory, including check constraints and foreign keys.
- Tests that check log files for health and personal data.
- Release job in CI: on `v*` tags a win-x64 build is published and uploaded as a zip artifact.
- English and Turkish README with architecture diagram, threat model (`docs/THREAT_MODEL.md`) and this changelog.

### Changed

- The main menu has four more buttons for the new screens.
- Saving a health record also checks the user's goal limits.
- The hand-written validation helper was replaced by the validators; messages, trimming and the order of the
  checks are unchanged.
- Errors that were written to the trace output are now written to the log file.

### Fixed

- The gender check constraint uses provider neutral literals outside SQL Server, so the schema can be created
  on SQLite. Migrations are unchanged.

## Layered .NET 10 foundation - 2026-11-19 (#1)

### Added

- Layered solution: `Domain`, `Business`, `Data` and `UI` projects under `src/`.
- Business services for users, health records, exercises and reports with validation rules.
- EF Core data access with entity configurations, the `InitialCreate` migration and fictional sample data;
  migrations are applied on startup.
- In-place upgrade of databases created by the original application, keeping their data; the upgrade stops
  when `sp_getapplock` does not grant its lock.
- Configuration from `appsettings.json`, user secrets and the `SAGLIKTAKIP_CONNECTION` environment variable;
  `appsettings.example.json` is committed instead of real settings.
- Global error handling with Turkish messages.
- Test projects for services, configuration and the legacy database upgrade.
- CI workflow that builds and tests on every push and pull request.

### Changed

- Converted to SDK-style projects targeting .NET 10 with nullable reference types.
- Forms call the Business services through dependency injection instead of running SQL directly.

### Fixed

- Repeated clicks on "Kaydet" (Save) no longer start duplicate saves.
- The report screen shows only the report of the latest selected user.
- Error dialogs show a fixed message; only exception types and methods are logged.

### Removed

- Tracked build output, packages and the database backup.
