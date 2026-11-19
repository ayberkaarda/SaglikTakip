using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using SaglikTakip.Business;
using SaglikTakip.Business.Services;
using SaglikTakip.Data.Persistence;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.Data.Tests;

/// <summary>Runs against a real SQL Server instance; see <see cref="SqlServerTestDatabase"/>.</summary>
public sealed class LegacyUpgradeTests
{
    // Schema of the original application's database (read from its backup): nullable columns,
    // system-named primary and foreign keys, no indexes, no row versions, no migration history.
    internal const string LegacySchema = """
        CREATE TABLE dbo.Kullanicilar (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            Ad NVARCHAR(255) NOT NULL,
            Yas INT NULL,
            Cinsiyet NVARCHAR(50) NULL);
        CREATE TABLE dbo.SaglikKayitlari (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            KullaniciId INT NULL FOREIGN KEY REFERENCES dbo.Kullanicilar (Id),
            Tarih DATE NULL,
            Kilo FLOAT NULL,
            Nabiz INT NULL,
            Notlar NVARCHAR(MAX) NULL);
        CREATE TABLE dbo.Egzersizler (
            Id INT IDENTITY(1,1) PRIMARY KEY,
            KullaniciId INT NULL FOREIGN KEY REFERENCES dbo.Kullanicilar (Id),
            EgzersizAdi NVARCHAR(255) NULL,
            SureDakika INT NULL,
            TekrarSayisi INT NULL);
        """;

    // Identity values deliberately do not start at 1 so overwritten or re-seeded rows would show.
    private const string LegacyData = """
        SET IDENTITY_INSERT dbo.Kullanicilar ON;
        INSERT dbo.Kullanicilar (Id, Ad, Yas, Cinsiyet) VALUES
            (4, N'Eski Kullanici', 22, N'Erkek'),
            (8, N'Diger Kullanici', 19, N'Kadın'),
            (11, N'', 20, N'Erkek'); -- the original form accepted an empty name
        SET IDENTITY_INSERT dbo.Kullanicilar OFF;
        SET IDENTITY_INSERT dbo.SaglikKayitlari ON;
        INSERT dbo.SaglikKayitlari (Id, KullaniciId, Tarih, Kilo, Nabiz, Notlar) VALUES
            (5, 4, '2025-05-01', 0, 0, N''),
            (6, 4, '2025-05-20', 78, 90, N'Son kayit'),
            (9, 8, '2025-05-10', 55, 70, NULL);
        SET IDENTITY_INSERT dbo.SaglikKayitlari OFF;
        SET IDENTITY_INSERT dbo.Egzersizler ON;
        INSERT dbo.Egzersizler (Id, KullaniciId, EgzersizAdi, SureDakika, TekrarSayisi) VALUES
            (3, 4, N'Kosu', 30, 15),
            (7, 8, N'Yuzme', 45, 20);
        SET IDENTITY_INSERT dbo.Egzersizler OFF;
        """;

    [Fact]
    public async Task Initialize_LegacyDatabase_PreservesDataAndEnablesOperations()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(LegacyData, ct);

        await using var provider = database.BuildServices();
        var initializer = provider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeAsync(ct);
        await initializer.InitializeAsync(ct); // Second start must be a no-op.

        var users = provider.GetRequiredService<IUserService>();
        var reports = provider.GetRequiredService<IReportService>();

