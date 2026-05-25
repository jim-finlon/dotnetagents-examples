# Security and Compliance

The Suite is built for customers who have to answer a security review
before they can buy anything. This page is what we hand to the
prospect's security team.

## Certifications

- SOC 2 Type II (current, audited annually).
- ISO 27001 (current).
- GDPR-aligned data-processing addendum available on request.
- HIPAA: the Suite is **not** HIPAA-compliant out of the box. The
  Suite reads sales-team data, not patient data, and we do not sign
  BAAs.

## Data residency

- US tenants: data stored in `us-east-1` and `us-west-2`.
- EU tenants: data stored in `eu-central-1` (Frankfurt) and
  `eu-west-1` (Dublin); no data leaves the EU.
- Other regions: hosted out of the closest of the above two pairs;
  data residency upgrade available on Enterprise tier.

## What we read

Default Starter-tier connector reads:

- CRM deal records, activity logs, stage history.
- Email metadata (sender, recipient, subject, send/receive timestamps,
  thread id).
- Calendar metadata (invitee list, status, timestamps).

What we explicitly do not read by default:

- Email body content.
- Calendar event descriptions / attachments.
- File-share contents (the Suite does not connect to Drive / OneDrive /
  Box / Dropbox).

Message-body indexing is opt-in per customer and per use case. The
opt-in toggle ships a separate consent flow with a documented
retention window.

## Authentication

- Web UI: SAML 2.0 SSO (Okta, Azure AD, Google Workspace, Ping).
- Programmatic access: OAuth 2.0 + per-environment service accounts.
- No long-lived API keys without an explicit operator override; even
  then the key rotates on the customer-configured cadence (default
  90 days).

## Pen testing and responsible disclosure

- Third-party pen test annual; report available under NDA.
- Responsible-disclosure program: published at
  `security.mitchmurray.example`, response SLA 5 business days,
  no-questions-asked safe harbor for good-faith research.

## Data deletion

- Customer-initiated tenant delete: 30 days to backup expiry, then
  cryptographic erasure of all encryption keys.
- Per-record deletion (GDPR right-to-erasure): 30 days from request,
  audit log retained for the legal minimum.
- No data is shared with other tenants or used for training shared
  models without explicit opt-in.

## What we will and won't sign

- DPA (data-processing addendum): yes, standard form available.
- BAA: no.
- MNDA: yes.
- Custom security questionnaire: yes, target 5 business days for
  Enterprise prospects.

## Related pages

- [Integrations](06-integrations.md) — what systems the Suite touches.
- [Pricing tiers](08-pricing-tiers.md) — Enterprise tier includes the
  custom data-residency option.
