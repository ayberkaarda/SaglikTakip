using System.ComponentModel;

namespace SaglikTakip.UI.Infrastructure;

/// <summary>A number that can be left empty: the check box turns the value on or off.</summary>
internal sealed class OptionalNumberInput : FlowLayoutPanel
{
    private readonly CheckBox _enabled = new() { AutoSize = true, Margin = new Padding(3, 6, 0, 3) };
    private readonly NumericUpDown _number;

    public OptionalNumberInput(string name, decimal minimum, decimal maximum, int decimalPlaces, decimal initial)
    {
        Name = name;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        WrapContents = false;
        Margin = new Padding(0);
        _enabled.Name = name + "Enabled";
        _number = new NumericUpDown
        {
            Name = name + "Value",
            Minimum = minimum,
            Maximum = maximum,
            DecimalPlaces = decimalPlaces,
            Increment = decimalPlaces > 0 ? 0.5m : 1,
            Value = initial,
            Width = 80,
            Enabled = false,
        };
        _enabled.CheckedChanged += (_, _) => _number.Enabled = _enabled.Checked;
        Controls.Add(_enabled);
        Controls.Add(_number);
    }

    /// <summary>The entered number, or <see langword="null"/> when the value is switched off.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public decimal? Value
    {
        get => _enabled.Checked ? _number.Value : null;
        set
        {
            if (value is { } number)
            {
                _number.Value = Math.Clamp(number, _number.Minimum, _number.Maximum);
                _enabled.Checked = true;
            }
            else
            {
                _enabled.Checked = false;
            }
        }
    }

    public int? IntValue => Value is { } number ? (int)number : null;
}
