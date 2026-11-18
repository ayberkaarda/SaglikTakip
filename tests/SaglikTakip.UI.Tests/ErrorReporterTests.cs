using SaglikTakip.UI.Infrastructure;

namespace SaglikTakip.UI.Tests;

public sealed class ErrorReporterTests
{
    private const string Secret = "Server=prod-sql-01;User Id=ayse";

    [Fact]
    public void FormatForLog_WritesTypesCodesAndMethodsOnly()
    {
        var exception = Capture();

        var log = ErrorReporter.FormatForLog(exception);

        Assert.Contains(typeof(InvalidOperationException).FullName!, log, StringComparison.Ordinal);
        Assert.Contains(typeof(TimeoutException).FullName!, log, StringComparison.Ordinal);
        Assert.Contains("HResult 0x", log, StringComparison.Ordinal);
        Assert.Contains(nameof(ThrowInner), log, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, log, StringComparison.Ordinal);
        Assert.DoesNotContain("prod-sql", log, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs", log, StringComparison.Ordinal);
        Assert.DoesNotContain(":line", log, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_UnexpectedError_ShowsFixedMessageWithoutDetails()
    {
        var message = ErrorReporter.Describe(new InvalidOperationException(Secret));

        Assert.Equal(ErrorReporter.UnexpectedErrorMessage, message);
        Assert.DoesNotContain(Secret, message, StringComparison.Ordinal);
    }

    private static InvalidOperationException Capture()
    {
        try
        {
            try
            {
                ThrowInner();
            }
            catch (TimeoutException inner)
            {
                throw new InvalidOperationException("Outer " + Secret, inner);
            }
        }
        catch (InvalidOperationException outer)
        {
            return outer;
        }

        throw new InvalidOperationException("Not reached.");
    }

    private static void ThrowInner() => throw new TimeoutException("Inner " + Secret);
}
