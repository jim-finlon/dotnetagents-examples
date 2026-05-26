# Plugin Showcase Example

This example demonstrates how public DotNetAgents plugin families are configured and used within runnable applications. It covers the following 7 adapter families:
1. **Vector Stores** (`IVectorStoreAdapter`)
2. **Messaging** (`IMessagingPublisher`)
3. **Storage & Artifacts** (`IArtifactStore`)
4. **Database Tooling** (`IDatabaseQueryExecutor`)
5. **Browser & Computer Use** (`IBrowserDriver`)
6. **UI Approval** (`IUiApprovalService`)
7. **Multimodal & Media** (`IMultimodalProcessor`)

## Project Structure

- **`PluginShowcaseDemo.csproj`**: Console application target using .NET 10.
- **`Program.cs`**: Main entry point defining core interfaces, mock/offline fallbacks, and command routing.

## Commands

### Smoke Verification (Offline Mode)

Runs a deterministic scenario for all 7 plugin families using fakes:
```bash
dotnet run --project public/dotnetagents-examples/examples/plugin-showcase -- --smoke
```

### Interactive Demos

To execute a demo flow for a specific plugin family, run:
```bash
dotnet run --project public/dotnetagents-examples/examples/plugin-showcase -- run vector
dotnet run --project public/dotnetagents-examples/examples/plugin-showcase -- run messaging
dotnet run --project public/dotnetagents-examples/examples/plugin-showcase -- run storage
dotnet run --project public/dotnetagents-examples/examples/plugin-showcase -- run database
dotnet run --project public/dotnetagents-examples/examples/plugin-showcase -- run browser
dotnet run --project public/dotnetagents-examples/examples/plugin-showcase -- run ui
dotnet run --project public/dotnetagents-examples/examples/plugin-showcase -- run multimodal
```

---

## Plugin Families Configuration

### 1. Vector Stores
- **Abstraction**: `IVectorStoreAdapter`
- **Packages**: `DotNetAgents.VectorStores.PostgreSQL`, `DotNetAgents.VectorStores.Qdrant`
- **Configuration**:
  - `DotNetAgents:Plugins:VectorStore:ConnectionString`
  - `DotNetAgents:Plugins:VectorStore:IndexName`
- **Security**: Bound to specific collections/tables; query limits (`TopK`) are strictly capped.

### 2. Messaging
- **Abstraction**: `IMessagingPublisher`
- **Packages**: `DotNetAgents.Agents.Messaging.RabbitMQ`, `DotNetAgents.Agents.Messaging.Redis`
- **Configuration**:
  - `DotNetAgents:Plugins:Messaging:HostName`
  - `DotNetAgents:Plugins:Messaging:Port`
- **Security**: Payloads must be sanitized; avoid sending private variables or tokens on broad event buses.

### 3. Storage & Artifacts
- **Abstraction**: `IArtifactStore`
- **Packages**: `DotNetAgents.Storage.ArtifactStore`
- **Configuration**:
  - `DotNetAgents:Plugins:Storage:RootDirectory`
- **Security**: Strict path-traversal validation; artifacts are isolated by run ID.

### 4. Database Tooling
- **Abstraction**: `IDatabaseQueryExecutor`
- **Packages**: `DotNetAgents.Database.PostgreSQL`
- **Configuration**:
  - `DotNetAgents:Plugins:Database:ConnectionString`
- **Security**: Database connections default to read-only. Mutating operations are forbidden unless wrapped in transaction hooks with explicit permissions.

### 5. Browser & Computer Use
- **Abstraction**: `IBrowserDriver`
- **Packages**: `DotNetAgents.Browser.Playwright`
- **Configuration**:
  - `DotNetAgents:Plugins:Browser:Headless`
- **Security**: Browser environments run inside sandboxed containers. No internet access is allowed except to authorized, public-safe targets.

### 6. UI Approval
- **Abstraction**: `IUiApprovalService`
- **Packages**: `DotNetAgents.Ui.Approval`
- **Configuration**:
  - `DotNetAgents:Plugins:Ui:WebhookUrl`
- **Security**: Operator confirmation is requested before executing high-impact, mutating, or costly operations.

### 7. Multimodal & Media
- **Abstraction**: `IMultimodalProcessor`
- **Packages**: `DotNetAgents.Multimodal.Media`
- **Configuration**:
  - `DotNetAgents:Plugins:Multimodal:Provider`
- **Security**: Media payloads are checked for size limits and PII before transmission.

---

## Security Notes

1. **No Committed Secrets**: Connection strings and API keys should be injected using environment variables at runtime, never hardcoded in the codebase.
2. **Local Fallback Design**: All examples support fallback to local, in-memory or flat-file modes to enable zero-dependency local development and CI testing.
