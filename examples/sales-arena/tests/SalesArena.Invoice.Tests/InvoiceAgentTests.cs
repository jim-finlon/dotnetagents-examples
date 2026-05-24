using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace SalesArena.Invoice.Tests;

public class InvoiceAgentTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static InvoiceAgent Create() => new(() => FixedNow);

    private static InvoiceContext SampleContext(decimal taxRate = 0.0875m) => new(
        ProspectId: "greybridge",
        ProposalTierRef: "pro",
        Billing: new BillingProfile("Greybridge Industries", "ap@greybridge.example", "1 Stratham Way, Boston MA"),
        LineItems: new[]
        {
            new LineItem("pro-monthly", "Pro tier monthly", 299m, 12),
            new LineItem("addon-sso", "SAML SSO add-on", 50m, 12),
        },
        TaxRate: taxRate);

    [Fact]
    public void OnStageChange_ClosedWon_EmitsInvoice()
    {
        var inv = Create().OnStageChange(
            new CrmStageEvent("greybridge", "Negotiation", "ClosedWon", FixedNow),
            SampleContext(taxRate: 0m));

        inv.Should().NotBeNull();
        inv!.ProspectId.Should().Be("greybridge");
        inv.LineItems.Should().HaveCount(2);
        inv.Subtotal.Should().Be(299m * 12 + 50m * 12);
        inv.Tax.Should().Be(0m);
        inv.Total.Should().Be(inv.Subtotal);
    }

    [Fact]
    public void OnStageChange_NotClosedWon_ReturnsNull()
    {
        var inv = Create().OnStageChange(
            new CrmStageEvent("greybridge", "Discovery", "Proposal", FixedNow),
            SampleContext());

        inv.Should().BeNull();
    }

    [Fact]
    public void LineItems_OrderedBySkuOrdinal()
    {
        var ctx = SampleContext() with
        {
            LineItems = new[]
            {
                new LineItem("zeta", "Zeta", 10m, 1),
                new LineItem("alpha", "Alpha", 10m, 1),
            },
        };

        var inv = Create().OnStageChange(new CrmStageEvent("p", "x", "ClosedWon", FixedNow), ctx);
        inv!.LineItems[0].Sku.Should().Be("alpha");
        inv.LineItems[1].Sku.Should().Be("zeta");
    }

    [Fact]
    public void Tax_AppliedToSubtotal()
    {
        var inv = Create().OnStageChange(
            new CrmStageEvent("g", "x", "ClosedWon", FixedNow),
            SampleContext(taxRate: 0.10m));

        inv!.Tax.Should().Be(decimal.Round(inv.Subtotal * 0.10m, 2, MidpointRounding.AwayFromZero));
        inv.Total.Should().Be(inv.Subtotal + inv.Tax);
    }

    [Fact]
    public void InvoiceNumber_IsDeterministic_ForSameProspectAndTier()
    {
        var inv1 = Create().OnStageChange(new CrmStageEvent("greybridge", "x", "ClosedWon", FixedNow), SampleContext());
        var inv2 = Create().OnStageChange(new CrmStageEvent("greybridge", "x", "ClosedWon", FixedNow.AddDays(7)), SampleContext());

        inv1!.InvoiceNumber.Should().Be(inv2!.InvoiceNumber);
        inv1.InvoiceNumber.Should().StartWith("INV-").And.HaveLength("INV-".Length + 12);
    }

    [Fact]
    public void InvoiceNumber_DiffersForDifferentProspect()
    {
        var inv1 = Create().OnStageChange(new CrmStageEvent("greybridge", "x", "ClosedWon", FixedNow), SampleContext() with { ProspectId = "greybridge" });
        var inv2 = Create().OnStageChange(new CrmStageEvent("northwood", "x", "ClosedWon", FixedNow), SampleContext() with { ProspectId = "northwood" });

        inv1!.InvoiceNumber.Should().NotBe(inv2!.InvoiceNumber);
    }

    [Fact]
    public void EmptyLineItems_Rejected()
    {
        var ctx = SampleContext() with { LineItems = Array.Empty<LineItem>() };
        var act = () => Create().OnStageChange(new CrmStageEvent("g", "x", "ClosedWon", FixedNow), ctx);
        act.Should().Throw<ArgumentException>().WithMessage("*line item*");
    }

    [Fact]
    public void BlankCompanyName_Rejected()
    {
        var ctx = SampleContext() with { Billing = new BillingProfile("   ", "x@y.example", "addr") };
        var act = () => Create().OnStageChange(new CrmStageEvent("g", "x", "ClosedWon", FixedNow), ctx);
        act.Should().Throw<ArgumentException>().WithMessage("*CompanyName*");
    }

    [Fact]
    public void CsvWriter_HeaderAndStableRows()
    {
        var inv = Create().OnStageChange(new CrmStageEvent("g", "x", "ClosedWon", FixedNow), SampleContext(taxRate: 0m));
        using var sw = new StringWriter();
        InvoiceCsvWriter.WriteCsv(inv!, sw);

        var csv = sw.ToString();
        csv.Should().Contain("invoice_number,row_type,sku,description,unit_price,quantity,amount");
        csv.Should().Contain(",line,addon-sso,SAML SSO add-on,");
        csv.Should().Contain(",line,pro-monthly,Pro tier monthly,");
        csv.Should().Contain(",subtotal,");
        csv.Should().Contain(",tax,");
        csv.Should().Contain(",total,");
    }
}
