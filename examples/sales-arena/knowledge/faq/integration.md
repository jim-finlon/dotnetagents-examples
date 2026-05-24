# FAQ — Integration

## Which CRMs does it support?

Managed connectors for Salesforce and HubSpot. Operator-configured
connector for Pipedrive. Warehouse-mode for custom CRMs (Snowflake /
Postgres / SQL Server) on Enterprise.

## Which email systems?

Microsoft 365 (Exchange Web Services) and Google Workspace (Gmail +
Calendar APIs). Both are metadata-only by default.

## Which BI tools embed the dashboard?

Power BI (custom visual, AppSource), Tableau (web data connector),
Looker (LookML block), or a plain iframe in any web app that
accepts one.

## Does it integrate with Slack / Teams?

Yes, both. Outbound: webhook for `dna_slip_risk = high` alerts.
Inbound: two-way commenting from Slack and Teams (Q2 roadmap;
GA in 6 weeks per the roadmap doc).

## Does it integrate with PagerDuty?

Yes. Outbound only — the Suite fires a PagerDuty incident on
configured high-severity deal events. Pro+ tier.

## Does it integrate with dialer systems?

Not directly today. The Suite reads call *logs* from the CRM
(Salesforce / HubSpot record-level activity log), but does not
connect to dialer APIs. Dialer integration is on the Q3 roadmap.

## Does it integrate with Marketo / Pardot / HubSpot Marketing?

MAP signals flow through the CRM. We don't connect to MAP directly
today — direct integration is a stretch goal on the Q3 roadmap.

## Does it integrate with file-share (Drive / OneDrive / Box)?

No. The Suite does not connect to file-share systems. Proposal
delivery events flow through the CRM activity log.

## How do I add a new connector?

Custom connectors are a paid implementation engagement on Enterprise
tier. Standard new-connector requests can be submitted through the
Pro+ customer portal; we prioritize new connectors based on customer
demand each quarter.

## Will my CRM data be replicated to Mitch & Murray's servers?

Yes, for the analytics engine to run. Data is stored in the
customer's tenant region per the data-residency contract. Custom
data residency available on Enterprise.

## What's the data flow at a high level?

```
CRM + Email Metadata + Calendar
   ↓ (read-only connectors)
Mitch & Murray Analytics Engine (customer's tenant region)
   ↓ (nightly batch)
Suite dashboard + BI embeds + CRM writeback + webhooks
```
