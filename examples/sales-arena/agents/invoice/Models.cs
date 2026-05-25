using System;
using System.Collections.Generic;

namespace SalesArena.Invoice;

public sealed record BillingProfile(
    string CompanyName,
    string BillingEmail,
    string Address,
    string? TaxId = null);

public sealed record LineItem(string Sku, string Description, decimal UnitPrice, int Quantity);

public sealed record InvoiceContext(
    string ProspectId,
    string ProposalTierRef,
    BillingProfile Billing,
    IReadOnlyList<LineItem> LineItems,
    decimal TaxRate);

public sealed record CrmStageEvent(
    string ProspectId,
    string FromStage,
    string ToStage,
    DateTimeOffset AtUtc);

public sealed record Invoice(
    string InvoiceNumber,
    string ProspectId,
    string ProposalTierRef,
    DateTimeOffset IssuedAtUtc,
    BillingProfile Billing,
    IReadOnlyList<LineItem> LineItems,
    decimal Subtotal,
    decimal TaxRate,
    decimal Tax,
    decimal Total);
