# Integrations

The Suite reads from the customer's existing stack and writes back
only the analytics outputs the customer chooses to surface. Every
integration is a documented connector — no scraping, no
screen-scraping, no email plug-ins that intercept message bodies.

## CRMs the Suite reads from

- Salesforce (REST API + Connected App)
- HubSpot (Private App, OAuth)
- Pipedrive (API key, single-region)
- Custom CRM (Postgres / SQL Server / Snowflake direct via the Suite's
  data-warehouse mode — schema-mapping file required)

The Salesforce and HubSpot connectors are managed; the Pipedrive and
custom-CRM modes are operator-configured.

## Email / calendar signal sources

- Microsoft 365 (Exchange Web Services, metadata-only by default).
- Google Workspace (Gmail + Calendar APIs, metadata-only by default).
- Outlook Desktop + Mac Mail via Exchange.

Message-body indexing is opt-in per customer. The opt-in toggles a
separate consent flow with a documented retention window (default 90
days, configurable).

## BI tool embeds

- Power BI (custom visual, AppSource).
- Tableau (web data connector).
- Looker (LookML block).
- Plain iframe for any tool that accepts one.

## Outbound webhooks

Every signal the engine writes back to the CRM can also fire a
webhook to the customer's choice of receiver: Slack, MS Teams,
PagerDuty (for `dna_slip_risk = high` alerts on enterprise deals),
custom HTTPS endpoint.

## What we don't integrate with (today)

- Phone systems (call recording / dialer events). The Suite reads
  call *logs* from the CRM but does not connect to dialer APIs
  directly.
- Marketing-automation platforms (Marketo, Pardot, etc.). MAP signals
  must flow through the CRM first.
- SaaS-spend management tools (Vendr, etc.).

These are on the [roadmap](09-roadmap.md) under "stretch" for the
next two quarters.

## Implementation effort

- Salesforce + Google Workspace: 1 day, no IT effort beyond the
  Connected App install.
- HubSpot + Microsoft 365: 1 day, requires a tenant admin for the
  initial OAuth.
- Custom CRM via warehouse: 5-15 business days, depends on schema
  cleanliness.

## Related pages

- [Security and compliance](07-security-and-compliance.md) — how
  data flows between systems.
- [Pricing tiers](08-pricing-tiers.md) — which connectors are
  Starter vs Pro vs Enterprise.
