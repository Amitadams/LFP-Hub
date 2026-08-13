using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace LfpHub;

/// <summary>Editable bulk grid row (bound to DataGrid).</summary>
public sealed class BulkGridRow : INotifyPropertyChanged
{
    string _username = "";
    string _displayName = "";
    string _password = "";
    string _ita = "";
    string _typeLabel = "Password";

    public string Username
    {
        get => _username;
        set { if (_username == value) return; _username = value ?? ""; OnPropertyChanged(); }
    }

    public string DisplayName
    {
        get => _displayName;
        set { if (_displayName == value) return; _displayName = value ?? ""; OnPropertyChanged(); }
    }

    public string Password
    {
        get => _password;
        set { if (_password == value) return; _password = value ?? ""; OnPropertyChanged(); }
    }

    public string Ita
    {
        get => _ita;
        set { if (_ita == value) return; _ita = value ?? ""; OnPropertyChanged(); }
    }

    /// <summary>UI label: Password, First login, MFA, No account.</summary>
    public string TypeLabel
    {
        get => _typeLabel;
        set
        {
            var raw = string.IsNullOrWhiteSpace(value) ? "Password" : value.Trim();
            var mode = BulkCloseoutParser.NormalizeMode(raw);
            var v = mode is not null ? BulkCloseoutParser.ModeLabel(mode) : "Password";
            if (_typeLabel == v) return;
            _typeLabel = v;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Mode));
        }
    }

    public string Mode => BulkCloseoutParser.NormalizeMode(TypeLabel) ?? "password";

    public string ModeLabel => BulkCloseoutParser.ModeLabel(Mode);

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Username)
        && string.IsNullOrWhiteSpace(DisplayName)
        && string.IsNullOrWhiteSpace(Password)
        && string.IsNullOrWhiteSpace(Ita);

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Validated row ready to run.</summary>
public sealed class BulkCloseoutRow
{
    public required string Username { get; init; }
    public string TempPassword { get; init; } = "";
    public string Name { get; init; } = "";
    public string Badge { get; init; } = "";
    public string Ita { get; init; } = "";
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
/// Parse bulk close-out lists from CSV or free text into grid rows / run rows.
/// Headers: Username, Display Name, Password, ITA Number, Type
/// Type: Password | First login | MFA | No account
/// </summary>
public static class BulkCloseoutParser
{
    public static readonly string[] TypeLabels =
    [
        "Password",
        "First login",
        "MFA",
        "No account",
    ];

    public const string SampleCsv =
        "Username,Display Name,Password,ITA Number,Type\r\n" +
        "jdoe,Jane Doe,OneTimeP@ss14,,Password\r\n" +
        "pdeyo,Parker Deyo,,,MFA\r\n" +
        "asmith,Alex Smith,,,No account\r\n";

    public static ObservableCollection<BulkGridRow> CreateEmptyGrid(int blankRows = 8)
    {
        var list = new ObservableCollection<BulkGridRow>();
        for (var i = 0; i < blankRows; i++)
            list.Add(new BulkGridRow());
        return list;
    }

    public static ObservableCollection<BulkGridRow> SampleGrid()
    {
        var list = new ObservableCollection<BulkGridRow>
        {
            new()
            {
                Username = "jdoe",
                DisplayName = "Jane Doe",
                Password = "OneTimeP@ss14",
                TypeLabel = "Password",
            },
            new()
            {
                Username = "pdeyo",
                DisplayName = "Parker Deyo",
                TypeLabel = "MFA",
            },
            new()
            {
                Username = "asmith",
                DisplayName = "Alex Smith",
                TypeLabel = "No account",
            },
        };
        // Extra blank rows for typing
        for (var i = 0; i < 5; i++)
            list.Add(new BulkGridRow());
        return list;
    }

