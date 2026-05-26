# Acme Customer Support SLAs

- Priority-0 (P0) Issues: Critical issues causing full system outage. Response SLA is 15 minutes. Escalation path: contact VP of Operations immediately.
- Priority-1 (P1) Issues: High priority issues causing partial degradation. Response SLA is 1 hour. Escalation path: contact Director of Engineering.
- Priority-2 (P2) Issues: Moderate issues. Response SLA is 4 hours.

# Security and Data Policies

- Data Redaction: All customer-identifying information (PII) must be redacted before sending data to external inference providers.
- Secrets: Never commit credentials, API keys, or database connection strings to source control.
