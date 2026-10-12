using System.Text;

namespace OmsiHofManager.Services;

public static class HofParser
{
    /// <summary>Liest den Anzeigenamen aus dem [name]-Abschnitt einer Hofdatei.</summary>
    public static string? ReadDisplayName(string path)
    {
        try
        {
            using var reader = new StreamReader(path, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true);
            string? line;
            var lineNumber = 0;
            while ((line = reader.ReadLine()) is not null && lineNumber++ < 500)
            {
                if (!line.Trim().Equals("[name]", StringComparison.OrdinalIgnoreCase))
                    continue;

                while ((line = reader.ReadLine()) is not null)
                {
                    var value = line.Trim();
                    if (value.Length > 0)
                        return value;
                }
            }
        }
        catch (Exception)
        {
            // Unlesbare Datei: Dateiname wird als Titel verwendet.
        }

        return null;
    }

    public static bool FilesEqual(string a, string b)
    {
        var infoA = new FileInfo(a);
        var infoB = new FileInfo(b);
        if (!infoA.Exists || !infoB.Exists || infoA.Length != infoB.Length)
            return false;
        return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
    }
}