    public static BulkParseResult ValidateGrid(IEnumerable<BulkGridRow> gridRows)
    {
        var result = new BulkParseResult();
        var n = 0;
        foreach (var g in gridRows)
        {
            n++;
            if (g.IsEmpty) continue;

            var user = NormalizeUsername(g.Username);
            if (string.IsNullOrWhiteSpace(user))
            {
                result.Errors.Add($"Row {n}: username is required.");
                continue;
            }

            var mode = NormalizeMode(g.TypeLabel) ?? "password";
            var pass = (g.Password ?? "").Trim();
            var needsPass = TemplateFiles.NeedsTempPassword(mode);

            if (needsPass)
            {
                if (string.IsNullOrWhiteSpace(pass) || pass.Length < 8)
                {
                    result.Errors.Add($"Row {n} ({user}): {ModeLabel(mode)} needs password (8+).");
                    continue;
                }
                if (pass.Contains("007.teslamotors.com", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"Row {n} ({user}): use one-time password, not a 007 link.");
                    continue;
                }
            }

            var ita = NormalizeIta(g.Ita);
            result.Rows.Add(new BulkCloseoutRow
            {
                Username = user,
                TempPassword = needsPass ? pass : "",
                Name = (g.DisplayName ?? "").Trim(),
                Ita = ita,
                Mode = mode,
                LineNumber = n,
            });
        }
        return result;
    }

    /// <param name="defaultMode">Used when a row has no Type value.</param>
    public static BulkParseResult Parse(string text, string defaultMode = "password") =>
        ParseLines(SplitLines(text), defaultMode);

    public static ObservableCollection<BulkGridRow> ParseToGrid(string text, string defaultMode = "password")
    {
        // Lenient fill for paste/CSV - no password validation; Run validates later.
        return FillGridFromText(text, defaultMode, extraBlankRows: 3);
    }

    /// <summary>
    /// Map clipboard/CSV text into grid rows without validating passwords.
    /// Never runs jobs - UI fill only.
    /// </summary>
    public static ObservableCollection<BulkGridRow> FillGridFromText(
        string text,
        string defaultMode = "password",
        int extraBlankRows = 3)
    {
        defaultMode = NormalizeMode(defaultMode) ?? "password";
        var list = new ObservableCollection<BulkGridRow>();
        var lines = SplitLines(text).ToList();
        if (lines.Count == 0)
            return CreateEmptyGrid();

        var start = 0;
        int userCol = 0, passCol = -1, nameCol = -1, typeCol = -1, itaCol = -1;
        var hasHeader = false;

        var first = SplitFields(lines[0]);
        if (LooksLikeHeader(first))
        {
            hasHeader = true;
            start = 1;
            userCol = IndexOfHeader(first,
                "username", "user", "login", "samaccountname", "uid", "account");
            passCol = IndexOfHeader(first,
                "password", "temp", "otp", "onetimepassword", "temp_password",
                "temporary", "temppassword");
            nameCol = IndexOfHeader(first,
                "displayname", "display name", "name", "fullname", "display", "fullname");
            itaCol = IndexOfHeader(first,
                "itanumber", "ita", "ticket", "ticketnumber", "ticketkey", "key");
            typeCol = IndexOfHeader(first,
                "tickettype", "type", "mode", "job",
                "closeout", "closeouttype", "requesttype");
            if (userCol < 0) userCol = 0;
            // If only one column header and no password header, still ok
            if (passCol < 0 && first.Count >= 2 && nameCol != 1 && typeCol != 1 && itaCol != 1)
                passCol = 1;
        }

        for (var i = start; i < lines.Count; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0 || raw.StartsWith('#') || raw.StartsWith("//"))
                continue;

            var fields = SplitFields(raw);
            if (fields.Count == 0) continue;

            string user = "";
            string pass = "";
            string name = "";
            string typeRaw = "";
            string ita = "";

            if (hasHeader)
            {
                user = Get(fields, userCol);
                pass = passCol >= 0 ? Get(fields, passCol) : "";
                if (nameCol >= 0) name = Get(fields, nameCol);
                if (typeCol >= 0) typeRaw = Get(fields, typeCol);
                if (itaCol >= 0) ita = Get(fields, itaCol);

                // Positional fallback when headers exist but some columns missing:
                // Username, Display Name, Password, ITA, Type is common Excel order
                if (fields.Count >= 5 && passCol < 0 && nameCol < 0)
                {
                    user = fields[0];
                    name = fields[1];
                    pass = fields[2];
                    ita = fields[3];
                    typeRaw = fields[4];
                }
            }
            else
            {
                // No header: treat as Username, Display Name, Password, ITA, Type
                // or shorter prefixes
                user = fields.Count > 0 ? fields[0] : "";
                if (fields.Count == 2)
                {
                    var m = NormalizeMode(fields[1]);
                    if (m is not null && !LooksLikePassword(fields[1]))
                        typeRaw = fields[1];
                    else
                        pass = fields[1];
                }
                else if (fields.Count == 3)
                {
                    // user, name|pass, type|pass
                    var m2 = NormalizeMode(fields[2]);
                    if (m2 is not null)
                    {
                        name = fields[1];
                        typeRaw = fields[2];
                    }
                    else if (LooksLikePassword(fields[1]) || fields[1].Length >= 8)
                    {
                        pass = fields[1];
                        name = fields[2];
                    }
                    else
                    {
                        name = fields[1];
                        pass = fields[2];
                    }
                }
                else if (fields.Count >= 4)
                {
                    // Username, Display Name, Password, ITA [, Type]
                    user = fields[0];
                    name = fields[1];
                    pass = fields[2];
                    ita = fields[3];
                    if (fields.Count >= 5)
                        typeRaw = fields[4];
                }
            }

            user = NormalizeUsername(user);
            if (string.IsNullOrWhiteSpace(user) && string.IsNullOrWhiteSpace(ita)
                && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(pass))
                continue;

            var mode = NormalizeMode(typeRaw) ?? defaultMode;
            list.Add(new BulkGridRow
            {
                Username = user,
                DisplayName = name.Trim().Trim('"'),
                Password = pass.Trim().Trim('"'),
                Ita = NormalizeIta(ita),
                TypeLabel = ModeLabel(mode),
            });
        }

