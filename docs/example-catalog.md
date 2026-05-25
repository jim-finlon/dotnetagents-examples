# Example Catalog

The canonical public example index is [`../examples/catalog.v1.json`](../examples/catalog.v1.json).

Use the catalog when you need to answer:

- Which examples are runnable today?
- Which public packages or plugin families does an example exercise?
- Which command proves the offline smoke path?
- Which live command is available when optional provider credentials are configured?
- Which examples are templates or reference packs rather than executable apps?
- Which public/private boundary note applies before expanding an example?

## Maturity Values

| Value | Meaning |
| --- | --- |
| `runnable` | Has a local command that executes without credentials or network access. |
| `template` | Provides a starter shape or configuration pattern; may not be a complete application. |
| `reference-pack` | Contains design/reference artifacts rather than a runnable app. |
| `scaffold` | Public placeholder for a larger example family that must not be treated as complete. |

## Required Fields For New Examples

Every new catalog entry must include:

- `id`
- `displayName`
- `path`
- `domain`
- `maturity`
- `primaryLanguage`
- `packagesExercised`
- `pluginsExercised`
- `capabilities`
- `smokeCommand`
- `liveCommand`
- `externalDependencies`
- `optionalEnvironment`
- `expectedOutput`
- `boundaryNote`

Use `null` for `smokeCommand` or `liveCommand` only when the example is a template, reference pack, or scaffold. Runnable examples should always have a smoke command.

## Public Boundary

Catalog entries should describe public-safe behavior, not private implementation details.

Good boundary note:

> Uses synthetic business scenarios; no live CRM, calendar, inbox, or customer data is required.

Bad boundary note:

> Calls our private evaluation service and routes through internal worker pools.

## Validation Checklist

Before a catalog change lands:

1. Run the smoke command for every entry you changed when it is not `null`.
2. Confirm any new docs link to the catalog path.
3. Run markdown/diff checks.
4. Run the public content audit or a targeted diff scan for the changed public files.
5. Include the changed catalog entry ids in the SDLC closeout evidence.
