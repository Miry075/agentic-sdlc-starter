# Knowledge Gateway: run it

Design: [DESIGN.md](DESIGN.md) (read "As built" first).

## Try it in two minutes (dev mode, no Azure)

Needs the .NET 10 SDK.

```bash
dotnet run --project src/Knowledge.Gateway
```

It starts on `http://localhost:5280` in **Dev** mode: no sign-in, a fixed user with the `developers` role, the sample repository in `samples/demo-repo`, and work items read from JSON files. Dev mode is for your own machine only. It refuses to start when `ASPNETCORE_ENVIRONMENT=Production`.

Connect Claude Code:

```bash
claude mcp add --transport http knowledge-gateway http://localhost:5280/mcp
```

Then ask it, for example: *"Using the knowledge gateway, in phase requirements, what does 'credit note' mean?"* Every tool takes a required `phase` argument (`requirements`, `design`, `build`, `test`, `release`). Try the same question in phase `test` to see the denial, and `get_work_item` for work item 41 to see the e-mail address redacted.

Health: `GET /health/live`, `GET /health/ready`.

## Build and test

```bash
dotnet build Knowledge.Gateway.sln
dotnet test  Knowledge.Gateway.sln
```

If the solution file is rejected by your SDK, regenerate it: `dotnet new sln -n Knowledge.Gateway && dotnet sln add src/*/*.csproj tests/*/*.csproj`.

The package versions for the MCP SDK, Microsoft.Identity.Web, Azure.Identity and Azure Monitor are floating (`*`) until the first successful restore. After the first green build, pin them to the versions that restored (`dotnet list package`).

## Shared deployment (Entra ID)

1. Register an app for the gateway in Entra ID. Expose an API (`api://knowledge-gateway`). Add the `groups` claim to the access token ("Groups assigned to the application"). For on-behalf-of calls to Azure DevOps, grant the app the Azure DevOps `user_impersonation` delegated permission and configure a credential (federated credential or certificate) in `Auth:Entra:ClientCredentials`.
2. Fill in `Auth:Entra` and `Auth:RoleMap` (group object ID to role name), `Repo:AzureRepos` and `WorkItems:AzureDevOps` in configuration (see `appsettings.json`). Give the gateway's managed identity read access to the repository.
3. Deploy the container (`docker build -t knowledge-gateway .`). The policy file is copied into the image: a policy change is a reviewed pull request and a redeploy.
4. Set `APPLICATIONINSIGHTS_CONNECTION_STRING` to send audit events to Azure Monitor. Without it they go to the console as JSON.

## Connect another MCP server

1. Add it to `Upstreams` in configuration (stdio or https). Example for Graphify:

```json
"Upstreams": [
  { "Name": "graphify", "Transport": "Stdio", "Command": "python",
    "Args": ["-m", "graphify.serve", "/graphs/billing-api/graph.json"] }
]
```

2. Declare it in `policy/phase-access.yaml` under `upstreams:` with the tools you want and **no pin yet**, then see what it offers:

```bash
dotnet run --project src/Knowledge.Gateway -- upstreams list
dotnet run --project src/Knowledge.Gateway -- upstreams approve graphify
```

3. Paste the printed pins into the policy, add the tools (as `graphify.query_graph`) to the phases and roles that may use them, and open a pull request. Tools the upstream offers but the policy does not declare are never exposed. If an upstream later changes a tool's description or schema, the pin no longer matches and the tool is disabled until re-approved.

To let agents read an upstream's resources, add `resources: { allow: ["graphify://node/*"], phases: [design] }`. They then get a `graphify.read_resource` tool, limited to those URIs and phases.

## Layout

```text
src/Knowledge.Core         contracts only
src/Knowledge.Policy       policy file, validation, phase policy
src/Knowledge.Redaction    sensitive-data masking
src/Knowledge.Application  ToolGuard, ToolDispatcher, L1 and L4 tools
src/Knowledge.Adapters     file system, Azure Repos, Azure DevOps, dev work items
src/Knowledge.Upstreams    upstream MCP connector (pins, breaker, resources)
src/Knowledge.Gateway      ASP.NET Core host: auth, MCP endpoint, composition
tests/                     one test project per source project
samples/demo-repo          sample data for dev mode
```
