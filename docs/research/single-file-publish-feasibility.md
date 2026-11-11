# Single-file self-contained win-x64 publish: feasibility

Date: 2026-11-10. Target: InvoiceReader.API (net9.0, `Microsoft.NET.Sdk.Web`, MVC controllers, ClosedXML 0.105.0, ASP.NET runtime pack 9.0.7), SDK 9.0.302.

Two kinds of evidence are used and labelled:
- **[Doc]** quoted or paraphrased from Microsoft docs (linked).
- **[Tested]** measured by publishing a scratch copy of this project outside the repo (no app code in the repo was changed). The scratch copy added `UseDefaultFiles/UseStaticFiles`, a `wwwroot/index.html`, a `/diag` endpoint and, in a second round, `ContentRootPath = AppContext.BaseDirectory`.

## Summary

| # | Question | Answer |
|---|---|---|
| 1 | Does single-file self-contained work with static files, appsettings, env vars, ClosedXML? | Yes, with one required code change (content root) and one packaging fact (wwwroot ships as loose files, not inside the exe). |
| 2 | Is trimming safe? | No. Keep it off. MVC is documented as not trim-compatible. |
| 3 | Exe size | About 106 MB uncompressed, about 49 MB with `EnableCompressionInSingleFile`. Trimmed would be about 21 MB but is not safe. |
| 4 | Auto-open browser | `ApplicationStarted` callback + `Process.Start` with `UseShellExecute = true`. |
| 5 | Fixed port taken | Kestrel throws `AddressInUseException`, the host fails to start and the process exits. Must be handled explicitly. |
| 6 | Gotchas | Content root is the current directory, not the exe folder; `UseHttpsRedirection` logs a warning and does nothing without an HTTPS endpoint; no dev cert on HTTP-only. |

## 1. Does it work cleanly?

Project properties (from the overview doc sample): `PublishSingleFile=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64`. [Doc: single-file overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)

