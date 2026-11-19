namespace SaglikTakip.UI.Tests.Support;

/// <summary>
/// Runs a test body on a dedicated STA thread, as Windows Forms requires. No synchronization context
/// is installed, so continuations of awaited tasks that the test completes run inline on this thread.
/// Forms are only constructed, never shown, so no window appears.
/// </summary>
internal static class StaThread
{
    public static void Run(Action body)
    {
        ArgumentNullException.ThrowIfNull(body);

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            WindowsFormsSynchronizationContext.AutoInstall = false;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                body();
            }
#pragma warning disable CA1031 // Rethrown on the test thread below.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            throw new TimeoutException("The STA test body did not finish in time.");
        }

        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public static T Find<T>(Control root, string name)
        where T : Control =>
        root.Controls.Find(name, searchAllChildren: true).OfType<T>().Single();
}
