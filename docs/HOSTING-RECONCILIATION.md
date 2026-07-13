# Hosting Reconciliation — docs.dotnetagents.com

Story: `8e22d10b` (OpenCore R5)

## Intent

Publish this repository's `/docs` tree as the technical content for
`docs.dotnetagents.com`, and publish `/site/www` as the marketing/announcement
source for `www.dotnetagents.com` narrative pages that belong with the examples
repo.

## Reuse Path (Do Not Fork Hosting)

| Concern | Canonical path |
| --- | --- |
| Marketing / www narrative host | Existing DO site stack (`dna-web` / jarvis-online migration epic `8e8284df`) |
| Docs static content | This repo: `docs/` |
| Announcement / quickstart copy | This repo: `site/www/` |
| Public readiness audit | DNA monorepo `scripts/audit-public-content.sh` (story `4714cb13`) |

Do **not** create a parallel hosting stack for DotNetAgents docs. Wire content
into the existing DO/dna-web pipeline when that epic's deploy path is ready.

## DNS

Subdomain configuration for `www.dotnetagents.com` / `docs.dotnetagents.com`
(W4.6) remains operator-gated at the registrar when a human click is required.
This story ships **content + reconciliation docs**; live DNS cutover is a
separate operator step with evidence recorded on closeout when performed.

## Safety Gate

Before any public publish:

```bash
# from DNA monorepo root, against this checkout
bash scripts/audit-public-content.sh --root public/dotnetagents-examples
```

Exit 0 required. No internal hostnames, ports, credentials, or private service
names in published trees.
