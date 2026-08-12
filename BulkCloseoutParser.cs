using System.IO;
using System.Text;

namespace LfpHub;

/// <summary>One bulk close-out row with optional per-row ticket type.</summary>
public sealed class BulkCloseoutRow
{
    public required string Username { get; init; }
    public string TempPassword { get; init; } = "";
    public string Name { get; init; } = "";
    public string Badge { get; init; } = "";
    /// <summary>password | setup | mfa | noaccount</summary>
    public required string Mode { get; init; }
    public int LineNumber { get; init; }

    public string ModeLabel => BulkCloseoutParser.ModeLabel(Mode);
}

public sealed class BulkParseResult
{
    public List<BulkCloseoutRow> Rows { get; } = [];
    public List<string> Errors { get; } = [];
    public bool HadHeader { get; set; }
    public string? DetectedColumns { get; set; }
}

/// <summary>
/// Parse bulk close-out lists from CSV or free text.
/// Preferred header:
///   Username,Password,Name,Ticket Type,Badge
/// Ticket Type: Password | First login | MFA | No account
/// Without a type column, defaultMode is used for every row.
/// </summary>
public static class BulkCloseoutParser
{
    public const string SampleCsv =
        "Username,Password,Name,Ticket Type,Badge\r\n" +
        "jdoe,OneTimeP@ss14,Jane Doe,Password,\r\n" +
        "pdeyo,,Parker Deyo,MFA,702960\r\n" +
        "asmith,,,No account,\r\n";

    /// <param name="defaultMode">Used when a row has no Ticket Type column/value.</param>
    public static BulkParseResult Parse(string text, string defaultMode = "password") =>
        ParseLines(SplitLines(text), defaultMode);

    public static BulkParseResult ParseFile(string path, string defaultMode = "password") =>
        Parse(File.ReadAllText(path), defaultMode);

    static IEnumerable<string> SplitLines(string text)
    {
        using var reader = new StringReader(text ?? "");
        string? line;
        while ((line = reader.ReadLine()) is not null)
            yield return line;
    }

    public static BulkParseResult ParseLines(IEnumerable<string> lines, string defaultMode = "password")
    {
        defaultMode = NormalizeMode(defaultMode) ?? "password";
        var result = new BulkParseResult();
        var list = lines.ToList();
        var start = 0;
        var hasHeader = false;
        int userCol = 0, passCol = -1, nameCol = -1, badgeCol = -1, typeCol = -1;

        if (list.Count > 0)
        {
            var first = SplitFields(list[0]);
            if (LooksLikeHeader(first))
            {
                hasHeader = true;
                start = 1;
                userCol = IndexOfHeader(first,
                    "username", "user", "login", "samaccountname", "uid", "account");
                passCol = IndexOfHeader(first,
                    "password", "temp", "otp", "onetimepassword", "temp_password",
                    "temporary", "temppassword", "onetimepassword");
                nameCol = IndexOfHeader(first,
                    "name", "fullname", "display", "displayname", "fullname", "fullname");
                badgeCol = IndexOfHeader(first,
                    "badge", "badgenumber", "emp", "employee");
                typeCol = IndexOfHeader(first,
                    "tickettype", "type", "mode", "job", "ticket",
                    "closeout", "closeouttype", "requesttype");

                if (userCol < 0) userCol = 0;
                result.HadHeader = true;
                result.DetectedColumns = string.Join(", ", first.Where(f => !string.IsNullOrWhiteSpace(f)));
            }
        }

        for (var i = start; i < list.Count; i++)
        {
            var lineNo = i + 1;
            var raw = list[i].Trim();
            if (raw.Length == 0 || raw.StartsWith('#') || raw.StartsWith("//"))
                continue;

            var fields = SplitFields(raw);
            if (fields.Count == 0) continue;

            string user;
            string pass = "";
            string name = "";
            string badge = "";
            string typeRaw = "";

            if (hasHeader)
            {
                user = Get(fields, userCol);
                pass = passCol >= 0 ? Get(fields, passCol) : "";
                if (nameCol >= 0) name = Get(fields, nameCol);
                if (badgeCol >= 0) badge = Get(fields, badgeCol);
                if (typeCol >= 0) typeRaw = Get(fields, typeCol);
            }
            else if (fields.Count >= 2)
            {
                // Positional: username, password|type [, name] [, type|badge] ...
                user = fields[0];
                var second = fields[1];
                var secondMode = NormalizeMode(second);
                if (secondMode is not null && second.Length < 24 && !LooksLikePassword(second))
                {
                    typeRaw = second;
                    if (fields.Count >= 3) name = fields[2];
                    if (fields.Count >= 4) badge = fields[3];
                }
                else
                {
                    pass = second;
                    if (fields.Count >= 3)
                    {
                        var m3 = NormalizeMode(fields[2]);
                        if (m3 is not null)
                            typeRaw = fields[2];
                        else
                            name = fields[2];
                    }
                    if (fields.Count >= 4)
                    {
                        var m4 = NormalizeMode(fields[3]);
                        if (m4 is not null && string.IsNullOrEmpty(typeRaw))
                            typeRaw = fields[3];
                        else if (string.IsNullOrEmpty(name))
                            name = fields[3];
                        else
                            badge = fields[3];
                    }
                    if (fields.Count >= 5 && string.IsNullOrEmpty(badge))
                        badge = fields[4];
                }
            }
            else
            {
                user = fields[0];
            }

            user = NormalizeUsername(user);
            pass = pass.Trim().Trim('"');
            name = name.Trim().Trim('"');
            badge = badge.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(user))
            {
                result.Errors.Add($"Line {lineNo}: missing username.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(typeRaw) && NormalizeMode(typeRaw) is null)
            {
                result.Errors.Add(
                    $"Line {lineNo} ({user}): unknown ticket type '{typeRaw}'. " +
                    "Use Password, First login, MFA, or No account.");
                continue;
            }

            var mode = NormalizeMode(typeRaw) ?? defaultMode;
            var needsPass = TemplateFiles.NeedsTempPassword(mode);

            if (needsPass)
            {
                if (string.IsNullOrWhiteSpace(pass) || pass.Length < 8)
                {
                    result.Errors.Add(
                        $"Line {lineNo} ({user}): {ModeLabel(mode)} needs password (8+ characters).");
                    continue;
                }
                if (pass.Contains("007.teslamotors.com", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"Line {lineNo} ({user}): use one-time password, not a 007 link.");
                    continue;
                }
            }

            result.Rows.RemoveAll(r =>
                string.Equals(r.Username, user, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.Mode, mode, StringComparison.OrdinalIgnoreCase));

            result.Rows.Add(new BulkCloseoutRow
            {
                Username = user,
                TempPassword = needsPass ? pass : "",
                Name = name,
                Badge = badge,
                Mode = mode,
                LineNumber = lineNo,
            });
        }

        return result;
    }

    /// <summary>Map free text to mode id, or null if unknown.</summary>
    public static string? NormalizeMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = new string(raw.Trim().ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_')
            .ToArray())
            .Replace('_', ' ')
            .Replace('-', ' ');
        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);
        s = s.Trim();

