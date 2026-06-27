# Invoice Agent

Deterministic invoice composition + CSV writer for the Sales Arena ([SALES-ARENA-FLAGSHIP-PLAN.md §5](../../../../examples/sales-arena/README.md)).
Slice scope: story `51ba31db` — child of parent SA-01-08 (`77f86038`).

## Surface

- `IInvoiceAgent` + `InvoiceAgent` — fires only on `CrmStageEvent.ToStage == "ClosedWon"` transitions; non-ClosedWon events return `null`.
- Invoice numbers are deterministic: `INV-` + first 12 hex chars of SHA-256(`prospectId|proposalTierRef`). Same input → same number.
- `InvoiceCsvWriter.WriteCsv(invoice, TextWriter)` emits a stable header + line rows (sorted by SKU) + subtotal/tax/total summary rows.
- Records: `InvoiceContext`, `BillingProfile`, `LineItem`, `Invoice`, `CrmStageEvent`.

## Deferred to follow-up

- PDF rendering via `DotNetAgents.MultiModal`.
- Email delivery via Communications Agent (SA-01-07) once it ships an outbound interface.
- Live wiring to SA-01-01 CRM publisher (subscribe to real `CrmStageChanged` stream).
- Currency support beyond USD.
