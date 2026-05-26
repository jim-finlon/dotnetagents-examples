# Extending Examples

Treat each example as a starting point, not an application framework.

## Replace The Domain Logic First

Find the smallest service or method that represents sample behavior and replace
it with your own domain logic. Keep the public shape the same until the new
behavior works.

Good first changes:

- change a greeting
- add a field to structured output
- add a read-only tool
- add validation for one argument
- write one test for the new behavior

## Keep Tool Contracts Explicit

When adding a tool:

- give it a stable name
- document arguments
- validate all inputs
- return structured output
- include clear failure messages
- avoid hidden side effects

If the tool mutates external state, add preview/confirm behavior before making
the mutation path easy to call.

## Add Plugins Deliberately

Use plugins when the example needs a real integration:

- storage for artifacts
- vector search for retrieval
- messaging for async events
- browser/computer-use for UI-only systems
- UI packages for operator interaction

Do not add a plugin just because it exists. Each integration should have a
reason and a testable failure mode.

## Update The Catalog

When you add or materially change an example, update
[`../examples/catalog.v1.json`](../examples/catalog.v1.json). Treat the catalog
as part of the example contract:

- add the smoke command;
- document optional live configuration;
- list public packages and plugin families exercised;
- include any external dependencies;
- add a public/private boundary note.

If the example exposes awkward core or plugin ergonomics, record the friction in
the delivery story using the [Friction Ledger](friction-ledger.md) template and
route a follow-up instead of hiding the workaround.

## Move From Example To Product

Before treating an example fork as production code, add:

- application-specific configuration
- logging and trace ids
- tests for invalid inputs
- secret handling through your approved secret store
- operational docs for your deployment
- policy for high-impact tools

The example should teach the pattern. Your product should own the policy.
