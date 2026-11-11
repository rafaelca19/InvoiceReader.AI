using InvoiceReader.API.Core.Abstractions;
using InvoiceReader.API.Infrastructure.Integrations.OpenAI;
using InvoiceReader.API.Infrastructure.Integrations.OpenAI.Model;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace InvoiceReader.Tests;

public class OpenAIBrokerageNoteExtractorTests
{
    private static Stream Pdf() => new MemoryStream("%PDF-1.7 fake"u8.ToArray());

    private static string Envelope(string messageText) => JsonSerializer.Serialize(new
    {
        id = "resp_1",
        model = "m",
        output = new object[]
        {
            new { type = "reasoning", id = "r1" },
            new { type = "message", id = "m1", status = "completed", role = "assistant",
                  content = new[] { new { type = "output_text", text = messageText } } },
        },
    });

    private static IBrokerageNoteExtractor Create(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var client = new HttpClient(new StubHandler(respond));
        return new OpenAIBrokerageNoteExtractor(
            new StubFactory(client),
            Options.Create(new OpenAIConfigurations { ApiKey = "k", Model = "m", ResponsesAPIUrl = "https://example.test/v1/responses" }));
    }

    private static IBrokerageNoteExtractor Respond(HttpStatusCode status, string body) =>
        Create((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") }));

    [Fact]
    public async Task Valid_response_returns_note()
    {
        var sut = Respond(HttpStatusCode.OK, Envelope("""{"Stocks":[{"Ticker":"PETR4","Amount":100,"Price":30.5}]}"""));
        var result = await sut.ExtractAsync(Pdf(), default);

        Assert.True(result.IsSuccess);
        var trade = Assert.Single(result.Note!.Stocks);
        Assert.Equal("PETR4", trade.Ticker);
        Assert.Equal(100, trade.Amount);
    }

    [Fact]
    public async Task Non_pdf_is_unreadable_without_calling_provider()
    {
        var called = false;
        var sut = Create((_, _) => { called = true; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); });
        var result = await sut.ExtractAsync(new MemoryStream("hello"u8.ToArray()), default);

        Assert.Equal(ExtractionFailureKind.Unreadable, result.Failure);
        Assert.False(called);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Error_status_is_provider_rejected(HttpStatusCode status)
    {
        var result = await Respond(status, """{"error":{"message":"nope"}}""").ExtractAsync(Pdf(), default);
        Assert.Equal(ExtractionFailureKind.ProviderRejected, result.Failure);
    }

    [Fact]
    public async Task Transport_failure_is_provider_rejected()
    {
        var sut = Create((_, _) => throw new HttpRequestException("boom"));
        var result = await sut.ExtractAsync(Pdf(), default);
        Assert.Equal(ExtractionFailureKind.ProviderRejected, result.Failure);
    }

    [Fact]
    public async Task Response_without_message_is_malformed()
    {
        var result = await Respond(HttpStatusCode.OK, """{"output":[{"type":"reasoning"}]}""").ExtractAsync(Pdf(), default);
        Assert.Equal(ExtractionFailureKind.MalformedResponse, result.Failure);
    }

    [Fact]
    public async Task Invalid_json_message_is_malformed()
    {
        var result = await Respond(HttpStatusCode.OK, Envelope("not json")).ExtractAsync(Pdf(), default);
        Assert.Equal(ExtractionFailureKind.MalformedResponse, result.Failure);
    }

    [Fact]
    public async Task Zero_trades_is_unreadable()
    {
        var result = await Respond(HttpStatusCode.OK, Envelope("""{"Stocks":[]}""")).ExtractAsync(Pdf(), default);
        Assert.Equal(ExtractionFailureKind.Unreadable, result.Failure);
    }

    [Fact]
    public async Task Null_trades_is_unreadable()
    {
        var result = await Respond(HttpStatusCode.OK, Envelope("""{"Stocks":null}""")).ExtractAsync(Pdf(), default);
        Assert.Equal(ExtractionFailureKind.Unreadable, result.Failure);
    }

    [Fact]
    public async Task Cancellation_stops_the_provider_call()
    {
        using var cts = new CancellationTokenSource();
        var sut = Create(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return new HttpResponseMessage(); });
        var task = sut.ExtractAsync(Pdf(), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request, ct);
    }

    private sealed class StubFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
