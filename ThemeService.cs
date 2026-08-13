using System.Windows;
using System.Windows.Media;

namespace LfpHub;

/// <summary>
/// Pushes a <see cref="LfpThemeInfo"/> into Application resources.
/// Mutates existing unfrozen <see cref="SolidColorBrush"/> instances so
/// DynamicResource (and shared StaticResource brush refs) update live.
/// </summary>
public static class ThemeService
{
    public static event Action? ThemeChanged;

    public static void Apply(string? themeId)
    {
        var theme = LfpThemes.Resolve(themeId);
        LfpThemes.SetCurrent(theme.Id);
        if (Application.Current?.Resources is not { } res) return;

        var p = theme.Palette;
        foreach (var (key, pick) in LfpThemes.BrushMap)
            Set(res, key, pick(p));

        ThemeChanged?.Invoke();
    }

    static void Set(ResourceDictionary res, string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        if (res[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }
        res[key] = new SolidColorBrush(color);
    }
}
