using System.Text;

namespace dCMS.Order.Core.Ordering;

/// <summary>URL-safe cursor encoding for keyset pagination (CreatedAt DESC, Id DESC).</summary>
public static class OrderListCursorCodec
{
    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var raw = $"{createdAt:O}|{id:D}";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Empty cursor is valid (first page).</summary>
    public static bool TryDecode(string? cursor, out DateTimeOffset? createdAt, out Guid? id)
    {
        createdAt = null;
        id = null;
        if (string.IsNullOrWhiteSpace(cursor))
            return true;

        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            var bytes = Convert.FromBase64String(padded);
            var s = Encoding.UTF8.GetString(bytes);
            var pipe = s.IndexOf('|', StringComparison.Ordinal);
            if (pipe <= 0 || pipe >= s.Length - 1)
                return false;
            if (!DateTimeOffset.TryParse(s.AsSpan(0, pipe), null, System.Globalization.DateTimeStyles.RoundtripKind, out var ca))
                return false;
            if (!Guid.TryParse(s.AsSpan(pipe + 1), out var gid))
                return false;
            createdAt = ca;
            id = gid;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
