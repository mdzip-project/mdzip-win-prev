using System.IO.Compression;
using System.Text;
using Markdig;
using MDZip.Core;
using MDZip.Core.Models;

namespace MDZip.WinPrev;

/// <summary>
/// Renders the entry-point Markdown from an .mdz archive to a complete HTML document.
/// Only document mode is supported; project mode archives are rejected with a clear message.
/// </summary>
internal sealed class MdzRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// Renders the entry-point of the specified .mdz file to a self-contained HTML string.
    /// Images and other assets are referenced from <paramref name="assetBaseDir"/>.
    /// </summary>
    /// <param name="mdzFilePath">Absolute path to the .mdz file.</param>
    /// <param name="assetBaseDir">
    /// Directory where archive assets have been extracted, used as the base for
    /// asset URLs in the rendered HTML.  When <see langword="null"/> assets are not
    /// rewritten.
    /// </param>
    /// <returns>A complete HTML document string ready for display.</returns>
    /// <exception cref="ProjectModeNotSupportedException">
    /// Thrown when the archive manifest declares <c>"mode": "project"</c>.
    /// </exception>
    public string Render(string mdzFilePath, string? assetBaseDir = null)
    {
        var manifest = MdzArchive.ReadManifest(mdzFilePath);

        if (IsProjectMode(manifest))
        {
            throw new ProjectModeNotSupportedException(
                "This .mdz archive uses project mode, which is not supported by the preview handler. " +
                "Only document mode archives can be previewed.");
        }

        var entryPoint = MdzArchive.ResolveEntryPoint(mdzFilePath)
            ?? throw new InvalidOperationException(
                "No unambiguous entry point found in the .mdz archive. " +
                "The archive must contain an 'index.md', a single root-level Markdown file, " +
                "or a manifest.json with an 'entryPoint' field.");

        var markdown = ReadEntryPoint(mdzFilePath, entryPoint);
        var bodyHtml = Markdown.ToHtml(markdown, Pipeline);
        var title = manifest?.Title ?? Path.GetFileNameWithoutExtension(mdzFilePath);

        return BuildHtmlDocument(title, bodyHtml, assetBaseDir);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool IsProjectMode(Manifest? manifest)
    {
        return string.Equals(manifest?.Mode, "project", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadEntryPoint(string mdzFilePath, string entryPoint)
    {
        using var zip = ZipFile.OpenRead(mdzFilePath);
        var entry = zip.GetEntry(entryPoint)
            ?? zip.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName, entryPoint, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Entry point '{entryPoint}' not found inside the .mdz archive.");

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string BuildHtmlDocument(string title, string bodyHtml, string? assetBaseDir)
    {
        var baseTag = assetBaseDir is not null
            ? $"<base href=\"file:///{assetBaseDir.Replace('\\', '/').TrimEnd('/')}/\">"
            : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\">");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        if (!string.IsNullOrEmpty(baseTag))
            sb.AppendLine($"  {baseTag}");
        sb.AppendLine($"  <title>{HtmlEncode(title)}</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(StyleSheet);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine(bodyHtml);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private const string StyleSheet =
        ":root {\n" +
        "  --bg: #ffffff;\n" +
        "  --fg: #1a1a1a;\n" +
        "  --link: #0066cc;\n" +
        "  --code-bg: #f4f4f4;\n" +
        "  --border: #d1d5db;\n" +
        "  --heading-fg: #111827;\n" +
        "}\n" +
        "@media (prefers-color-scheme: dark) {\n" +
        "  :root {\n" +
        "    --bg: #1e1e1e;\n" +
        "    --fg: #d4d4d4;\n" +
        "    --link: #4db8ff;\n" +
        "    --code-bg: #2d2d2d;\n" +
        "    --border: #3e3e3e;\n" +
        "    --heading-fg: #e0e0e0;\n" +
        "  }\n" +
        "}\n" +
        "* { box-sizing: border-box; }\n" +
        "body {\n" +
        "  font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;\n" +
        "  font-size: 15px; line-height: 1.65;\n" +
        "  color: var(--fg); background: var(--bg);\n" +
        "  margin: 0; padding: 16px 24px; max-width: 900px;\n" +
        "}\n" +
        "h1, h2, h3, h4, h5, h6 { color: var(--heading-fg); margin-top: 1.5em; margin-bottom: 0.5em; line-height: 1.3; }\n" +
        "h1 { font-size: 1.9em; border-bottom: 1px solid var(--border); padding-bottom: 0.25em; }\n" +
        "h2 { font-size: 1.5em; border-bottom: 1px solid var(--border); padding-bottom: 0.2em; }\n" +
        "a { color: var(--link); text-decoration: none; }\n" +
        "a:hover { text-decoration: underline; }\n" +
        "p { margin: 0.75em 0; }\n" +
        "img { max-width: 100%; height: auto; }\n" +
        "pre, code { font-family: \"Cascadia Code\", \"Fira Mono\", Consolas, monospace; font-size: 0.9em; background: var(--code-bg); }\n" +
        "pre { padding: 12px 16px; border-radius: 6px; overflow-x: auto; border: 1px solid var(--border); }\n" +
        "code { padding: 1px 4px; border-radius: 3px; }\n" +
        "pre code { padding: 0; background: transparent; }\n" +
        "blockquote { margin: 1em 0; padding: 0.5em 1em; border-left: 4px solid var(--border); color: #6b7280; }\n" +
        "table { border-collapse: collapse; width: 100%; margin: 1em 0; }\n" +
        "th, td { border: 1px solid var(--border); padding: 8px 12px; text-align: left; }\n" +
        "th { background: var(--code-bg); font-weight: 600; }\n" +
        "ul, ol { padding-left: 2em; margin: 0.75em 0; }\n" +
        "li { margin: 0.25em 0; }\n" +
        "hr { border: none; border-top: 1px solid var(--border); margin: 2em 0; }";

    private static string HtmlEncode(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }
}
