using ClosedXML.Excel;
using InvoiceReader.API.Core.Abstractions;
using InvoiceReader.API.Core.Entities;
using Microsoft.AspNetCore.Mvc;

namespace InvoiceReader.API.Controllers;

[ApiController]
[Route("[controller]")]
public class InvoiceReaderController : ControllerBase
{
    private readonly IBrokerageNoteExtractor _extractor;

    public InvoiceReaderController(IBrokerageNoteExtractor extractor)
    {
        _extractor = extractor;
    }

    [HttpPost("convert")]
    public async Task<ActionResult> UploadDocument(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded.");

        await using var stream = file.OpenReadStream();
        var result = await _extractor.ExtractAsync(stream, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.Failure switch
            {
                ExtractionFailureKind.Unreadable => UnprocessableEntity("The document could not be read into trades."),
                _ => StatusCode(StatusCodes.Status502BadGateway, "The extraction provider failed to produce a valid response."),
            };
        }

        CreateInvoiceExcel(result.Note!);
        return Ok(result.Note);
    }

    private static void CreateInvoiceExcel(Invoice invoice)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Ações");

        worksheet.Cell(1, 1).InsertTable(invoice.Stocks);
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string filePath = Path.Combine(desktop, $"acoes_{DateTime.Now:yyyy-MM-dd'T'HH-mm-ss}.xlsx");
        workbook.SaveAs(filePath);
    }
}
