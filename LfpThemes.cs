namespace LfpHub;

/// <summary>Built-in LFP Hub skins. Id persists in config.json.</summary>
public static class LfpThemes
{
    public const string LfpPlant = "lfp-plant";
    public const string CalmDark = "calm-dark";
    public const string CalmLight = "calm-light";
    public const string TeslaDark = "tesla-dark";
    public const string TeslaLight = "tesla-light";
    public const string ApertureDark = "aperture-dark";
    public const string Default = LfpPlant;

    public static IReadOnlyList<LfpThemeInfo> All { get; } =
    [
        new(LfpPlant, "LFP Plant",
            "Cool slate greens and phosphor accent - default LFP Hub look.",
            IsDark: true, Palettes.LfpPlant),
        new(CalmDark, "Calm Dark",
            "Warm charcoal panels and soft indigo. Easy on long shifts.",
            IsDark: true, Palettes.CalmDark),
        new(CalmLight, "Calm Light",
            "Soft gray canvas and indigo accent. Bright rooms and projectors.",
            IsDark: false, Palettes.CalmLight),
        new(TeslaDark, "Tesla Dark",
            "Near-black panels, white text, Tesla red.",
            IsDark: true, Palettes.TeslaDark),
        new(TeslaLight, "Tesla Light",
            "Light panels and Tesla red.",
            IsDark: false, Palettes.TeslaLight),
        new(ApertureDark, "Portal Aperture Dark",
            "Black lab panels and Aperture orange. The cake is not in the installer.",
            IsDark: true, Palettes.ApertureDark),
    ];

    public static LfpThemeInfo Current { get; private set; } = All[0];

    public static string Normalize(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return Default;
        foreach (var t in All)
        {
            if (t.Id.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase))
                return t.Id;
        }
        return Default;
    }

    public static LfpThemeInfo Resolve(string? id)
    {
        var key = Normalize(id);
        foreach (var t in All)
        {
            if (t.Id == key) return t;
        }
        return All[0];
    }

    public static void SetCurrent(string? id) => Current = Resolve(id);

    public static IReadOnlyList<(string Key, Func<LfpPalette, string> Pick)> BrushMap { get; } =
    [
        ("BgBrush", p => p.Bg),
        ("PanelBrush", p => p.Panel),
        ("PanelAltBrush", p => p.PanelAlt),
        ("BorderBrushKey", p => p.Border),
        ("TextBrush", p => p.Text),
        ("MutedBrush", p => p.Muted),
        ("DimBrush", p => p.Dim),
        ("AccentBrush", p => p.Accent),
        ("AccentDeepBrush", p => p.AccentDeep),
        ("OnAccentBrush", p => p.OnAccent),
        ("InputBgBrush", p => p.InputBg),
        ("InputBorderBrush", p => p.InputBorder),
        ("OkBrush", p => p.Ok),
        ("WarnBrush", p => p.Warn),
        ("DangerBrush", p => p.Danger),
        ("LogBgBrush", p => p.LogBg),
        ("SectionLabelBrush", p => p.SectionLabel),
        ("ChipBrush", p => p.Chip),
        ("ChipActiveBrush", p => p.ChipActive),
        ("ChipBorderActive", p => p.ChipBorderActive),
        ("ScrollThumbBrush", p => p.ScrollThumb),
        ("ScrollThumbHoverBrush", p => p.ScrollThumbHover),
        ("ScrollThumbDragBrush", p => p.ScrollThumbDrag),
    ];
}

public sealed record LfpThemeInfo(
    string Id,
    string Title,
    string Blurb,
    bool IsDark,
    LfpPalette Palette);

public sealed record LfpPalette(
    string Bg,
    string Panel,
    string PanelAlt,
    string Border,
    string Text,
    string Muted,
    string Dim,
    string Accent,
    string AccentDeep,
    string OnAccent,
    string InputBg,
    string InputBorder,
    string Ok,
    string Warn,
    string Danger,
    string LogBg,
    string SectionLabel,
    string Chip,
    string ChipActive,
    string ChipBorderActive,
    string ScrollThumb,
    string ScrollThumbHover,
    string ScrollThumbDrag);

