namespace Katalog.Api.Features.Labels;

/// <summary>
/// Turns a label name into a URL-safe unique slug (arch report conventions: Slugify-style, kept
/// tiny and dependency-free for the single-user MVP).
/// </summary>
public static class LabelSlug
{
    public static string From(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length);
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }
}
