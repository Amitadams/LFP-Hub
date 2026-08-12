using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LfpHub;

/// <summary>Persisted config under %LocalAppData%\LfpHub.</summary>
public sealed class AppConfig
{
    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string? TemplatesDirectory { get; set; }
    public TechIdentity Tech { get; set; } = new();

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
        if (!cfg.Tech.IsConfigured)
            cfg.Tech.TrySeedFromNova();
        return cfg;
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataDir);
        Tech ??= new TechIdentity();
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOpts));
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
        File.WriteAllText(TechIdentityPath, JsonSerializer.Serialize(payload, JsonOpts) + Environment.NewLine);
    }
}

public sealed class TechIdentity
{
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public string Site { get; set; } = "GFNV";
    public string SignatureTitle { get; set; } = "Tesla IT Support — GFNV";
    public string WalkupHours { get; set; } = "7:00 AM – 7:00 PM";

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
        return "";
    }

    /// <summary>One-time seed from Nova skills tech-identity.json when LFP Hub has none.</summary>
    public void TrySeedFromNova()
    {
        try
        {
            var nova = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Nova", ".opencode", "skills", "_shared", "tech-identity.json");
            if (!File.Exists(nova)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(nova));
            var r = doc.RootElement;
            if (r.TryGetProperty("displayName", out var dn) && string.IsNullOrWhiteSpace(DisplayName))
                DisplayName = dn.GetString() ?? "";
            if (r.TryGetProperty("email", out var em) && string.IsNullOrWhiteSpace(Email))
                Email = em.GetString() ?? "";
            if (r.TryGetProperty("username", out var un) && string.IsNullOrWhiteSpace(Username))
                Username = un.GetString() ?? "";
            if (r.TryGetProperty("site", out var site) && !string.IsNullOrWhiteSpace(site.GetString()))
                Site = site.GetString() ?? Site;
            if (r.TryGetProperty("signatureTitle", out var st) && !string.IsNullOrWhiteSpace(st.GetString()))
                SignatureTitle = st.GetString() ?? SignatureTitle;
            if (r.TryGetProperty("walkupHours", out var wh) && !string.IsNullOrWhiteSpace(wh.GetString()))
                WalkupHours = wh.GetString() ?? WalkupHours;
        }
        catch
        {
            /* ignore */
        }
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
