using System.Data;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using SaglikTakip.Data.Persistence.Migrations;

namespace SaglikTakip.Data.Persistence;

/// <summary>
/// Adopts a database created by the original application (Kullanicilar, SaglikKayitlari and
/// Egzersizler without an EF Core migrations history). The existing rows and identity values are
/// kept; the missing constraints, indexes and row versions of <see cref="InitialCreate"/> are added
/// and the migration is recorded as applied, so its sample data is never inserted into such a database.
/// </summary>
/// <remarks>
/// Every step checks the current schema first, and the whole upgrade runs in one transaction under
/// an application lock, so it is safe to run repeatedly and from concurrent processes.
/// </remarks>
internal static class LegacySchemaUpgrader
{
    private static readonly string[] LegacyTables = ["Kullanicilar", "SaglikKayitlari", "Egzersizler"];

    /// <summary>Identifier of the migration that a legacy database is upgraded to.</summary>
    internal static string BaselineMigrationId { get; } =
        typeof(InitialCreate).GetCustomAttribute<MigrationAttribute>()!.Id;

    /// <summary>
    /// Upgrades a legacy database in place. Returns <see langword="false"/> when there is nothing to
    /// adopt (no database yet, an empty database, or the baseline migration is already applied).
    /// </summary>
    public static async Task<bool> UpgradeIfLegacyAsync(HealthDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (!await IsLegacyAsync(db, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            db,
            static async (context, token) =>
            {
                await using var transaction = await context.Database.BeginTransactionAsync(token).ConfigureAwait(false);

                await AcquireUpgradeLockAsync(context, token).ConfigureAwait(false);

                // Another process may have finished the upgrade while this one waited for the lock.
                if (!await IsLegacyAsync(context, token).ConfigureAwait(false))
                {
                    await transaction.RollbackAsync(token).ConfigureAwait(false);
                    return false;
                }

                await EnsureAllTablesExistAsync(context, token).ConfigureAwait(false);
                await ValidateDataAsync(context, token).ConfigureAwait(false);

                foreach (var statement in UpgradeStatements)
                {
                    await ExecuteAsync(context, statement, token).ConfigureAwait(false);
                }

                await MarkBaselineAppliedAsync(context, token).ConfigureAwait(false);
                await transaction.CommitAsync(token).ConfigureAwait(false);
                return true;
            },
            verifySucceeded: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Takes the transaction scoped application lock that serializes upgrades. sp_getapplock reports
    /// failures through its return code instead of raising an error, so the code is checked and the
    /// upgrade stops before any schema statement runs when the lock was not granted.
    /// </summary>
    private static async Task AcquireUpgradeLockAsync(HealthDbContext db, CancellationToken cancellationToken)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
#pragma warning disable EF1002 // Constant statement; the only parameter is the output value.
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = N'SaglikTakip.LegacySchemaUpgrade', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 60000;",
            [result],
            cancellationToken).ConfigureAwait(false);
#pragma warning restore EF1002

        EnsureLockGranted(result.Value is int code ? code : null);
    }

    /// <summary>Throws when an sp_getapplock return code means the lock was not granted.</summary>
    internal static void EnsureLockGranted(int? returnCode)
    {
        if (returnCode is >= 0)
        {
            return;
        }

        var reason = returnCode switch
        {
            -1 => "the request timed out because another upgrade is still running",
            -2 => "the request was canceled",
            -3 => "the request was chosen as a deadlock victim",
            -999 => "the lock request was invalid",
            null => "the lock procedure returned no result",
            _ => "the lock procedure failed",
        };

        throw new InvalidOperationException(
            $"The legacy database upgrade lock could not be acquired ({reason}, sp_getapplock returned "
            + $"{(returnCode?.ToString(CultureInfo.InvariantCulture) ?? "null")}). No changes were made; try again later.");
    }

    private static async Task<bool> IsLegacyAsync(HealthDbContext db, CancellationToken cancellationToken)
    {
        var applied = await db.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false);
        if (applied.Contains(BaselineMigrationId, StringComparer.Ordinal))
        {
            return false;
        }

        return await CountExistingLegacyTablesAsync(db, cancellationToken).ConfigureAwait(false) > 0;
    }

