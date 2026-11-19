namespace SaglikTakip.UI.Infrastructure;

/// <summary>
/// The single place that opens message boxes. Questions that need an answer and unexpected errors use
/// a message box; results and validation messages are shown inline (see <see cref="AppDialog"/>).
/// </summary>
internal static class Dialogs
{
    /// <summary>
    /// Replaces the message box, for example in the offscreen screenshot tool, which must never open a
    /// window. <see langword="null"/> in the application.
    /// </summary>
    internal static Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult>? Override { get; set; }

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        Override is { } replacement
            ? replacement(text, caption, buttons, icon)
            : MessageBox.Show(owner, text, caption, buttons, icon);

    /// <summary>A yes/no question; <see langword="true"/> when the user answers yes.</summary>
    public static bool Confirm(IWin32Window? owner, string text) =>
        Show(owner, text, "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
}
