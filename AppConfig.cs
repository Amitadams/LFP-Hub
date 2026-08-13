using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LfpHub;

/// <summary>Persisted config under %LocalAppData%\LfpHub.</summary>
public sealed class AppConfig
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string? TemplatesDirectory { get; set; }
    public TechIdentity Tech { get; set; } = new();
    /// <summary>True after first-time setup wizard completes.</summary>
    public bool SetupComplete { get; set; }
    /// <summary>Theme id from <see cref="LfpThemes"/> (e.g. lfp-plant).</summary>
    public string Theme { get; set; } = LfpThemes.Default;

    public static string AppDataDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LfpHub");

    public static string ConfigPath => Path.Combine(AppDataDir, "config.json");

    public static string TechIdentityPath => Path.Combine(AppDataDir, "tech-identity.json");

    public static string BundledTemplatesDir =>
        Path.Combine(AppContext.BaseDirectory, "Templates");

    public static string DefaultWorkingTemplatesDir =>
        Path.Combine(AppDataDir, "Templates");

    public static bool NeedsFirstRunSetup()
    {
        var cfg = Load();
        return !cfg.SetupComplete || !cfg.Tech.IsConfigured;
    }

    public static string ResolveTemplatesDir(AppConfig? cfg = null)
    {
        cfg ??= Load();
        if (!string.IsNullOrWhiteSpace(cfg.TemplatesDirectory)
            && Directory.Exists(cfg.TemplatesDirectory))
            return Path.GetFullPath(cfg.TemplatesDirectory);

        EnsureWorkingTemplates();
        return DefaultWorkingTemplatesDir;
    }

    public static void EnsureWorkingTemplates()
    {
        Directory.CreateDirectory(DefaultWorkingTemplatesDir);
        foreach (var name in TemplateFiles.AllNames)
        {
            var dest = Path.Combine(DefaultWorkingTemplatesDir, name);
            if (File.Exists(dest)) continue;
            var src = Path.Combine(BundledTemplatesDir, name);
            if (File.Exists(src))
                File.Copy(src, dest, overwrite: false);
        }
    }

    public static AppConfig Load()
    {
        AppConfig cfg;
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
            else
            {
                cfg = new AppConfig();
            }
        }
        catch
        {
            cfg = new AppConfig();
        }

        cfg.Tech ??= new TechIdentity();
        cfg.Theme = LfpThemes.Normalize(cfg.Theme);

        if (cfg.Tech.IsConfigured && !cfg.SetupComplete)
            cfg.SetupComplete = true;

        return cfg;
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataDir);
        Tech ??= new TechIdentity();
        Tech.NormalizeAsciiPunctuation();
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts) + Environment.NewLine, utf8);
        WriteTechIdentityFile(Tech);
    }

    /// <summary>JSON the lfp-reset skill reads via --tech-identity.</summary>
    public static void WriteTechIdentityFile(TechIdentity tech)
    {
        Directory.CreateDirectory(AppDataDir);
        tech.NormalizeAsciiPunctuation();
        var payload = new
        {
            displayName = tech.DisplayName?.Trim() ?? "",
            email = tech.Email?.Trim().ToLowerInvariant() ?? "",
            username = tech.Username?.Trim().ToLowerInvariant() ?? "",
            site = tech.Site?.Trim() ?? "GFNV",
            signatureTitle = tech.SignatureTitle?.Trim() ?? "",
            walkupHours = tech.WalkupHours?.Trim() ?? "",
        };
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(
            TechIdentityPath,
            JsonSerializer.Serialize(payload, JsonOpts) + Environment.NewLine,
            utf8);
    }
}

public sealed class TechIdentity
{
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Site { get; set; } = "GFNV";
    public string SignatureTitle { get; set; } = "Tesla IT Support - GFNV";
    public string WalkupHours { get; set; } = "7:00 AM - 7:00 PM";

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(DisplayName)
        && !string.IsNullOrWhiteSpace(Username);

    public string ValidationError()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
            return "Display name is required.";
        if (string.IsNullOrWhiteSpace(Username))
            return "Username is required.";
        if (Username.Contains('@', StringComparison.Ordinal))
            return "Username only - no @tesla.com.";
        if (!string.IsNullOrWhiteSpace(Email)
            && !Regex.IsMatch(Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return "Email looks invalid.";
        return "";
    }

    /// <summary>
    /// Keep ticket signatures ASCII-safe (Teams/Jira often mojibake curly dashes).
    /// </summary>
    public void NormalizeAsciiPunctuation()
    {
        DisplayName = ToAsciiPunct(DisplayName);
        Email = ToAsciiPunct(Email);
        Username = ToAsciiPunct(Username);
        Site = ToAsciiPunct(Site);
        SignatureTitle = ToAsciiPunct(SignatureTitle);
        WalkupHours = ToAsciiPunct(WalkupHours);
    }

    static string ToAsciiPunct(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        return s
            .Replace('\u2014', '-') // em dash
            .Replace('\u2013', '-') // en dash
            .Replace('\u2012', '-')
            .Replace('\u2010', '-')
            .Replace('\u00A0', ' ')
            .Replace('\u2026', '.')
            .Replace("...", "...")
            .Replace('\u2018', '\'')
            .Replace('\u2019', '\'')
            .Replace('\u201C', '"')
            .Replace('\u201D', '"')
            .Replace('\u00B7', '-');
    }
}

public static class TemplateFiles
{
    public const string Password = "LFP Password Reset.md";
    public const string Setup = "LFP Account Setup.md";
    public const string Mfa = "LFP MFA Reset.md";
    public const string NoAccount = "LFP No Account.md";

    public static IReadOnlyList<string> AllNames { get; } =
        [Password, Setup, Mfa, NoAccount];

    public static string ForMode(string mode) => mode switch
    {
        "setup" => Setup,
        "mfa" => Mfa,
        "noaccount" => NoAccount,
        _ => Password,
    };

    /// <summary>Modes that do not need an Azure one-time password.</summary>
    public static bool NeedsTempPassword(string mode) =>
        mode is not "mfa" and not "noaccount";

    public static string Label(string fileName) => fileName switch
    {
        Password => "Password reset",
        Setup => "First login",
        Mfa => "MFA reset",
        NoAccount => "No account",
        _ => fileName,
    };
}
