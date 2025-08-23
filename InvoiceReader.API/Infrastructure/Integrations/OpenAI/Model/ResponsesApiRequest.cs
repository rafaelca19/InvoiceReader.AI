using InvoiceReader.API.Infrastructure.Serialization;
using System.Text.Json.Serialization;

namespace InvoiceReader.API.Infrastructure.Integrations.OpenAI.Model;

public class ResponsesApiRequest
{
    public ResponsesApiRequest CreateSearchDocumentRequest(string base64Document, Type jsonModel, string model)
    {
        var jsonSchema = JsonSchemaGenerator.GenerateSchema(jsonModel);
        return new ResponsesApiRequest
        {
            input =
            [
                new Input
                {
                    role = "user",
                    content =
                    [
                        new DocumentContent
                        {
                            type = "input_file",
                            filename = "invoice.pdf",
                            file_data = base64Document
                        },
                        new TextContent
                        {
                            type = "input_text",
                            text = "Extraia as informações do PDF para JSON conforme modelo enviado"
                        },
                    ],
                },
            ],
            model = model,
            text = new Text()
            {
                format = new Format()
                {
                    type = "json_schema",
                    schema = jsonSchema,
                    name = "invoice"
                }
            }
        };
    }

    public Input[] input { get; set; }
    public string model { get; set; }
    public Text text { get; set; }
}

public class Input
{
    public Content[] content { get; set; }
    public string role {get; set; }
}

public class DocumentContent : Content
{
    public string @type { get; set; }
    public string filename { get; set; }
    public string file_data { get; set; }
}
public class TextContent : Content
{
    public string @type { get; set; }
    public string text { get; set; }
}
public class Text 
{ 
    public Format format { get; set; }
}
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DocumentContent), "input_file")]
[JsonDerivedType(typeof(TextContent), "input_text")]
public class Content
{
}

public class Format
{
    public string type { get; set; }
    public object schema { get; set; }
    public string name { get; set; }
}