    private static Task<int> CountExistingLegacyTablesAsync(HealthDbContext db, CancellationToken cancellationToken) =>
        ScalarAsync(
            db,
            "SELECT COUNT(*) AS [Value] FROM sys.tables WHERE schema_id = SCHEMA_ID(N'dbo') AND name IN (N'Kullanicilar', N'SaglikKayitlari', N'Egzersizler')",
            cancellationToken);

    private static async Task EnsureAllTablesExistAsync(HealthDbContext db, CancellationToken cancellationToken)
    {
        var count = await CountExistingLegacyTablesAsync(db, cancellationToken).ConfigureAwait(false);
        if (count != LegacyTables.Length)
        {
            throw new InvalidOperationException(
                "The database contains only part of the health tracking schema (Kullanicilar, SaglikKayitlari, Egzersizler) "
                + "and no migration history, so it cannot be upgraded automatically.");
        }
    }

    /// <summary>
    /// Refuses the upgrade, before anything is changed, when existing rows cannot satisfy the new
    /// constraints. Fixing such rows needs a human decision, so no data is altered or deleted here.
    /// Only counts are reported; row contents (personal data) never appear in the message. Empty texts are
    /// accepted because the original application allowed them; the services require them for new rows.
    /// </summary>
    private static async Task ValidateDataAsync(HealthDbContext db, CancellationToken cancellationToken)
    {
        (string Sql, string Problem)[] checks =
        [
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.Kullanicilar
             WHERE Ad IS NULL OR LEN(Ad) > 100
             """,
                "users with a missing name or a name longer than 100 characters"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.Kullanicilar
             WHERE Yas IS NULL OR Yas < 0 OR Yas > 150
             """,
                "users with a missing or out of range age"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.Kullanicilar
             WHERE Cinsiyet IS NULL OR Cinsiyet NOT IN (N'Erkek', N'Kadın', N'Diğer')
             """,
                "users with a missing or unknown gender"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.SaglikKayitlari s
             WHERE s.KullaniciId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Kullanicilar k WHERE k.Id = s.KullaniciId)
             """,
                "health records without an existing user"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.SaglikKayitlari
             WHERE Tarih IS NULL OR Kilo IS NULL OR Nabiz IS NULL
                OR Kilo < 0 OR Kilo > 200 OR Nabiz < 0 OR Nabiz > 250
             """,
                "health records with a missing or out of range date, weight or pulse"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.SaglikKayitlari
             WHERE LEN(Notlar) > 500
             """,
                "health records with notes longer than 500 characters"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.Egzersizler e
             WHERE e.KullaniciId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.Kullanicilar k WHERE k.Id = e.KullaniciId)
             """,
                "exercises without an existing user"),
            ("""
             SELECT COUNT(*) AS [Value] FROM dbo.Egzersizler
             WHERE EgzersizAdi IS NULL OR LEN(EgzersizAdi) > 100
                OR SureDakika IS NULL OR TekrarSayisi IS NULL
                OR SureDakika < 0 OR SureDakika > 1440 OR TekrarSayisi < 0 OR TekrarSayisi > 1000
             """,
                "exercises with a missing or invalid name, duration or repetition count"),
        ];

        var problems = new List<string>();
        foreach (var (sql, problem) in checks)
        {
            var count = await ScalarAsync(db, sql, cancellationToken).ConfigureAwait(false);
            if (count > 0)
            {
                problems.Add($"{count} {problem}");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The existing health tracking database cannot be upgraded until these rows are corrected: "
                + string.Join("; ", problems) + ". No changes were made.");
        }
    }

    /// <summary>
    /// Brings the legacy tables to the shape created by <see cref="InitialCreate"/>. Each statement
    /// is guarded so it only runs when the corresponding part of the schema is missing.
    /// </summary>
    private static readonly string[] UpgradeStatements =
    [
        // Foreign keys are dropped before their columns are altered and recreated under the model's names.
        DropForeignKeyIfUnnamed("SaglikKayitlari", "FK_SaglikKayitlari_Kullanicilar"),
        DropForeignKeyIfUnnamed("Egzersizler", "FK_Egzersizler_Kullanicilar"),

        AlterColumn("Kullanicilar", "Ad", "nvarchar", 200, "NVARCHAR(100) NOT NULL"),
        AlterColumn("Kullanicilar", "Yas", "int", 4, "INT NOT NULL"),
        AlterColumn("Kullanicilar", "Cinsiyet", "nvarchar", 20, "NVARCHAR(10) NOT NULL"),
        AlterColumn("SaglikKayitlari", "KullaniciId", "int", 4, "INT NOT NULL"),
        AlterColumn("SaglikKayitlari", "Tarih", "date", 3, "DATE NOT NULL"),
        AlterColumn("SaglikKayitlari", "Kilo", "float", 8, "FLOAT NOT NULL"),
        AlterColumn("SaglikKayitlari", "Nabiz", "int", 4, "INT NOT NULL"),
        AlterColumn("SaglikKayitlari", "Notlar", "nvarchar", 1000, "NVARCHAR(500) NULL", nullable: true),
        AlterColumn("Egzersizler", "KullaniciId", "int", 4, "INT NOT NULL"),
        AlterColumn("Egzersizler", "EgzersizAdi", "nvarchar", 200, "NVARCHAR(100) NOT NULL"),
        AlterColumn("Egzersizler", "SureDakika", "int", 4, "INT NOT NULL"),
        AlterColumn("Egzersizler", "TekrarSayisi", "int", 4, "INT NOT NULL"),

        RenamePrimaryKey("Kullanicilar", "PK_Kullanicilar"),
        RenamePrimaryKey("SaglikKayitlari", "PK_SaglikKayitlari"),
        RenamePrimaryKey("Egzersizler", "PK_Egzersizler"),

        AddForeignKey("SaglikKayitlari", "FK_SaglikKayitlari_Kullanicilar"),
        AddForeignKey("Egzersizler", "FK_Egzersizler_Kullanicilar"),

        AddCheck("Kullanicilar", "CK_Kullanicilar_Yas", SchemaLimits.AgeCheck),
        AddCheck("Kullanicilar", "CK_Kullanicilar_Cinsiyet", SchemaLimits.GenderCheck),
        AddCheck("SaglikKayitlari", "CK_SaglikKayitlari_Kilo", SchemaLimits.WeightCheck),
        AddCheck("SaglikKayitlari", "CK_SaglikKayitlari_Nabiz", SchemaLimits.PulseCheck),
        AddCheck("Egzersizler", "CK_Egzersizler_SureDakika", SchemaLimits.DurationCheck),
        AddCheck("Egzersizler", "CK_Egzersizler_TekrarSayisi", SchemaLimits.RepetitionsCheck),

        AddRowVersion("Kullanicilar"),
        AddRowVersion("SaglikKayitlari"),
        AddRowVersion("Egzersizler"),

        CreateIndex("Kullanicilar", "IX_Kullanicilar_Ad", "CREATE INDEX IX_Kullanicilar_Ad ON dbo.Kullanicilar (Ad)"),
        CreateIndex(
            "SaglikKayitlari",
            "IX_SaglikKayitlari_KullaniciId_Tarih",
            "CREATE INDEX IX_SaglikKayitlari_KullaniciId_Tarih ON dbo.SaglikKayitlari (KullaniciId, Tarih)"),
        CreateIndex("Egzersizler", "IX_Egzersizler_KullaniciId", "CREATE INDEX IX_Egzersizler_KullaniciId ON dbo.Egzersizler (KullaniciId)"),
    ];

    private static string AlterColumn(string table, string column, string typeName, int maxLength, string definition, bool nullable = false) =>
        $"""
        IF EXISTS (
            SELECT 1 FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id
            WHERE c.object_id = OBJECT_ID(N'dbo.{table}') AND c.name = N'{column}'
              AND (c.is_nullable <> {(nullable ? 1 : 0)} OR t.name <> N'{typeName}' OR c.max_length <> {maxLength}))
            EXEC(N'ALTER TABLE dbo.{table} ALTER COLUMN {column} {definition}');
        """;

    private static string RenamePrimaryKey(string table, string name) =>
        $"""
        DECLARE @current sysname = (
            SELECT name FROM sys.key_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.{table}') AND type = 'PK');
        IF @current IS NOT NULL AND @current <> N'{name}'
        BEGIN
            DECLARE @qualified nvarchar(300) = N'dbo.' + QUOTENAME(@current);
            EXEC sp_rename @objname = @qualified, @newname = N'{name}', @objtype = N'OBJECT';
        END
        """;

    /// <summary>Drops foreign keys to Kullanicilar that do not carry the model's constraint name.</summary>
    private static string DropForeignKeyIfUnnamed(string table, string name) =>
        $"""
        DECLARE @drop nvarchar(max) = N'';
        SELECT @drop += N'ALTER TABLE dbo.{table} DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';'
        FROM sys.foreign_keys fk
        WHERE fk.parent_object_id = OBJECT_ID(N'dbo.{table}')
          AND fk.referenced_object_id = OBJECT_ID(N'dbo.Kullanicilar')
          AND fk.name <> N'{name}';
        IF @drop <> N'' EXEC(@drop);
        """;

    /// <summary>Adds the named, non-cascading foreign key to Kullanicilar when it is missing.</summary>
    private static string AddForeignKey(string table, string name) =>
        $"""
        IF OBJECT_ID(N'dbo.{name}', N'F') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} WITH CHECK ADD CONSTRAINT {name} FOREIGN KEY (KullaniciId) REFERENCES dbo.Kullanicilar (Id)');
        """;

    private static string AddCheck(string table, string name, string condition) =>
        $"""
        IF OBJECT_ID(N'dbo.{name}', N'C') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} WITH CHECK ADD CONSTRAINT {name} CHECK ({condition.Replace("'", "''", StringComparison.Ordinal)})');
        """;

    private static string AddRowVersion(string table) =>
        $"""
        IF COL_LENGTH(N'dbo.{table}', N'{HealthDbContext.RowVersion}') IS NULL
            EXEC(N'ALTER TABLE dbo.{table} ADD {HealthDbContext.RowVersion} ROWVERSION');
        """;

    private static string CreateIndex(string table, string name, string createStatement) =>
        $"""
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.{table}') AND name = N'{name}')
            EXEC(N'{createStatement}');
        """;

    private static async Task MarkBaselineAppliedAsync(HealthDbContext db, CancellationToken cancellationToken)
    {
        var history = db.GetService<IHistoryRepository>();
        await ExecuteAsync(db, history.GetCreateIfNotExistsScript(), cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            db,
            history.GetInsertScript(new HistoryRow(BaselineMigrationId, ProductInfo.GetVersion())),
            cancellationToken).ConfigureAwait(false);
    }

    private static Task<int> ScalarAsync(HealthDbContext db, string sql, CancellationToken cancellationToken) =>
#pragma warning disable EF1002 // The statements are built from constants in this class, never from user input.
        db.Database.SqlQueryRaw<int>(sql).SingleAsync(cancellationToken);
#pragma warning restore EF1002

    private static Task<int> ExecuteAsync(HealthDbContext db, string sql, CancellationToken cancellationToken) =>
#pragma warning disable EF1002 // The statements are built from constants in this class, never from user input.
        db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
#pragma warning restore EF1002
}
