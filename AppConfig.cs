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
        ScrubLegacyPersonalIdentity();
        var cfg = Load();
        return !cfg.SetupComplete || !cfg.Tech.IsConfigured;
    }

    /// <summary>
    /// Drop leftover identity that still names a blocked previous tech
    /// so every install starts clean until the wizard runs.
    /// </summary>
    public static void ScrubLegacyPersonalIdentity()
    {
        try
        {
            foreach (var path in new[] { ConfigPath, TechIdentityPath })
            {
                if (!File.Exists(path)) continue;
                var text = File.ReadAllText(path);
                if (!TechIdentity.ContainsBlockedName(text)) continue;
                File.Delete(path);
            }

            // Working templates may have been saved with a personal signature expanded.
            if (Directory.Exists(DefaultWorkingTemplatesDir))
            {
                foreach (var file in Directory.EnumerateFiles(DefaultWorkingTemplatesDir, "*.md"))
                {
                    var body = File.ReadAllText(file);
                    if (!TechIdentity.ContainsBlockedName(body)) continue;
                    var name = Path.GetFileName(file);
                    var bundled = Path.Combine(BundledTemplatesDir, name);
                    if (File.Exists(bundled))
                        File.Copy(bundled, file, overwrite: true);
                    else
                        File.Delete(file);
                }
            }
        }
        catch
        {
            /* best-effort */
        }
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
        // Never auto-fill another person's identity.
        if (TechIdentity.ContainsBlockedName(cfg.Tech.DisplayName)
            || TechIdentity.ContainsBlockedName(cfg.Tech.Email)
            || TechIdentity.ContainsBlockedName(cfg.Tech.Username))
        {
            cfg.Tech = new TechIdentity();
            cfg.SetupComplete = false;
        }

        if (cfg.Tech.IsConfigured && !cfg.SetupComplete)
            cfg.SetupComplete = true;

        return cfg;
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataDir);
        Tech ??= new TechIdentity();
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts) + Environment.NewLine, utf8);
        WriteTechIdentityFile(Tech);
    }

    /// <summary>JSON the lfp-reset skill reads via --tech-identity.</summary>
    public static void WriteTechIdentityFile(TechIdentity tech)
    {
        Directory.CreateDirectory(AppDataDir);
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
    /// <summary>Names that must never ship as defaults or linger in local config.</summary>
    static readonly string[] BlockedNameFragments =
    [
        "amity adams",
        "amitadams",
        "amity.adams",
    ];

    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Site { get; set; } = "GFNV";
    public string SignatureTitle { get; set; } = "Tesla IT Support — GFNV";
    public string WalkupHours { get; set; } = "7:00 AM – 7:00 PM";

    [JsonIgnore]
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(DisplayName)
        && !string.IsNullOrWhiteSpace(Username)
        && !ContainsBlockedName(DisplayName)
        && !ContainsBlockedName(Username)
        && !ContainsBlockedName(Email);

    public static bool ContainsBlockedName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var n = text.ToLowerInvariant();
        foreach (var frag in BlockedNameFragments)
        {
            if (n.Contains(frag, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public string ValidationError()
    {
        if (string.IsNullOrWhiteSpace(DisplayName))
            return "Display name is required.";
        if (string.IsNullOrWhiteSpace(Username))
            return "Username is required.";
        if (ContainsBlockedName(DisplayName) || ContainsBlockedName(Username) || ContainsBlockedName(Email))
            return "Enter your own tech identity.";
        if (Username.Contains('@', StringComparison.Ordinal))
            return "Username only — no @tesla.com.";
        if (!string.IsNullOrWhiteSpace(Email)
            && !Regex.IsMatch(Email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            return "Email looks invalid.";
        return "";
    }
}

public static class TemplateFiles
{
    public const string Password = "LFP Password Reset.md";
    public const string Setup = "LFP Account Setup.md";
    public const string Mfa = "LFP MFA Reset.md";

    public static IReadOnlyList<string> AllNames { get; } = [Password, Setup, Mfa];

    public static string ForMode(string mode) => mode switch
    {
        "setup" => Setup,
        "mfa" => Mfa,
        _ => Password,
    };

    public static string Label(string fileName) => fileName switch
    {
        Password => "Password reset",
        Setup => "First login",
        Mfa => "MFA reset",
        _ => fileName,
    };
}
