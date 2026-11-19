using SaglikTakip.Data.Persistence;

namespace SaglikTakip.Data.Tests;

/// <summary>sp_getapplock return codes; no database is needed.</summary>
public sealed class UpgradeLockTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void EnsureLockGranted_AcceptsGrantedCodes(int code)
    {
        LegacySchemaUpgrader.EnsureLockGranted(code);
    }

    [Theory]
    [InlineData(-1, "timed out")]
    [InlineData(-2, "canceled")]
    [InlineData(-3, "deadlock")]
    [InlineData(-999, "invalid")]
    [InlineData(null, "no result")]
    public void EnsureLockGranted_RejectsFailureCodes(int? code, string reason)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => LegacySchemaUpgrader.EnsureLockGranted(code));

        Assert.Contains(reason, ex.Message, StringComparison.Ordinal);
        Assert.Contains("No changes were made", ex.Message, StringComparison.Ordinal);
    }
}
