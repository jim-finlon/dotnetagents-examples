# Research Agent

Deterministic 1-pager assembler for the Sales Arena ([SALES-ARENA-FLAGSHIP-PLAN.md §5](../../../../docs/public/SALES-ARENA-FLAGSHIP-PLAN.md)).
Slice scope: story `7b756c99` — child of parent SA-01-08 (`77f86038`).

## Surface

- `IResearchAgent` + `ResearchAgent` — assembles a `ResearchOnePager` from injected providers.
- `IPublicFeedAdapter` — interface with an allow-listed `FetchAsync(prospectId, allowedHosts, ct)` contract. Live HTTP/RSS is deferred.
- `InMemoryPublicFeedAdapter` / `InMemoryCompanyFactProvider` / `InMemoryKnownContactProvider` — deterministic test impls.
- `ResearchOnePager.ToMarkdown()` extension renders a stable Markdown document.

## Allow-list filter

The `InMemoryPublicFeedAdapter` enforces the request's `AllowedFeedHosts` allow-list by host name (case-insensitive). If the list is empty, all feed items pass through — the live HTTP adapter (follow-up) will tighten this to require a non-empty allow-list.

## Deferred to follow-up

- Live HTTP/RSS adapter with retry, parser, and mandatory allow-list.
- Integration with SA-01-01 CRM Agent (consume prospect ids) and SA-01-05 Meeting Agent (feed into Brief).
- Source quality scoring + dedupe.
