namespace SaglikTakip.UI.Infrastructure;

/// <summary>
/// Guards a UI operation (such as a save) so it cannot run again while a previous run is still
/// pending. The "running" flag is set synchronously, before the first await, so a second click that
/// arrives while the first one awaits is ignored instead of starting a duplicate write.
/// </summary>
/// <remarks>
/// Intended for the UI thread only; it has no dependencies besides Windows Forms and can be copied
/// into other projects as is.
/// </remarks>
public sealed class ExclusiveOperation
{
    /// <summary>Whether an operation started through <see cref="RunAsync"/> has not finished yet.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Runs <paramref name="action"/> unless another run is pending. The given controls are disabled
    /// for the duration and their previous <see cref="Control.Enabled"/> state is restored afterwards,
    /// also when the action fails. Exceptions are propagated to the caller.
    /// </summary>
    /// <returns><see langword="false"/> when the call was ignored because a run is pending.</returns>
    public async Task<bool> RunAsync(Func<Task> action, params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(controls);

        if (IsRunning)
        {
            return false;
        }

        IsRunning = true;
        var previousStates = new bool[controls.Length];
        try
        {
            for (var i = 0; i < controls.Length; i++)
            {
                previousStates[i] = controls[i].Enabled;
                controls[i].Enabled = false;
            }

            await action().ConfigureAwait(true);
            return true;
        }
        finally
        {
            for (var i = 0; i < controls.Length; i++)
            {
                if (!controls[i].IsDisposed)
                {
                    controls[i].Enabled = previousStates[i];
                }
            }

            IsRunning = false;
        }
    }
}