        // Existing rows and identities are kept; no sample data was added.
        Assert.Equal([4, 8, 11], (await users.GetAllAsync(ct)).Select(u => u.Id));
        var report = await reports.GetUserReportAsync(4, ct);
        Assert.Equal([6, 5], report.HealthRecords.Select(r => r.Id));
        Assert.Equal("Son kayit", report.LatestHealthRecord?.Notes);
        Assert.Equal("Kosu", report.LatestExercise?.Name);
        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId LIKE N'%_InitialCreate'",
            ct));

        await AssertUpgradedSchemaAsync(database, ct);

        // New rows work on the upgraded schema and continue after the existing identities.
        var user = await users.AddAsync("Yeni Kullanici", 30, Genders.Other, ct);
        Assert.True(user.Id > 11);
        var record = await provider.GetRequiredService<IHealthRecordService>()
            .AddAsync(user.Id, new DateTime(2025, 6, 1), 70, 72, "Not", ct);
        Assert.True(record.Id > 9);
        var exercise = await provider.GetRequiredService<IExerciseService>().AddAsync(user.Id, "Bisiklet", 20, 0, ct);
        Assert.True(exercise.Id > 7);

        // The database itself refuses rows that break the new constraints.
        var orphan = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            "INSERT dbo.SaglikKayitlari (KullaniciId, Tarih, Kilo, Nabiz) VALUES (@userId, '2025-01-01', 70, 70)",
            ct,
            ("@userId", 999)));
        Assert.Contains("FK_SaglikKayitlari_Kullanicilar", orphan.Message, StringComparison.Ordinal);
        var gender = await Assert.ThrowsAsync<SqlException>(() => database.ExecuteAsync(
            "INSERT dbo.Kullanicilar (Ad, Yas, Cinsiyet) VALUES (N'X', 30, N'?')",
            ct));
        Assert.Contains("CK_Kullanicilar_Cinsiyet", gender.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Initialize_LegacyDatabaseWithInvalidRows_RefusesWithoutChanges()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.CreateAsync(ct);
        await database.ExecuteAsync(LegacySchema, ct);
        await database.ExecuteAsync(
            """
            INSERT dbo.Kullanicilar (Ad, Yas, Cinsiyet) VALUES (N'Yassiz', NULL, N'Erkek');
            INSERT dbo.Egzersizler (KullaniciId, EgzersizAdi, SureDakika, TekrarSayisi) VALUES (NULL, N'Sahipsiz', 10, 1);
            """,
            ct);

        await using var provider = database.BuildServices();
        var error = await Assert.ThrowsAsync<DatabaseInitializationException>(
            () => provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct));

        Assert.Contains("missing or out of range age", error.InnerException?.Message, StringComparison.Ordinal);
        Assert.Contains("exercises without an existing user", error.InnerException?.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Yassiz", error.InnerException?.Message, StringComparison.Ordinal);
        Assert.Equal(0, await database.ScalarAsync("SELECT COUNT(*) FROM sys.tables WHERE name = N'__EFMigrationsHistory'", ct));
        Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Kullanicilar') AND name = N'Yas' AND is_nullable = 1", ct));
    }

    [Fact]
    public async Task Initialize_EmptyDatabase_AppliesMigrationWithSampleData()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);

        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        var users = await provider.GetRequiredService<IUserService>().GetAllAsync(ct);
        Assert.Equal([1, 2, 3], users.Select(u => u.Id));
        var report = await provider.GetRequiredService<IReportService>().GetUserReportAsync(1, ct);
        Assert.Equal(new DateTime(2025, 4, 7), report.LatestHealthRecord?.Date);
        await AssertUpgradedSchemaAsync(database, ct);
    }

    /// <summary>
    /// Restores a backup made by the original application (path in SAGLIKTAKIP_LEGACY_BACKUP, readable
    /// by the SQL Server service) and upgrades it. Only counts and identities are compared, so no
    /// personal data from the backup ends up in test output.
    /// </summary>
    [Fact]
    public async Task Initialize_RestoredLegacyBackup_KeepsAllRows()
    {
        var backup = Environment.GetEnvironmentVariable("SAGLIKTAKIP_LEGACY_BACKUP");
        if (string.IsNullOrWhiteSpace(backup))
        {
            Assert.Skip("SAGLIKTAKIP_LEGACY_BACKUP is not set.");
        }

        var ct = TestContext.Current.CancellationToken;
        await using var database = await SqlServerTestDatabase.ReserveAsync(ct);
        await RestoreAsync(database, backup, ct);

        const string Snapshot = """
            SELECT CHECKSUM_AGG(CHECKSUM(*)) FROM (
                SELECT 'K' AS T, COUNT(*) AS C, MAX(Id) AS M FROM dbo.Kullanicilar
                UNION ALL SELECT 'S', COUNT(*), MAX(Id) FROM dbo.SaglikKayitlari
                UNION ALL SELECT 'E', COUNT(*), MAX(Id) FROM dbo.Egzersizler) s
            """;
        const string Content = """
            SELECT CHECKSUM_AGG(CHECKSUM(Id, KullaniciId, Tarih, Kilo, Nabiz, Notlar)) FROM dbo.SaglikKayitlari
            """;
        var countsBefore = await database.ScalarAsync(Snapshot, ct);
        var contentBefore = await database.ScalarAsync(Content, ct);
        var usersBefore = await database.ScalarAsync("SELECT COUNT(*) FROM dbo.Kullanicilar", ct);

        await using var provider = database.BuildServices();
        await provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync(ct);

        Assert.Equal(countsBefore, await database.ScalarAsync(Snapshot, ct));
        Assert.Equal(contentBefore, await database.ScalarAsync(Content, ct));
        Assert.Equal(usersBefore, (await provider.GetRequiredService<IUserService>().GetAllAsync(ct)).Count);
        await AssertUpgradedSchemaAsync(database, ct);
    }

    private static async Task AssertUpgradedSchemaAsync(SqlServerTestDatabase database, CancellationToken ct)
    {
        string[] expected =
        [
            "PK_Kullanicilar", "PK_SaglikKayitlari", "PK_Egzersizler",
            "FK_SaglikKayitlari_Kullanicilar", "FK_Egzersizler_Kullanicilar",
            "CK_Kullanicilar_Yas", "CK_Kullanicilar_Cinsiyet", "CK_SaglikKayitlari_Kilo",
            "CK_SaglikKayitlari_Nabiz", "CK_Egzersizler_SureDakika", "CK_Egzersizler_TekrarSayisi",
        ];
        foreach (var name in expected)
        {
            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.objects WHERE name = @name", ct, ("@name", name)));
        }

        Assert.Equal(3, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE name IN (N'IX_Kullanicilar_Ad', N'IX_SaglikKayitlari_KullaniciId_Tarih', N'IX_Egzersizler_KullaniciId')",
            ct));

        // Later migrations run on top of the adopted baseline.
        foreach (var name in new[] { "PK_Hedefler", "FK_Hedefler_Kullanicilar", "CK_Hedefler_NabizAraligi" })
        {
            Assert.Equal(1, await database.ScalarAsync("SELECT COUNT(*) FROM sys.objects WHERE name = @name", ct, ("@name", name)));
        }

        Assert.Equal(1, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.indexes WHERE name = N'IX_Hedefler_KullaniciId' AND is_unique = 1",
            ct));
        Assert.Equal(3, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.name = N'RowVersion' AND t.name = N'timestamp' AND c.object_id IN (OBJECT_ID(N'dbo.Kullanicilar'), OBJECT_ID(N'dbo.SaglikKayitlari'), OBJECT_ID(N'dbo.Egzersizler'))",
            ct));
        Assert.Equal(0, await database.ScalarAsync(
            "SELECT COUNT(*) FROM sys.columns WHERE object_id IN (OBJECT_ID(N'dbo.SaglikKayitlari'), OBJECT_ID(N'dbo.Egzersizler')) AND name = N'KullaniciId' AND is_nullable = 1",
            ct));
    }

    private static async Task RestoreAsync(SqlServerTestDatabase database, string backup, CancellationToken ct)
    {
        await SqlServerTestDatabase.ExecuteAsync(
            database.Server,
            """
            DECLARE @files TABLE (
                LogicalName nvarchar(128), PhysicalName nvarchar(260), Type char(1), FileGroupName nvarchar(128), Size numeric(20,0),
                MaxSize numeric(20,0), FileId bigint, CreateLSN numeric(25,0), DropLSN numeric(25,0), UniqueId uniqueidentifier,
                ReadOnlyLSN numeric(25,0), ReadWriteLSN numeric(25,0), BackupSizeInBytes bigint, SourceBlockSize int, FileGroupId int,
                LogGroupGUID uniqueidentifier, DifferentialBaseLSN numeric(25,0), DifferentialBaseGUID uniqueidentifier, IsReadOnly bit,
                IsPresent bit, TDEThumbprint varbinary(32), SnapshotUrl nvarchar(360));
            INSERT @files EXEC sp_executesql N'RESTORE FILELISTONLY FROM DISK = @p', N'@p nvarchar(4000)', @p = @backup;
            DECLARE @data nvarchar(128) = (SELECT LogicalName FROM @files WHERE Type = 'D');
            DECLARE @log nvarchar(128) = (SELECT LogicalName FROM @files WHERE Type = 'L');
            DECLARE @path nvarchar(400) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400));
            DECLARE @mdf nvarchar(500) = @path + @name + N'.mdf';
            DECLARE @ldf nvarchar(500) = @path + @name + N'_log.ldf';
            RESTORE DATABASE @name FROM DISK = @backup WITH MOVE @data TO @mdf, MOVE @log TO @ldf, RECOVERY;
            """,
            ct,
            ("@name", database.Name),
            ("@backup", backup));
    }
}