file static class Palettes
{
    public static readonly LfpPalette LfpPlant = new(
        Bg: "#0F1412",
        Panel: "#171E1B",
        PanelAlt: "#1E2824",
        Border: "#2E3B35",
        Text: "#E8F0EC",
        Muted: "#9BB0A6",
        Dim: "#6B7F75",
        Accent: "#3DDC97",
        AccentDeep: "#2A9B6A",
        OnAccent: "#0A1210",
        InputBg: "#121916",
        InputBorder: "#3A4A43",
        Ok: "#5EE4A8",
        Warn: "#E8C26A",
        Danger: "#F08080",
        LogBg: "#0B100E",
        SectionLabel: "#7FCBAA",
        Chip: "#24302B",
        ChipActive: "#1A3D2E",
        ChipBorderActive: "#3DDC97",
        ScrollThumb: "#3A4A43",
        ScrollThumbHover: "#4E6359",
        ScrollThumbDrag: "#6B7F75");

    public static readonly LfpPalette CalmDark = new(
        Bg: "#12141A",
        Panel: "#1A1D26",
        PanelAlt: "#22262F",
        Border: "#343A46",
        Text: "#E8EAF0",
        Muted: "#A0A6B4",
        Dim: "#6F7686",
        Accent: "#6B8CFF",
        AccentDeep: "#4A66D4",
        OnAccent: "#FFFFFF",
        InputBg: "#161920",
        InputBorder: "#3E4554",
        Ok: "#5BD6A2",
        Warn: "#E6B84D",
        Danger: "#F07178",
        LogBg: "#0E1016",
        SectionLabel: "#8FA3FF",
        Chip: "#2C313C",
        ChipActive: "#1E2A44",
        ChipBorderActive: "#6B8CFF",
        ScrollThumb: "#4A5160",
        ScrollThumbHover: "#646C7E",
        ScrollThumbDrag: "#7E879A");

    public static readonly LfpPalette CalmLight = new(
        Bg: "#F0F2F5",
        Panel: "#FFFFFF",
        PanelAlt: "#F5F6F8",
        Border: "#D5D9E0",
        Text: "#1A1D26",
        Muted: "#5A6170",
        Dim: "#7A8294",
        Accent: "#4A66D4",
        AccentDeep: "#3A52B0",
        OnAccent: "#FFFFFF",
        InputBg: "#FFFFFF",
        InputBorder: "#C5CAD4",
        Ok: "#1B7A4E",
        Warn: "#9A6400",
        Danger: "#C62828",
        LogBg: "#FFFFFF",
        SectionLabel: "#4A66D4",
        Chip: "#EEF0F4",
        ChipActive: "#E8EEFF",
        ChipBorderActive: "#4A66D4",
        ScrollThumb: "#C5CAD4",
        ScrollThumbHover: "#A8AFBC",
        ScrollThumbDrag: "#8A92A2");

    public static readonly LfpPalette TeslaDark = new(
        Bg: "#0B0B0B",
        Panel: "#141414",
        PanelAlt: "#1C1C1C",
        Border: "#3A3A3A",
        Text: "#F5F5F5",
        Muted: "#C8C8C8",
        Dim: "#9A9A9A",
        Accent: "#E82127",
        AccentDeep: "#8B1519",
        OnAccent: "#FFFFFF",
        InputBg: "#111111",
        InputBorder: "#555555",
        Ok: "#5EEBB4",
        Warn: "#FFC95C",
        Danger: "#FF8A90",
        LogBg: "#0A0A0A",
        SectionLabel: "#FF8A90",
        Chip: "#2A2A2A",
        ChipActive: "#3A1518",
        ChipBorderActive: "#E82127",
        ScrollThumb: "#4A4A4A",
        ScrollThumbHover: "#6E6E6E",
        ScrollThumbDrag: "#8A8A8A");

    public static readonly LfpPalette TeslaLight = new(
        Bg: "#F3F3F3",
        Panel: "#FFFFFF",
        PanelAlt: "#F0F0F0",
        Border: "#D0D0D0",
        Text: "#1A1A1A",
        Muted: "#4A4A4A",
        Dim: "#6B6B6B",
        Accent: "#E82127",
        AccentDeep: "#B4141A",
        OnAccent: "#FFFFFF",
        InputBg: "#FFFFFF",
        InputBorder: "#BDBDBD",
        Ok: "#1B7A4E",
        Warn: "#9A6400",
        Danger: "#C62828",
        LogBg: "#FFFFFF",
        SectionLabel: "#E82127",
        Chip: "#F5F5F5",
        ChipActive: "#FDECEA",
        ChipBorderActive: "#E82127",
        ScrollThumb: "#BDBDBD",
        ScrollThumbHover: "#9E9E9E",
        ScrollThumbDrag: "#757575");

    public static readonly LfpPalette ApertureDark = new(
        Bg: "#0C0B09",
        Panel: "#161410",
        PanelAlt: "#1E1B16",
        Border: "#4A3A22",
        Text: "#F4EDE0",
        Muted: "#C4B59A",
        Dim: "#8A7D68",
        Accent: "#FF9A00",
        AccentDeep: "#C45A00",
        OnAccent: "#1A1200",
        InputBg: "#12100C",
        InputBorder: "#6A5428",
        Ok: "#7DDF6A",
        Warn: "#FFC857",
        Danger: "#FF6B4A",
        LogBg: "#0A0907",
        SectionLabel: "#FF9A00",
        Chip: "#2A241C",
        ChipActive: "#3A2808",
        ChipBorderActive: "#FF9A00",
        ScrollThumb: "#5A4A30",
        ScrollThumbHover: "#7A6438",
        ScrollThumbDrag: "#9A7A40");
}
