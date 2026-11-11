# Brokerage Note Reading

Turns a brokerage note PDF into a structured list of trades, using an AI model to read the document.

## Language

**Brokerage Note**:
A PDF issued by a broker (*nota de corretagem*) listing the stock trades made on a trading day. Called `Invoice` in the current code.
_Avoid_: Invoice, receipt

**Trade**:
One buy or sell of a stock on a Brokerage Note, with ticker, signed amount and unit price. Called `Stock` in the current code.
_Avoid_: Stock, position, line item

**Extraction**:
Reading a Brokerage Note PDF and producing its Trades.
_Avoid_: Conversion, parsing, search

**Extraction Rule**:
An instruction about how raw note data maps to a Trade (a ticker ending in F drops the F; buys are positive, sells negative). Currently expressed as instructions to the model.
_Avoid_: Prompt, schema description
