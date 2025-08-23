using InvoiceReader.API.Infrastructure.Integrations.OpenAI.Model;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace InvoiceReader.API.Infrastructure.Integrations.OpenAI.Client;

public class OpenAIHttpClient : IOpenAIHttpClient
{
    public async Task<ResponsesApiResponse> SendRequestToResponsesAPIAsync(ResponsesApiRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                TypeInfoResolver = JsonTypeInfoResolver.Combine(
                new DefaultJsonTypeInfoResolver())
            });

            var response = await _openAIHttpClient.PostAsync(_openAIConfigurations.ResponsesAPIUrl,
                                                            new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
                                                            cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            var responseJson = JsonSerializer.Deserialize<ResponsesApiResponse>(responseText);
            return responseJson;
        }
        catch (Exception ex)
        {
            throw new Exception("An error occurred while sending the request to the OpenAI API.", ex);
        }
    }

    private readonly HttpClient _openAIHttpClient;
    private readonly OpenAIConfigurations _openAIConfigurations;
    public OpenAIHttpClient(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAIConfigurations> openAIConfigurations)
    {
        _openAIHttpClient = httpClientFactory.CreateClient("OpenAI");
        _openAIConfigurations = openAIConfigurations.Value;
    }
}
