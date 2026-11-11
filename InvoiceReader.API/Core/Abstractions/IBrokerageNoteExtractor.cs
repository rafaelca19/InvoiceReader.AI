using InvoiceReader.API.Core.Entities;

namespace InvoiceReader.API.Core.Abstractions;

public interface IBrokerageNoteExtractor
{
    Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken);
}

public enum ExtractionFailureKind
{
    Unreadable,
    ProviderRejected,
    MalformedResponse,
}

public sealed class ExtractionResult
{
    private ExtractionResult(Invoice? note, ExtractionFailureKind? failure, string? message)
    {
        Note = note;
        Failure = failure;
        Message = message;
    }

    public Invoice? Note { get; }
    public ExtractionFailureKind? Failure { get; }
    public string? Message { get; }
    public bool IsSuccess => Note is not null;

    public static ExtractionResult Success(Invoice note) => new(note, null, null);
    public static ExtractionResult Fail(ExtractionFailureKind kind, string message) => new(null, kind, message);
}