- **Native libraries.** "Only managed DLLs are bundled with the app ... the native binaries of the core runtime itself are separate files. To embed those files for extraction and get one output file, set the property `IncludeNativeLibrariesForSelfExtract` to `true`." Extraction goes to `%TEMP%/.net` on Windows, or `DOTNET_BUNDLE_EXTRACT_BASE_DIR` if set. [Doc: overview, Native libraries](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#native-libraries)
- `*.runtimeconfig.json` and `*.deps.json` are included in the single file. [Doc: overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- IIS in-process hosting is not supported with single-file (irrelevant: self-hosted Kestrel). [Doc: overview, Other considerations](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#other-considerations)

### Static files (wwwroot) [Tested]
`dotnet publish` with `PublishSingleFile` produced `InvoiceReader.API.exe`, `.pdb`, `appsettings.json`, `appsettings.Development.json`, `web.config`, `InvoiceReader.API.staticwebassets.endpoints.json` and a **loose `wwwroot/` folder**. wwwroot is NOT embedded in the exe.
- The exe copied alone into an empty folder runs, but `/` returns 404 and logs `The WebRootPath was not found: ...\wwwroot. Static files may be unavailable.`
- So the distribution is "exe + wwwroot folder" (zip it), unless wwwroot is embedded (for example `ManifestEmbeddedFileProvider` passed to `UseStaticFiles`). The embedding route was not built or tested. The overview doc suggests embedded resources "to avoid shipping loose files entirely". [Doc: overview, API incompatibility](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#api-incompatibility)
- `ExcludeFromSingleFile` metadata exists for files deliberately kept beside the exe. [Doc](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#exclude-files-from-being-embedded)
- ASP.NET lists StaticFiles as compatible with Native AOT, the stricter model. [Doc: ASP.NET Native AOT compatibility table](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot?view=aspnetcore-9.0)

### appsettings.json [Doc + Tested]
- Default app config order: appsettings.json, appsettings.{Environment}.json, user secrets (Development only), environment variables, command-line arguments. [Doc: Web Host](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host?view=aspnetcore-9.0)
- appsettings files are published loose beside the exe [Tested]. The exe also starts with no appsettings.json at all, because the file is optional [Tested].
- `appsettings.Development.json` is published too; do not ship dev values. Environment defaults to `Production` when `ASPNETCORE_ENVIRONMENT` is unset. [Doc: Web Host](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host?view=aspnetcore-9.0)

### Environment variable config [Tested]
`OpenAI__ApiKey=sk-test` was read as `Configuration["OpenAI:ApiKey"]` by the single-file exe (the `__` to `:` mapping is standard .NET configuration behaviour). The existing `Configure<OpenAIConfigurations>(GetSection("OpenAI"))` binding therefore works unchanged. Program.cs reads `options.ApiKey` when the HttpClient is created, so a missing key surfaces on first request, not at startup.

### ClosedXML [Tested + search]
- The trimmed publish produced **no warning attributed to ClosedXML or DocumentFormat.OpenXml**; all IL2026/IL2070/IL2104 warnings came from ASP.NET MVC or this repo's own code. That is absence of warnings, not proof of trim safety, and no end-to-end ClosedXML run was done in a published exe.
- Untrimmed, single-file bundling embeds managed assemblies, so a pure-managed library like ClosedXML needs no special handling.
- I found no ClosedXML issue or doc about single-file or trimming/AOT support. The one related hit (Microsoft Q&A, ClosedXML failing in a published WebAssembly app) concerns WASM, not Windows. [Q&A](https://learn.microsoft.com/en-us/answers/questions/2260971/closedxml-new-xlworkbook(memorystream)-works-in-vs) (secondary source, not authoritative).
- Recommendation: smoke-test the real `/convert` endpoint (producing the xlsx) from the published exe before release.

## 2. Trimming: keep it off

- MVC is "Not supported" for Native AOT, and the Native AOT template "uses Minimal APIs only, as MVC isn't yet compatible". [Doc: ASP.NET Native AOT](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot?view=aspnetcore-9.0)
- Trimming removes code that is not statically reachable; reflection-based serializers are listed as known incompatibilities. [Doc: incompatibilities](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/incompatibilities), [Doc: trimming options](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options)
- [Tested] `PublishTrimmed=true` on this project emitted roughly 100 trim warnings (the build still succeeds, so failures would show only at runtime):
  - The `AddControllersCore` warning text is literally "MVC does not currently support trimming or native AOT. https://aka.ms/aspnet/trimming".
  - `SystemTextJsonInputFormatter` / `OutputFormatter` use reflection-based `JsonSerializer`; MVC model binding and `ActionEndpointFactory` are flagged.
  - This repo's own code: `JsonSchemaGenerator` (reflection, IL2070), `OpenAIBrokerageNoteExtractor` (IL2026), `AddControllers`/`MapControllers` in Program.cs (IL2026).
  - `Microsoft.AspNetCore.Mvc.Core`, `Mvc.DataAnnotations`, `Mvc` assemblies each report IL2104 (not trim-compatible).
- A trimmed exe was built but not exercised end to end. Microsoft's guidance is that an app with trim/AOT warnings "might not work correctly". [Doc](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/native-aot?view=aspnetcore-9.0)
- Use compression to reduce size instead (no correctness risk).

## 3. Exe size [Tested; exact exe, .NET 9.0.7 runtime, win-x64]

| Variant | Exe size |
|---|---|
| Single file, self-contained, `IncludeNativeLibrariesForSelfExtract` | 105.6 MB (110,702,091 bytes) |
| plus `EnableCompressionInSingleFile=true` | 49.4 MB (51,775,131 bytes) |
| plus `PublishTrimmed=true` (not recommended) | about 20.7 MB |

Docs: the self-contained single file "is large since it includes the runtime and the framework libraries"; compression "can significantly reduce the size" but costs start-up time because assemblies are decompressed into memory, so "measure both". [Doc: overview, Compress assemblies](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#compress-assemblies-in-single-file-apps). The compressed exe started and served requests within the roughly 6 second waits in my tests; start-up was not benchmarked precisely. The exe is unsigned, so Windows SmartScreen may warn on a downloaded copy (general Windows behaviour, not verified here).

## 4. Auto-open the default browser

Pattern (standard APIs; not executed in this research):
```csharp
app.Lifetime.ApplicationStarted.Register(() =>
{
    var url = app.Urls.FirstOrDefault() ?? "http://localhost:5190";
    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
});
```
- `ApplicationStarted` is triggered when "The host is fully started". [Doc: Web Host, IHostApplicationLifetime](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host?view=aspnetcore-9.0)
- On modern .NET `UseShellExecute` defaults to false; it must be true to open a URL with the shell's default handler. [Doc: ProcessStartInfo.UseShellExecute](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.useshellexecute)
- With port 0, the real address must be read after start (`app.Urls` or `IServerAddressesFeature`). [Doc: Kestrel endpoints, dynamic port binding](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-9.0)
- Wrap in try/catch and skip it in Development.

## 5. Fixed localhost port already in use [Tested + Doc]

[Tested] A second instance on the same `http://localhost:5190` printed `fail: Microsoft.Extensions.Hosting.Internal.Host[11] Hosting failed to start` / `System.IO.IOException: Failed to bind to address http://127.0.0.1:5190: address already in use` (inner `AddressInUseException`, socket error 10048) and the process exited. `ApplicationStarted` never fires, so no browser opens.

[Doc] "If the requested port is in use by another service on either loopback interface, Kestrel fails to start." [Doc: Kestrel endpoints, localhost](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-9.0)

Handling options:
1. **Fail clearly**: catch the exception around `app.Run()`, show a friendly message ("already running, open http://localhost:PORT"), optionally open the browser at the existing instance, then exit. Covers the "double-clicked twice" case.
2. **Fall back to a free port**: probe the preferred port, otherwise bind port 0 via `ASPNETCORE_URLS`/`UseUrls("http://127.0.0.1:0")` and read the real URL from `app.Urls`. Dynamic binding is not available when code uses `ListenLocalhost`. [Doc](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-9.0)
3. **Named `Mutex`** for single-instance detection, to tell "this app already running" from "another process owns the port".

Default URL with no configuration is `http://localhost:5000`; `ASPNETCORE_URLS` or `--urls` override it. [Doc: Kestrel endpoints](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-9.0) The repo's `launchSettings.json` (5190) is a dev-only file that is not published; the exe will not use it. Program.cs currently sets no URL, so the port must be set in code or env.

## 6. Gotchas

1. **Content root is the current working directory, not the exe folder.** `CreateDefaultBuilder` "Sets the content root to the path returned by GetCurrentDirectory". [Doc: Web Host](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host?view=aspnetcore-9.0) .NET 7 added one exception: if the cwd is the Windows System special folder it defaults to `AppContext.BaseDirectory`. [Doc: breaking change](https://learn.microsoft.com/dotnet/core/compatibility/extensions/7.0/contentrootpath-hosted-app)
   [Tested] Launching the single-file exe with cwd `C:\Windows` gave `ContentRootPath = C:\Windows`, `WebRootPath = null`, and `/` returned 404 (appsettings.json would likewise not be found). This bites shortcuts with a different "Start in", scheduled tasks and launch-from-another-folder.
   **Fix [Tested]:** `WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory })`. With it, launching from `C:\Windows` served `wwwroot/index.html` and `WebRootPath` pointed beside the exe. Docs recommend `AppContext.BaseDirectory` for files next to the exe and note `Assembly.Location` returns an empty string in single-file. [Doc: overview, API incompatibility](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#api-incompatibility)
2. **HTTPS redirection.** Program.cs calls `app.UseHttpsRedirection()`. With only an HTTP endpoint, requests still work but it logs `Failed to determine the https port for redirect.` [Tested]. Remove it, or guard it, for the localhost-HTTP exe. `https_port` only sets the redirect target; it does not make the server listen. [Doc: Web Host, HTTPS port](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host?view=aspnetcore-9.0)
3. **Dev certificate.** It "isn't installed in nondevelopment environments" and is per-user; an HTTPS endpoint with no certificate "throws an exception and fails to start". [Doc: Kestrel endpoints, HTTPS development certificate](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-9.0) So ship HTTP on loopback only for this design, and bind to `localhost`/`127.0.0.1`, not `*`, so the API-key-backed endpoint is not exposed to the LAN.
4. **Environment defaults to Production**: `MapOpenApi()` (Development only in Program.cs) is off, and `launchSettings.json` env vars do not apply. [Doc: Web Host](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/web-host?view=aspnetcore-9.0)
5. **Files that travel with the exe**: `wwwroot/` is required; `appsettings.json` is optional; `web.config`, `.pdb` and `staticwebassets.endpoints.json` are emitted but not needed for self-hosting (the exe ran without them in the isolated-folder test, which had only the exe and no wwwroot).
6. **Extraction.** With `IncludeNativeLibrariesForSelfExtract`, native runtime files are extracted under `%TEMP%\.net` (the `.net\InvoiceReader.API` folder was present after my runs). Locked-down TEMP or antivirus can interfere; `DOTNET_BUNDLE_EXTRACT_BASE_DIR` overrides. [Doc: overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview#native-libraries)
7. **Env var timing.** `OpenAI__ApiKey` is read at process start; a newly set user variable is not visible to already-running shells/Explorer. An empty `OpenAI:ApiKey` in shipped appsettings.json is overridden by the env var, since env vars load later in the provider order [Doc: Web Host order; Tested].
8. Kestrel timeouts configured in Program.cs carry over unchanged.

## Suggested publish command (for the owner's script)

```
dotnet publish InvoiceReader.API -c Release -r win-x64 --self-contained ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true -o publish
```
No `PublishTrimmed`. Verified to build and run on a scratch copy; output is the exe plus `wwwroot/` and loose json files. The same properties can live in the csproj.

## Not verified
- Trimmed exe runtime behaviour (only warnings and size were checked).
- End-to-end ClosedXML output from the published exe.
- Embedding wwwroot into the exe.
- The browser-launch snippet and the port-fallback options (only the port-in-use failure itself was observed).
- Compression start-up cost.
