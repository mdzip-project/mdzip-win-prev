namespace MDZip.WinPrev;

/// <summary>
/// Thrown when an .mdz archive declares <c>"mode": "project"</c> and the caller
/// requested document-mode rendering, which is the only mode supported by this
/// preview handler.
/// </summary>
public sealed class ProjectModeNotSupportedException : NotSupportedException
{
    /// <inheritdoc />
    public ProjectModeNotSupportedException(string message) : base(message) { }
}
