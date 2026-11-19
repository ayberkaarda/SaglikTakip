using SaglikTakip.Business;
using SaglikTakip.UI.Infrastructure;

namespace SaglikTakip.UI.Tests;

/// <summary>What reaches the log file. No window is created by these tests.</summary>
public sealed class LoggingTests : IDisposable
{
    // Health and personal data as it could appear inside an exception message.
    private const string PersonalName = "Ayşe Yılmaz";
    private const string HealthData = "Kilo=72,5 Nabız=118 Notlar=tansiyon ilacı";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "sagliktakip-log-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void CreateLogger_WritesDailyFileInGivenDirectory()
    {
        using (var logger = AppLogging.CreateLogger(_directory))
        {
            logger.Information("probe");
        }

        var file = Assert.Single(Directory.GetFiles(_directory));
        Assert.Matches(@"sagliktakip-\d{8}\.log$", file);
        Assert.Contains("probe", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultDirectory_IsUnderLocalApplicationData()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(root, AppLogging.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("SaglikTakip", "logs"), AppLogging.DefaultDirectory, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Record_UnexpectedError_LogsTypesAndMethodsButNoHealthOrPersonalData()
    {
        var exception = Throw(new InvalidOperationException(
            $"Kayıt eklenemedi: {PersonalName} {HealthData}",
            new FormatException(HealthData)));

        var text = WriteAndRead(exception);

        Assert.Contains("[ERR] Operation failed", text, StringComparison.Ordinal);
        Assert.Contains(typeof(InvalidOperationException).FullName!, text, StringComparison.Ordinal);
        Assert.Contains(typeof(FormatException).FullName!, text, StringComparison.Ordinal);
        Assert.Contains(nameof(LoggingTests) + "." + nameof(Throw), text, StringComparison.Ordinal);
        Assert.DoesNotContain(PersonalName, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Nabız", text, StringComparison.Ordinal);
        Assert.DoesNotContain("tansiyon", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Kayıt eklenemedi", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Record_BusinessRule_LogsTheFixedMessageAsWarning()
    {
        var text = WriteAndRead(new BusinessRuleException("Nabız 1 ile 250 arasında olmalıdır."));

        Assert.Contains("[WRN] Business rule rejected an operation: Nabız 1 ile 250 arasında olmalıdır.", text, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private string WriteAndRead(Exception exception)
    {
        using (var logger = AppLogging.CreateLogger(_directory))
        {
            ErrorReporter.Record(logger, exception);
        }

        return File.ReadAllText(Assert.Single(Directory.GetFiles(_directory)));
    }

    private static Exception Throw(Exception exception)
    {
        try
        {
            throw exception;
        }
#pragma warning disable CA1031 // Test helper captures the stack trace.
        catch (Exception caught)
#pragma warning restore CA1031
        {
            return caught;
        }
    }
}
