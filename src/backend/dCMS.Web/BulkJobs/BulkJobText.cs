using System.Text;

namespace dCMS.Web.BulkJobs;

internal static class BulkJobText
{
    public static string Csv(string? s) => s is null
        ? ""
        : (s.Contains(',') || s.Contains('"', StringComparison.Ordinal) || s.Contains('\n', StringComparison.Ordinal)
            ? $"\"{s.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : s);

    public static List<string> SplitCsvLine(string line)
    {
        var list = new List<string>();
        var cur = new StringBuilder();
        var inQ = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQ = !inQ;
                continue;
            }
            if (c == ',' && !inQ)
            {
                list.Add(cur.ToString());
                cur.Clear();
                continue;
            }
            cur.Append(c);
        }
        list.Add(cur.ToString());
        return list;
    }
}
