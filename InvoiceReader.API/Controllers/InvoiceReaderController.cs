using ClosedXML.Excel;
using InvoiceReader.API.Core.Entities;
using InvoiceReader.API.Infrastructure.Integrations.OpenAI.Client;
using InvoiceReader.API.Infrastructure.Integrations.OpenAI.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace InvoiceReader.API.Controllers;

[ApiController]
[Route("[controller]")]
public class InvoiceReaderController : ControllerBase
{
    [HttpPost("convert")]
    public async Task<ActionResult> UploadDocument(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        try
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var base64UrlString = $"data:application/pdf;base64,{Convert.ToBase64String(memoryStream.ToArray())}";
            var request = CreateRequest(base64UrlString);
            var invoice = await SearchInvoiceDataAsync(request, cancellationToken);
            CreateInvoiceExcel(invoice);
            return Ok(invoice);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error. {ex.Message}");
        }
    }

    private ResponsesApiRequest CreateRequest(string base64Document) => new ResponsesApiRequest().CreateSearchDocumentRequest(base64Document, typeof(Invoice), _openAIConfigurations.Model);

    private async Task<Invoice> SearchInvoiceDataAsync(ResponsesApiRequest request, CancellationToken cancellationToken)
    {
        var response = await _openAIHttpClient.SendRequestToResponsesAPIAsync(request, cancellationToken);

        var invoice = JsonSerializer.Deserialize<Invoice>(response.output?.First(o => o.type == "message").content?.First().text);
        return invoice;
    }

    private void CreateInvoiceExcel(Invoice invoice)
    {
        try
        {
            var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Ações");

            worksheet.Cell(1, 1).InsertTable(invoice.Stocks);
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string filePath = Path.Combine(desktop, $"acoes_{DateTime.Now.ToString("yyyy-MM-dd'T'HH-mm-ss")}.xlsx");
            workbook.SaveAs(filePath);
        }
        catch(Exception ex)
        {
            throw;
        }
    }

    private readonly IOpenAIHttpClient _openAIHttpClient;
    private readonly OpenAIConfigurations _openAIConfigurations;

    public InvoiceReaderController(IOpenAIHttpClient openAIHttpClient,
        IOptions<OpenAIConfigurations> openAIConfigurations)
    {
        _openAIHttpClient = openAIHttpClient;
        _openAIConfigurations = openAIConfigurations.Value;

    }
}
