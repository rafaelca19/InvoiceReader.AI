using InvoiceReader.API.Core.Abstractions;
using InvoiceReader.API.Core.Entities;
using InvoiceReader.API.Infrastructure.Integrations.OpenAI.Model;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace InvoiceReader.API.Infrastructure.Integrations.OpenAI;

public class OpenAIBrokerageNoteExtractor : IBrokerageNoteExtractor
{
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    private readonly HttpClient _httpClient;
    private readonly OpenAIConfigurations _configurations;

    public OpenAIBrokerageNoteExtractor(IHttpClientFactory httpClientFactory, IOptions<OpenAIConfigurations> configurations)
    {
        _httpClient = httpClientFactory.CreateClient("OpenAI");
        _configurations = configurations.Value;
    }

    public async Task<ExtractionResult> ExtractAsync(Stream pdf, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await pdf.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        if (!bytes.AsSpan().StartsWith(PdfMagic))
            return ExtractionResult.Fail(ExtractionFailureKind.Unreadable, "The upload is not a PDF.");

        var dataUrl = $"data:application/pdf;base64,{Convert.ToBase64String(bytes)}";
        var request = new ResponsesApiRequest().CreateSearchDocumentRequest(dataUrl, typeof(Invoice), _configurations.Model);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsJsonAsync(_configurations.ResponsesAPIUrl, request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return ExtractionResult.Fail(ExtractionFailureKind.ProviderRejected, $"Provider request failed: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return ExtractionResult.Fail(ExtractionFailureKind.ProviderRejected, $"Provider returned {(int)response.StatusCode}.");

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            Invoice? note;
            try
            {
                var parsed = JsonSerializer.Deserialize<ResponsesApiResponse>(body);
                var text = parsed?.output?.FirstOrDefault(o => o.type == "message")?.content?.FirstOrDefault()?.text;
                if (string.IsNullOrWhiteSpace(text))
                    return ExtractionResult.Fail(ExtractionFailureKind.MalformedResponse, "Provider response contained no message.");

                note = JsonSerializer.Deserialize<Invoice>(text);
            }
            catch (JsonException)
            {
                return ExtractionResult.Fail(ExtractionFailureKind.MalformedResponse, "Provider response was not valid JSON.");
            }

            if (note is null)
                return ExtractionResult.Fail(ExtractionFailureKind.MalformedResponse, "Provider message was empty.");
            if (note.Stocks is null || note.Stocks.Count == 0)
                return ExtractionResult.Fail(ExtractionFailureKind.Unreadable, "No trades could be read from the document.");

            return ExtractionResult.Success(note);
        }
    }
}
