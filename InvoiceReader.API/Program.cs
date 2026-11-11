using InvoiceReader.API.Core.Abstractions;
using InvoiceReader.API.Infrastructure.Integrations.OpenAI;
using InvoiceReader.API.Infrastructure.Integrations.OpenAI.Model;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);


builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
});

builder.Services.Configure<OpenAIConfigurations>(
    builder.Configuration.GetSection("OpenAI"));

builder.Services.AddHttpClient("OpenAI", (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<OpenAIConfigurations>>().Value;
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddSingleton<IBrokerageNoteExtractor, OpenAIBrokerageNoteExtractor>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
