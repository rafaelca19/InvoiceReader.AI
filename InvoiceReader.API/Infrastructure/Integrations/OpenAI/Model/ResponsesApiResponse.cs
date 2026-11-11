namespace InvoiceReader.API.Infrastructure.Integrations.OpenAI.Model;

internal class ResponsesApiResponse
{
    public string id { get; set; }
    public string model { get; set; }
    public Output[] output { get; set; }
}

internal class Output
{
    public string type { get; set; }
    public string id { get; set; }
    public string status { get; set; }
    public string role { get; set; }
    public TextContent[] content { get; set; }
}
