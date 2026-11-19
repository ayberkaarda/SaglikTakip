namespace SaglikTakip.UI.Theming;

/// <summary>Which palette the user asked for in the settings (<c>Appearance:Theme</c>).</summary>
public enum ThemeMode
{
    /// <summary>The light palette; the default.</summary>
    Light,

    /// <summary>The dark palette.</summary>
    Dark,

    /// <summary>Follows the Windows app mode (light or dark).</summary>
    System,
}
