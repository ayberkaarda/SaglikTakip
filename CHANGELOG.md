# Changelog

🇬🇧 **English** · 🇹🇷 [Türkçe](CHANGELOG.tr.md)

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Central theme for the user interface: color, type, spacing and corner radius tokens, a light palette by default
  and an optional dark palette or the Windows app mode (`Appearance:Theme` = `Light`, `Dark` or `System`); with
  Windows high contrast the system colors are used unchanged.
- Original application logo and icon (`assets/logo.svg`, rasterized into `assets/app.ico`).
- `tools/Screenshots`: renders the README images at 1x and 2x from invented sample data without opening a window,
  and generates the icon from the logo.
- Tests for palette contrast (WCAG), token scales, theme selection, building every screen without showing it,
  layout inside the window, tab order and inline messages.
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

- New design with a neutral "Sağlık Takip" identity: the government style name, background artwork and logo are
  gone. Every screen shares one frame (header, inline message strip, content cards, actions in the footer) in a
  standard resizable window with a minimum size, per-monitor DPI scaling, access keys and Esc to close.
- The main menu groups the screens as tiles under "Kayıtlar", "Takip ve analiz" and "Planlama".
- The trend chart uses the theme colors, a series pair that stays distinct for color vision deficiencies, a band
  for the goal range, round axis steps, marked out-of-range measurements, a label on the latest value and
  loading and empty states.
- Results and validation messages appear inside the window instead of message boxes and move the focus to the
  field to correct; confirmations and unexpected errors still use a dialog.
- The report screen shows its record lists, which were placed outside the window before, and the latest values
  as cards.
- New screenshots with kebab-case file names.
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
