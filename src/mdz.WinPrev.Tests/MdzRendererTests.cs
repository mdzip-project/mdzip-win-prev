using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MDZip.WinPrev;

namespace MDZip.WinPrev.Tests;

/// <summary>
/// Unit tests for <see cref="MdzRenderer"/>.
/// </summary>
public sealed class MdzRendererTests : IDisposable
{
    private readonly string _tempDir;
    private readonly MdzRenderer _renderer = new();

    public MdzRendererTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mdz-prev-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string NewMdzPath() => Path.Combine(_tempDir, Guid.NewGuid().ToString("N") + ".mdz");

    private static string MakeArchive(
        string mdzPath,
        (string path, string content)[] entries,
        object? manifest = null)
    {
        using var zip = ZipFile.Open(mdzPath, ZipArchiveMode.Create);

        foreach (var (path, content) in entries)
        {
            var entry = zip.CreateEntry(path);
            using var s = entry.Open();
            s.Write(Encoding.UTF8.GetBytes(content));
        }

        if (manifest is not null)
        {
            var mEntry = zip.CreateEntry("manifest.json");
            using var ms = mEntry.Open();
            ms.Write(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest)));
        }

        return mdzPath;
    }

    // -------------------------------------------------------------------------
    // Document mode — basic rendering
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_MinimalArchive_ReturnsHtmlContainingMarkdownBody()
    {
        var mdzPath = MakeArchive(NewMdzPath(), [("index.md", "# Hello World\n\nSome text.")]);

        var html = _renderer.Render(mdzPath);

        Assert.Contains("<h1", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Hello World</h1>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Some text.", html);
    }

    [Fact]
    public void Render_MinimalArchive_ReturnsCompleteHtmlDocument()
    {
        var mdzPath = MakeArchive(NewMdzPath(), [("index.md", "# Test")]);

        var html = _renderer.Render(mdzPath);

        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart());
        Assert.Contains("<html", html);
        Assert.Contains("</html>", html);
        Assert.Contains("<body>", html);
        Assert.Contains("</body>", html);
    }

    [Fact]
    public void Render_WithManifestTitle_UsesTitleInHtml()
    {
        var mdzPath = MakeArchive(
            NewMdzPath(),
            [("index.md", "# Content")],
            new { spec = new { version = "1.1.0" }, title = "My Document" });

        var html = _renderer.Render(mdzPath);

        Assert.Contains("My Document", html);
    }

    [Fact]
    public void Render_WithoutManifest_UseFileNameAsTitle()
    {
        var mdzPath = MakeArchive(NewMdzPath(), [("index.md", "# Test")]);
        var expectedTitle = Path.GetFileNameWithoutExtension(mdzPath);

        var html = _renderer.Render(mdzPath);

        Assert.Contains(expectedTitle, html);
    }

    [Fact]
    public void Render_SingleRootMarkdownFile_ResolvesAsEntryPoint()
    {
        var mdzPath = MakeArchive(NewMdzPath(), [("readme.md", "# Readme\n\nSome readme content.")]);

        var html = _renderer.Render(mdzPath);

        Assert.Contains("Readme", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Some readme content.", html);
    }

    [Fact]
    public void Render_ManifestEntryPointOverride_RendersOverrideFile()
    {
        var mdzPath = MakeArchive(
            NewMdzPath(),
            [("index.md", "# Index"), ("start.md", "# Start Page\n\nStart content.")],
            new { spec = new { version = "1.1.0" }, title = "Doc", entryPoint = "start.md" });

        var html = _renderer.Render(mdzPath);

        Assert.Contains("Start Page", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Start content.", html);
    }

    [Fact]
    public void Render_MarkdownWithCodeBlock_IsRenderedAsPreCode()
    {
        var mdzPath = MakeArchive(NewMdzPath(), [("index.md", "# Code\n\n```csharp\nvar x = 1;\n```\n")]);

        var html = _renderer.Render(mdzPath);

        Assert.Contains("<pre>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<code", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_MarkdownWithTable_IsRenderedAsHtmlTable()
    {
        var mdzPath = MakeArchive(NewMdzPath(), [("index.md", "| A | B |\n|---|---|\n| 1 | 2 |\n")]);

        var html = _renderer.Render(mdzPath);

        Assert.Contains("<table", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<td>", html, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Asset base directory
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_WithAssetBaseDir_EmitsBaseTag()
    {
        var mdzPath = MakeArchive(NewMdzPath(), [("index.md", "# Test")]);

        var html = _renderer.Render(mdzPath, assetBaseDir: @"C:\Some\Dir");

        Assert.Contains("<base href=", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C:/Some/Dir", html);
    }

    [Fact]
    public void Render_WithoutAssetBaseDir_DoesNotEmitBaseTag()
    {
        var mdzPath = MakeArchive(NewMdzPath(), [("index.md", "# Test")]);

        var html = _renderer.Render(mdzPath);

        Assert.DoesNotContain("<base", html, StringComparison.OrdinalIgnoreCase);
    }

    // -------------------------------------------------------------------------
    // Project mode
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_ProjectMode_ThrowsProjectModeNotSupportedException()
    {
        var mdzPath = MakeArchive(
            NewMdzPath(),
            [("index.md", "# Home"), ("guide/intro.md", "# Intro")],
            new { spec = new { version = "1.1.0" }, title = "Project", mode = "project" });

        Assert.Throws<ProjectModeNotSupportedException>(() => _renderer.Render(mdzPath));
    }

    [Fact]
    public void Render_ProjectModeCaseInsensitive_ThrowsProjectModeNotSupportedException()
    {
        var mdzPath = MakeArchive(
            NewMdzPath(),
            [("index.md", "# Home")],
            new { spec = new { version = "1.1.0" }, mode = "PROJECT" });

        Assert.Throws<ProjectModeNotSupportedException>(() => _renderer.Render(mdzPath));
    }

    [Fact]
    public void Render_DocumentModeExplicit_RendersNormally()
    {
        var mdzPath = MakeArchive(
            NewMdzPath(),
            [("index.md", "# Doc\n\nDocument content.")],
            new { spec = new { version = "1.1.0" }, mode = "document" });

        var html = _renderer.Render(mdzPath);

        Assert.Contains("Document content.", html);
    }

    [Fact]
    public void Render_NoModeInManifest_RendersNormally()
    {
        var mdzPath = MakeArchive(
            NewMdzPath(),
            [("index.md", "# Doc\n\nSome content.")],
            new { spec = new { version = "1.1.0" }, title = "No Mode" });

        var html = _renderer.Render(mdzPath);

        Assert.Contains("Some content.", html);
    }

    // -------------------------------------------------------------------------
    // Entry-point resolution errors
    // -------------------------------------------------------------------------

    [Fact]
    public void Render_MultipleRootMarkdownNoIndex_ThrowsInvalidOperationException()
    {
        var mdzPath = MakeArchive(
            NewMdzPath(),
            [("doc1.md", "# Doc 1"), ("doc2.md", "# Doc 2")]);

        Assert.Throws<InvalidOperationException>(() => _renderer.Render(mdzPath));
    }

    [Fact]
    public void Render_NoMarkdownFiles_ThrowsInvalidOperationException()
    {
        var mdzPath = MakeArchive(
            NewMdzPath(),
            [("README.txt", "just text")]);

        Assert.Throws<InvalidOperationException>(() => _renderer.Render(mdzPath));
    }
}
