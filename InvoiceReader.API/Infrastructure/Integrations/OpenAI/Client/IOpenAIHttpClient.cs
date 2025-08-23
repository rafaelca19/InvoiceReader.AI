using InvoiceReader.API.Infrastructure.Integrations.OpenAI.Model;

namespace InvoiceReader.API.Infrastructure.Integrations.OpenAI.Client;

public interface IOpenAIHttpClient
{
    Task<ResponsesApiResponse> SendRequestToResponsesAPIAsync(ResponsesApiRequest request, CancellationToken cancellationToken);
}
