# Road Access .NET Consumer Sample

This sample shows the configuration shape a public-facing .NET app should use
when it consumes one approved DNA internal service through the WireGuard
road-access tunnel.

The sample is configuration-only on purpose. Public apps should use their own
typed options and `HttpClient` wiring, but they should keep the same guardrails:

- explicit `RoadAccess:Enabled`
- one allowlisted `RoadAccess:AllowedService`
- private tunnel-only `RoadAccess:BaseUrl`
- API key read from an environment variable, never from source-controlled JSON
- short timeout and bounded retries
- safe fallback behavior when the tunnel or backend is down

Validate the sample from the repository root:

```bash
pwsh -NoProfile -File scripts/Test-RoadAccessConsumerPattern.ps1
```

Optional live probe:

```bash
export DNA_ROAD_ACCESS_API_KEY="<lab key>"
export DNA_ROAD_ACCESS_RUN_LIVE=1
pwsh -NoProfile -File scripts/Test-RoadAccessConsumerPattern.ps1
```

The live probe only uses `HealthPath` and must remain read-only.