        for (var i = 0; i < extraBlankRows; i++)
            list.Add(new BulkGridRow());

        return list.Count == 0 ? CreateEmptyGrid() : list;
    }

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
        int userCol = 0, passCol = -1, nameCol = -1, badgeCol = -1, typeCol = -1, itaCol = -1;

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
                    "temporary", "temppassword");
                nameCol = IndexOfHeader(first,
                    "displayname", "display name", "name", "fullname", "display", "fullname");
                badgeCol = IndexOfHeader(first,
                    "badge", "badgenumber", "emp", "employee");
                itaCol = IndexOfHeader(first,
                    "itanumber", "ita", "ticket", "ticketnumber", "ticketkey", "key");
                typeCol = IndexOfHeader(first,
                    "tickettype", "type", "mode", "job",
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
            string ita = "";

            if (hasHeader)
            {
                user = Get(fields, userCol);
                pass = passCol >= 0 ? Get(fields, passCol) : "";
                if (nameCol >= 0) name = Get(fields, nameCol);
                if (badgeCol >= 0) badge = Get(fields, badgeCol);
                if (typeCol >= 0) typeRaw = Get(fields, typeCol);
                if (itaCol >= 0) ita = Get(fields, itaCol);
            }
            else if (fields.Count >= 2)
            {
                user = fields[0];
                var second = fields[1];
                var secondMode = NormalizeMode(second);
                if (secondMode is not null && second.Length < 24 && !LooksLikePassword(second))
                {
                    typeRaw = second;
                    if (fields.Count >= 3) name = fields[2];
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
            ita = NormalizeIta(ita);

            if (string.IsNullOrWhiteSpace(user) && string.IsNullOrWhiteSpace(ita))
            {
                result.Errors.Add($"Line {lineNo}: username or ITA required.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(typeRaw) && NormalizeMode(typeRaw) is null)
            {
                result.Errors.Add(
                    $"Line {lineNo}: unknown type '{typeRaw}'. " +
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

            result.Rows.Add(new BulkCloseoutRow
            {
                Username = user,
                TempPassword = needsPass ? pass : "",
                Name = name,
                Badge = badge,
                Ita = ita,
                Mode = mode,
                LineNumber = lineNo,
            });
        }

        return result;
    }

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

    public static string NormalizeIta(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length == 0) return "";
        var m = System.Text.RegularExpressions.Regex.Match(s, @"ITA-\d+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return m.Success ? m.Value.ToUpperInvariant() : s;
    }

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
               || joined.Contains("badge")
               || joined.Contains("ita")
               || joined.Contains("display");
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
