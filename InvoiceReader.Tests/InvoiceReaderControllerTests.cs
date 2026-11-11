using InvoiceReader.API.Controllers;
using InvoiceReader.API.Core.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceReader.Tests;

public class InvoiceReaderControllerTests
{
    private static IFormFile File(int length) =>
        new FormFile(new MemoryStream(new byte[length]), 0, length, "file", "n.pdf");

    private static InvoiceReaderController Create(ExtractionResult result) => new(new FakeExtractor(result));

    [Fact]
    public async Task Empty_upload_is_400()
    {
        var response = await Create(ExtractionResult.Fail(ExtractionFailureKind.Unreadable, "x")).UploadDocument(File(0), default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(response).StatusCode);
    }

    [Fact]
    public async Task Missing_upload_is_400()
    {
        var response = await Create(ExtractionResult.Fail(ExtractionFailureKind.Unreadable, "x")).UploadDocument(null!, default);
        Assert.Equal(400, Assert.IsAssignableFrom<ObjectResult>(response).StatusCode);
    }

    [Fact]
    public async Task Unreadable_is_422_without_exception_message()
    {
        var response = await Create(ExtractionResult.Fail(ExtractionFailureKind.Unreadable, "secret internals")).UploadDocument(File(5), default);
        var obj = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(422, obj.StatusCode);
        Assert.DoesNotContain("secret", obj.Value?.ToString());
    }

    [Theory]
    [InlineData(ExtractionFailureKind.ProviderRejected)]
    [InlineData(ExtractionFailureKind.MalformedResponse)]
    public async Task Provider_failures_are_502(ExtractionFailureKind kind)
    {
        var response = await Create(ExtractionResult.Fail(kind, "secret internals")).UploadDocument(File(5), default);
        var obj = Assert.IsAssignableFrom<ObjectResult>(response);
        Assert.Equal(502, obj.StatusCode);
        Assert.DoesNotContain("secret", obj.Value?.ToString());
    }

    private sealed class FakeExtractor(ExtractionResult result) : IBrokerageNoteExtractor
    {
        public Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
