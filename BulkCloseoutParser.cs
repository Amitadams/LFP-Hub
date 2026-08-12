using System.IO;
using System.Text;

namespace LfpHub;

/// <summary>One bulk close-out row: username + one-time password (+ optional name/badge).</summary>
public sealed class BulkCloseoutRow
{
    public required string Username { get; init; }
    public required string TempPassword { get; init; }
    public string Name { get; init; } = "";
    public string Badge { get; init; } = "";
    public int LineNumber { get; init; }
}

public sealed class BulkParseResult
{
    public List<BulkCloseoutRow> Rows { get; } = [];
    public List<string> Errors { get; } = [];
}

/// <summary>
/// Parse username + one-time password lists from CSV or free text.
/// Supported:
///   username,password
///   username,password,name,badge
///   username password
///   username\tpassword
/// Header row optional (username/user + password/temp/otp).
/// </summary>
public static class BulkCloseoutParser
{
    /// <param name="requirePassword">When false (MFA close-out), username-only rows are allowed.</param>
    public static BulkParseResult Parse(string text, bool requirePassword = true) =>
        ParseLines(SplitLines(text), requirePassword);

    public static BulkParseResult ParseFile(string path, bool requirePassword = true)
    {
        var text = File.ReadAllText(path);
        return Parse(text, requirePassword);
    }

    static IEnumerable<string> SplitLines(string text)
    {
        using var reader = new StringReader(text ?? "");
        string? line;
        while ((line = reader.ReadLine()) is not null)
            yield return line;
    }

    public static BulkParseResult ParseLines(IEnumerable<string> lines, bool requirePassword = true)
    {
        var result = new BulkParseResult();
        var list = lines.ToList();
        var start = 0;
        var hasHeader = false;
        int userCol = 0, passCol = 1, nameCol = -1, badgeCol = -1;

        if (list.Count > 0)
        {
            var first = SplitFields(list[0]);
            if (LooksLikeHeader(first))
            {
                hasHeader = true;
                start = 1;
                userCol = IndexOfHeader(first, "username", "user", "login", "samaccountname", "uid");
                passCol = IndexOfHeader(first, "password", "temp", "otp", "onetimepassword", "temp_password", "temporary");
                nameCol = IndexOfHeader(first, "name", "fullname", "display", "displayname");
                badgeCol = IndexOfHeader(first, "badge", "badgenumber", "emp", "employee");
                if (userCol < 0) userCol = 0;
                if (passCol < 0) passCol = first.Count > 1 ? 1 : -1;
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

            if (hasHeader)
            {
                user = Get(fields, userCol);
                pass = passCol >= 0 ? Get(fields, passCol) : "";
                if (nameCol >= 0) name = Get(fields, nameCol);
                if (badgeCol >= 0) badge = Get(fields, badgeCol);
            }
            else if (fields.Count >= 2)
            {
                user = fields[0];
                pass = fields[1];
                if (fields.Count >= 3) name = fields[2];
                if (fields.Count >= 4) badge = fields[3];
            }
            else if (!requirePassword && fields.Count == 1)
            {
                user = fields[0];
            }
            else
            {
                result.Errors.Add($"Line {lineNo}: need username and password.");
                continue;
            }

            user = NormalizeUsername(user);
            pass = pass.Trim().Trim('"');

            if (string.IsNullOrWhiteSpace(user))
            {
                result.Errors.Add($"Line {lineNo}: missing username.");
                continue;
            }
            if (requirePassword)
            {
                if (string.IsNullOrWhiteSpace(pass) || pass.Length < 8)
                {
                    result.Errors.Add($"Line {lineNo} ({user}): password missing or too short (8+).");
                    continue;
                }
                if (pass.Contains("007.teslamotors.com", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"Line {lineNo} ({user}): use one-time password, not a 007 link.");
                    continue;
                }
            }

            result.Rows.RemoveAll(r =>
                string.Equals(r.Username, user, StringComparison.OrdinalIgnoreCase));
            result.Rows.Add(new BulkCloseoutRow
            {
                Username = user,
                TempPassword = pass,
                Name = name.Trim(),
                Badge = badge.Trim(),
                LineNumber = lineNo,
            });
        }

        return result;
    }

    static bool LooksLikeHeader(List<string> fields)
    {
        if (fields.Count == 0) return false;
        var joined = string.Join(" ", fields).ToLowerInvariant();
        return joined.Contains("user") || joined.Contains("password") || joined.Contains("temp") || joined.Contains("otp");
    }

    static int IndexOfHeader(List<string> fields, params string[] names)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            var f = NormalizeHeader(fields[i]);
            foreach (var n in names)
            {
                if (f == n || f.Replace("_", "") == n.Replace("_", ""))
                    return i;
            }
        }
        return -1;
    }

    static string NormalizeHeader(string s) =>
        new string((s ?? "").Trim().ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

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

    /// <summary>CSV-ish split: commas, tabs, or whitespace (2+ tokens).</summary>
    public static List<string> SplitFields(string line)
    {
        var s = (line ?? "").Trim();
        if (s.Length == 0) return [];

        if (s.Contains('\t'))
            return SplitCsvLike(s, '\t');

        if (s.Contains(','))
            return SplitCsvLike(s, ',');

        // whitespace-separated: first token user, remainder is password (may contain spaces rarely)
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
