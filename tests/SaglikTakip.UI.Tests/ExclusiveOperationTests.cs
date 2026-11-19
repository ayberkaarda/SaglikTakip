using SaglikTakip.UI.Infrastructure;
using static SaglikTakip.UI.Tests.Support.StaThread;

namespace SaglikTakip.UI.Tests;

public sealed class ExclusiveOperationTests
{
    [Fact]
    public void RunAsync_Failure_RestoresControlsAndAllowsNextRun() => Run(() =>
    {
        var operation = new ExclusiveOperation();
        using var enabled = new Button();
        using var disabled = new Button { Enabled = false };
        var pending = new TaskCompletionSource();

        var run = operation.RunAsync(() => pending.Task, enabled, disabled);
        Assert.True(operation.IsRunning);
        Assert.False(enabled.Enabled);

        pending.SetException(new InvalidOperationException("boom"));

        Assert.IsType<InvalidOperationException>(run.Exception?.InnerException);
        Assert.False(operation.IsRunning);
        Assert.True(enabled.Enabled);
        Assert.False(disabled.Enabled);

        var next = operation.RunAsync(() => Task.CompletedTask);
        Assert.True(next.IsCompletedSuccessfully && next.Result);
    });
}
