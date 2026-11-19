using SaglikTakip.UI.Infrastructure;
using SaglikTakip.UI.Tests.Support;

[assembly: AssemblyFixture(typeof(NoDialogs))]
// The active theme is shared static state, and a test may switch it (high contrast); the suite takes about two seconds.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace SaglikTakip.UI.Tests.Support;

/// <summary>
/// Safety net for the whole test assembly: a message box would be a real window that waits for a
/// click, so every dialog request is answered with "No" and recorded instead of being shown.
/// </summary>
public sealed class NoDialogs : IDisposable
{
    private static readonly List<string> Requests = [];

    public NoDialogs()
    {
        Dialogs.Override = (text, caption, _, _) =>
        {
            lock (Requests)
            {
                Requests.Add(caption + ": " + text);
            }

            return DialogResult.No;
        };
    }

    /// <summary>Dialog requests made so far, for tests that check one was (or was not) asked.</summary>
    public static IReadOnlyList<string> Requested
    {
        get
        {
            lock (Requests)
            {
                return [.. Requests];
            }
        }
    }

    public void Dispose() => Dialogs.Override = null;
}
