using SaglikTakip.Business.Services;
using SaglikTakip.Domain.Entities;

namespace SaglikTakip.UI.Infrastructure;

internal static class UiExtensions
{
    /// <summary>
    /// Runs an asynchronous UI action with a wait cursor. Business rule violations are shown as
    /// warnings; anything else is reported through <see cref="ErrorReporter"/>.
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
            ErrorReporter.Show(ex, form);
        }
        finally
        {
            form.UseWaitCursor = false;
        }
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
