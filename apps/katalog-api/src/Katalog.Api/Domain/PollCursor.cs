namespace Katalog.Api.Domain;

/// <summary>
/// Singleton-row poll cursor per polling job (research §5), giving crash-safe resumption:
/// <see cref="CursorValue"/> records the latest successfully polled point in time.
/// </summary>
public sealed class PollCursor
{
    public string JobName { get; set; } = string.Empty;
    public DateTimeOffset? CursorValue { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public string? Status { get; set; }
}