        return s switch
        {
            "password" or "pwd" or "pass" or "pw" or "reset" or "password reset"
                or "lfp password" or "pwreset" => "password",
            "setup" or "first login" or "firstlogin" or "first time" or "firsttime"
                or "account setup" or "new account" or "new" => "setup",
            "mfa" or "authenticator" or "2fa" or "totp" or "mfa reset"
                or "mfa only" => "mfa",
            "noaccount" or "no account" or "not found" or "missing" or "none"
                or "no user" or "does not exist" or "doesnt exist" or "account not found"
                or "initial" or "create account" or "provision" => "noaccount",
            _ => null,
        };
    }

    public static string ModeLabel(string mode) => mode switch
    {
        "setup" => "First login",
        "mfa" => "MFA",
        "noaccount" => "No account",
        _ => "Password",
    };

    static bool LooksLikePassword(string s) =>
        s.Length >= 8 && (s.Any(char.IsDigit) || s.Any(c => !char.IsLetterOrDigit(c)));

    static bool LooksLikeHeader(List<string> fields)
    {
        if (fields.Count == 0) return false;
        var joined = string.Join(" ", fields).ToLowerInvariant();
        return joined.Contains("user")
               || joined.Contains("password")
               || joined.Contains("temp")
               || joined.Contains("otp")
               || joined.Contains("name")
               || joined.Contains("type")
               || joined.Contains("ticket")
               || joined.Contains("badge");
    }

    static int IndexOfHeader(List<string> fields, params string[] names)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            var f = NormalizeHeader(fields[i]);
            foreach (var n in names)
            {
                var nn = NormalizeHeader(n);
                if (f == nn || f.Replace("_", "") == nn.Replace("_", ""))
                    return i;
            }
        }
        return -1;
    }

    static string NormalizeHeader(string s) =>
        new string((s ?? "").Trim().ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

    static string Get(List<string> fields, int index) =>
        index >= 0 && index < fields.Count ? fields[index].Trim().Trim('"') : "";

    static string NormalizeUsername(string raw)
    {
        var u = (raw ?? "").Trim().Trim('"');
        if (u.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            u = u[7..];
        var at = u.IndexOf('@');
        if (at > 0) u = u[..at];
        return u.ToLowerInvariant();
    }

    public static List<string> SplitFields(string line)
    {
        var s = (line ?? "").Trim();
        if (s.Length == 0) return [];

        if (s.Contains('\t'))
            return SplitCsvLike(s, '\t');

        if (s.Contains(','))
            return SplitCsvLike(s, ',');

        var parts = new List<string>();
        var span = s.AsSpan().Trim();
        var i = 0;
        while (i < span.Length && !char.IsWhiteSpace(span[i])) i++;
        if (i == 0) return [];
        parts.Add(span[..i].ToString());
        var rest = span[i..].Trim().ToString();
        if (rest.Length > 0) parts.Add(rest);
        return parts;
    }

    static List<string> SplitCsvLike(string line, char sep)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }
            if (c == sep && !inQuotes)
            {
                fields.Add(sb.ToString().Trim());
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        fields.Add(sb.ToString().Trim());
        return fields;
    }
}
