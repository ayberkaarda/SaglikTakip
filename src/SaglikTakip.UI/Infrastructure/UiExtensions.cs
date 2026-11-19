using SaglikTakip.Business;
using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;
using SaglikTakip.UI.Theming;
using Serilog;

namespace SaglikTakip.UI.Infrastructure;

internal static class UiExtensions
{
    /// <summary>
    /// Runs an asynchronous UI action with a wait cursor. Business rule violations are shown as
    /// warnings (inline when the form has a message strip); anything else is reported through
    /// <see cref="ErrorReporter"/>.
    /// </summary>
    public static async Task RunAsync(this Form form, Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(action);

        form.UseWaitCursor = true;
        try
        {
            await action();
        }
#pragma warning disable CA1031 // UI boundary: every failure is reported to the user.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (ex is BusinessRuleException && form is IStatusSurface surface)
            {
                ErrorReporter.Record(Log.Logger, ex);
                surface.ShowStatus(StatusKind.Warning, ex.Message);
            }
            else
            {
                ErrorReporter.Show(ex, form);
            }
        }
        finally
        {
            form.UseWaitCursor = false;
        }
    }

    /// <summary>
    /// Like <see cref="RunAsync"/>, but through <paramref name="operation"/>: while a run is pending a
    /// new call is ignored and <paramref name="controls"/> stay disabled, so a write cannot be started twice.
    /// </summary>
    public static Task<bool> RunExclusiveAsync(
        this Form form,
        ExclusiveOperation operation,
        Func<Task> action,
        params Control[] controls)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(action);

        return operation.RunAsync(() => form.RunAsync(action), controls);
    }

    /// <summary>Fills a user picker; items are <see cref="User"/> objects shown by their display name.</summary>
    public static async Task LoadUsersAsync(this ComboBox comboBox, IUserService userService)
    {
        ArgumentNullException.ThrowIfNull(comboBox);
        ArgumentNullException.ThrowIfNull(userService);

        var users = await userService.GetAllAsync();

        comboBox.BeginUpdate();
        try
        {
            comboBox.Items.Clear();
            foreach (var user in users)
            {
                comboBox.Items.Add(user);
            }
        }
        finally
        {
            comboBox.EndUpdate();
        }
    }

    /// <summary>Applies Turkish header texts and hides the columns that are not listed.</summary>
    public static void ConfigureColumns(this DataGridView grid, IReadOnlyDictionary<string, string> headers)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(headers);

        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (headers.TryGetValue(column.DataPropertyName, out var header))
            {
                column.HeaderText = header;
                column.Visible = true;
            }
            else
            {
                column.Visible = false;
            }
        }
    }
}
