# Result Envelopes And Arena Compatibility

Some examples emit a result envelope. This is a structured file format that
describes what an example run produced.

## Why It Exists

Result envelopes make example output easier to compare:

- example id and version
- run id
- timestamp
- input summary or hash
- output artifact references
- validation summary
- self-reported metrics

This helps examples become measurable without requiring a hosted service.

## Public Arena Compatibility

The public roadmap includes a gamified Arena experience where builders can
compare agents, workflows, and strategies in a challenge format.

Public examples may emit Arena-compatible result envelopes. That does not mean
the examples include scoring engines, private evaluation packs, tournament
orchestration, optimization loops, or promotion gates.

Think of the envelope as a receipt. The public example can produce it. A hosted
or premium system may later consume it.

## What To Include

Include non-secret run metadata:

- example id
- example version
- run id
- UTC timestamp
- input summary hash
- output artifact refs
- validation result
- metrics that are safe to publish

Do not include:

- raw credentials
- private endpoints
- customer data
- hidden prompts
- proprietary scoring details
- implementation notes for private evaluators
