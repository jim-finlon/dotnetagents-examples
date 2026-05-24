using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SalesArena.Invoice;

public interface IInvoiceAgent
{
    Invoice? OnStageChange(CrmStageEvent evt, InvoiceContext context);
}

/// <summary>
/// Deterministic invoice composition. Fires only on ClosedWon transitions; non-ClosedWon
/// events return null. Invoice numbers are deterministic from prospect + proposal tier ref.
/// </summary>
public sealed class InvoiceAgent : IInvoiceAgent
{
    public const string ClosedWonStage = "ClosedWon";

    private readonly Func<DateTimeOffset> _utcNow;

    public InvoiceAgent() : this(() => DateTimeOffset.UtcNow) { }

    public InvoiceAgent(Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public Invoice? OnStageChange(CrmStageEvent evt, InvoiceContext context)
    {
        if (evt is null) throw new ArgumentNullException(nameof(evt));
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (!string.Equals(evt.ToStage, ClosedWonStage, StringComparison.Ordinal))
            return null;

        if (context.Billing is null || string.IsNullOrWhiteSpace(context.Billing.CompanyName))
            throw new ArgumentException("Billing profile with non-blank CompanyName is required.", nameof(context));
        if (context.LineItems is null || context.LineItems.Count == 0)
            throw new ArgumentException("At least one line item is required.", nameof(context));
        foreach (var li in context.LineItems)
        {
            if (li.UnitPrice <= 0 || li.Quantity <= 0)
                throw new ArgumentException("LineItem UnitPrice and Quantity must be > 0.", nameof(context));
        }
        if (context.TaxRate < 0)
            throw new ArgumentException("TaxRate must be >= 0.", nameof(context));

        var sorted = context.LineItems
            .OrderBy(li => li.Sku, StringComparer.Ordinal)
            .ToArray();

        var subtotal = sorted.Sum(li => li.UnitPrice * li.Quantity);
        var tax = decimal.Round(subtotal * context.TaxRate, 2, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;

        return new Invoice(
            InvoiceNumber: DeterministicInvoiceNumber(context.ProspectId, context.ProposalTierRef),
            ProspectId: context.ProspectId.Trim(),
            ProposalTierRef: context.ProposalTierRef.Trim(),
            IssuedAtUtc: _utcNow(),
            Billing: context.Billing,
            LineItems: sorted,
            Subtotal: subtotal,
            TaxRate: context.TaxRate,
            Tax: tax,
            Total: total);
    }

    private static string DeterministicInvoiceNumber(string prospect, string tierRef)
    {
        var basis = $"{prospect}|{tierRef}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(basis));
        var sb = new StringBuilder("INV-", 12);
        for (int i = 0; i < 6; i++) sb.Append(bytes[i].ToString("X2"));
        return sb.ToString();
    }
}

public static class InvoiceCsvWriter
{
    public static void WriteCsv(Invoice invoice, TextWriter writer)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));
        if (writer is null) throw new ArgumentNullException(nameof(writer));

        writer.WriteLine("invoice_number,row_type,sku,description,unit_price,quantity,amount");
        foreach (var li in invoice.LineItems)
        {
            var amount = li.UnitPrice * li.Quantity;
            writer.Write(invoice.InvoiceNumber);
            writer.Write(",line,");
            writer.Write(Csv(li.Sku));
            writer.Write(',');
            writer.Write(Csv(li.Description));
            writer.Write(',');
            writer.Write(li.UnitPrice.ToString("0.00", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(li.Quantity.ToString(CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.WriteLine(amount.ToString("0.00", CultureInfo.InvariantCulture));
        }

        WriteSummaryRow(writer, invoice.InvoiceNumber, "subtotal", invoice.Subtotal);
        WriteSummaryRow(writer, invoice.InvoiceNumber, "tax", invoice.Tax);
        WriteSummaryRow(writer, invoice.InvoiceNumber, "total", invoice.Total);
    }

    private static void WriteSummaryRow(TextWriter writer, string invoiceNumber, string rowType, decimal amount)
    {
        writer.Write(invoiceNumber);
        writer.Write(',');
        writer.Write(rowType);
        writer.Write(",,,,,");
        writer.WriteLine(amount.ToString("0.00", CultureInfo.InvariantCulture));
    }

    private static string Csv(string s)
    {
        if (s is null) return string.Empty;
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
