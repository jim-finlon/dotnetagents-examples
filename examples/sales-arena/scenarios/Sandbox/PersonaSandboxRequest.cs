namespace SalesArena.Sandbox;

public sealed record PersonaSandboxRequest(
    string Persona,
    IReadOnlyList<SandboxStep> Steps);
