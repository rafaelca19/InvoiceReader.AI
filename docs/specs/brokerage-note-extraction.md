# Brokerage Note Extraction module

Status: ready-for-agent

## Problem Statement

As the (only) user of this tool, I upload a Brokerage Note PDF and get its Trades back. The flow that does this lives entirely inside the HTTP controller: it encodes the PDF, builds the OpenAI request, picks the message out of the response, parses it and writes the spreadsheet. As a result:

- Nothing about Extraction can be tested without ASP.NET and the real OpenAI API.
- The OpenAI wire format leaks into the controller, so a change to it (such as the in-progress `type` discriminator edit) touches code that has nothing to do with it.
- Provider errors are not detected: a rejected request is read as an empty response and fails later with a null reference.
- Every failure becomes a 500 with the raw exception message sent to the client.

## Solution

Introduce one deep module, the Brokerage Note extractor, whose interface is: give it PDF bytes, get back either a Brokerage Note or a typed failure. Everything about OpenAI (encoding, instruction text, schema, HTTP, status handling, response parsing) sits behind it. The controller only maps HTTP in and out. From the user's side, successful uploads behave as before, and failures return meaningful status codes instead of a generic 500.

## User Stories

1. As the user, I want to upload a Brokerage Note PDF and receive its Trades, so that I can review what I traded.
2. As the user, I want the Trades also written to an Excel file as today, so that my existing workflow keeps working.
3. As the user, I want an empty or missing upload rejected with a 400, so that I know I forgot the file.
4. As the user, I want a non-PDF upload reported as unreadable (422), so that I don't wait on a doomed provider call.
5. As the user, I want a PDF the model cannot read into Trades reported as unreadable (422), so that I don't get an empty spreadsheet.
6. As the user, I want a Brokerage Note with zero Trades treated as a failed read, so that a bad extraction is never silent.
7. As the user, I want a null or missing Trades list treated as a failed read, so that the Excel step never receives null.
8. As the user, I want an OpenAI rejection (bad key, bad model, rate limit, 4xx/5xx) reported as a 502, so that I can tell a provider problem from a bad file.
9. As the user, I want a malformed model response (no message, invalid JSON) reported distinctly, so that I can tell the model misbehaved.
10. As the user, I want exception messages never returned to the client, so that internals are not exposed.
11. As the user, I want cancellation of the request to stop the provider call, so that abandoned uploads don't keep running.
12. As the maintainer, I want Extraction callable with a plain `Stream`, so that it can be reused from something other than the HTTP controller.
13. As the maintainer, I want the OpenAI request and response formats hidden in one module, so that a wire-format change is a one-module change.
14. As the maintainer, I want only the OpenAI module to read the OpenAI configuration, so that model name, key and URL have one reader.
15. As the maintainer, I want to test Extraction without network access, so that tests are fast and deterministic.
16. As the maintainer, I want to test the controller against an in-memory fake extractor, so that status-code mapping is covered without OpenAI.
17. As the maintainer, I want provider status-code handling tested with canned responses, so that the error path that used to null-reference is covered.
18. As the maintainer, I want the Extraction Rules (drop the trailing F, signed amounts) kept as model instructions next to the extractor, so that changes to them are local.
19. As the maintainer, I want the pass-through HTTP client module removed, so that there is one fewer module to read.
20. As the maintainer, I want the glossary terms (Brokerage Note, Trade, Extraction, Extraction Rule) used in the new module's names, so that the code reads in the domain's language.

## Implementation Decisions

- **One new interface, the Brokerage Note extractor.** It takes a `Stream` of PDF bytes and a cancellation token and returns a result: success carrying the Brokerage Note, or failure carrying a kind and a message. It lives in the core part of the project, next to the entities.
- **Failure kinds:** `Unreadable` (non-PDF input, empty Trades, null Trades), `ProviderRejected` (non-success HTTP status or transport failure), `MalformedResponse` (no message in the output, content that is not valid Brokerage Note JSON). Exceptions are reserved for bugs.
- **Return type stays `Invoice`** (with `Stock`) for now. The glossary records Brokerage Note and Trade as the domain names; no rename in this change.
- **OpenAI adapter** is the only implementation. It owns: base64 data-URL encoding, the `invoice.pdf` filename, the `invoice` schema name, the Portuguese instruction text, schema generation from the entity type, the polymorphic request content (including the in-progress `type` discriminator edit, which is built upon as-is), the HTTP call, status-code checking, selecting the `message` output and deserializing it.
- **Config:** only the adapter depends on the OpenAI configuration (model, key, URL). The controller no longer depends on it.
- **Deleted:** the OpenAI HTTP client interface and implementation. The adapter uses the named `HttpClient` directly. Request and response DTOs become internal to the adapter module.
- **Controller:** depends only on the extractor. Missing or empty file returns 400. `Unreadable` returns 422. `ProviderRejected` and `MalformedResponse` return 502. No exception message is sent to the client. The Excel write to the Desktop is unchanged and still runs after a successful extraction.
- **Extraction Rules** stay as `[Description]` instructions on the entity properties. The schema generator is unchanged.
- **Startup wiring** registers the extractor interface against the OpenAI adapter and keeps the named `HttpClient` setup.

## Testing Decisions

- **One seam: the Brokerage Note extractor interface.** Tests assert on external behavior only: the result (note or failure kind) for a given input. They do not assert on request JSON structure, DTO shape, schema output or message selection.
- **OpenAI adapter tests** go through the extractor interface, substituting a stubbed `HttpMessageHandler` that returns canned response bodies and status codes. Cases: valid response, non-PDF input, 4xx and 5xx statuses, response without a message, invalid JSON, zero Trades, null Trades, cancellation.
- **Controller tests** use an in-memory fake extractor returning a canned note or each failure kind, and assert the HTTP status mapping, including 400 for an empty upload. The Excel Desktop write is not covered here (out of scope).
- **Prior art:** none. The repository has no test project yet, so a new one is created.

## Out of Scope

- Changing how Excel is produced or where it is written (stays on the Desktop; candidate 3 in the architecture review).
- Replacing the hand-rolled JSON schema generator (candidate 4).
- Moving the Extraction Rules from model instructions into code.
- Renaming `Invoice` and `Stock` to Brokerage Note and Trade in the code.
- Changing the prompt text, model, or response format.
- Authentication, rate limiting, persistence, or multiple providers.

## Further Notes

- The glossary at the repo root defines Brokerage Note, Trade, Extraction and Extraction Rule.
- No ADR is needed: the decisions are cheap to reverse and not surprising.
- A second provider adapter would make the seam hold two real adapters in production. Today the second adapter is the in-memory fake used in tests.
