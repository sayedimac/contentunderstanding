# CopilotTest — quickstart (Blazor WASM + .NET 10 Functions)

Purpose
- Minimal setup for a Blazor WebAssembly static web app (dotnet 10) in src/web
- .NET 10 Azure Functions backend in src/api
- Ready for local dev and GitHub Actions deploy to Azure Static Web Apps

Prerequisites
- .NET 10 SDK installed
- (For Functions) Azure Functions Core Tools v4+ and dotnet-isolated support
- Git, optional: Azure CLI, VS Code

Recommended folder layout (root of repo)
- src/
    - web/    ← Blazor WebAssembly app
    - api/    ← Azure Functions (.NET 10 isolated)

Create projects (from repo root)
1. Create solution
     dotnet new sln -n CopilotTest
     

2. Create Blazor WASM client
     mkdir -p src
     dotnet new blazorwasm -o src/web -f net10.0

3. Create Azure Functions (dotnet-isolated) — requires Func Core Tools
     # from repo root
     func init src/api --worker-runtime dotnet-isolated --target-framework net10.0
     cd src/api
     func new --template "HTTP trigger" --name HttpTrigger --authlevel "anonymous"

4. Add projects to solution
     dotnet sln add src/web/*.csproj src/api/*.csproj

Local dev
- Build everything:
    dotnet build

- Run web locally (serves the static site)
    dotnet run --project src/web

- Run functions locally (from src/api)
    cd src/api
    func start

- Tip: set CORS/local endpoints as needed in local.settings.json for testing.

GitHub Actions (Azure Static Web Apps) — minimal workflow
- Place this workflow in .github/workflows/azure-static-web-apps.yml
- It assumes you create an Azure Static Web App and put the deployment token in repository secrets as AZURE_STATIC_WEB_APPS_API_TOKEN

name: Azure Static Web Apps CI/CD

on:
    push:
        branches:
            - main

jobs:
    build_and_deploy_job:
        runs-on: ubuntu-latest
        steps:
            - uses: actions/checkout@v4
            - name: Setup .NET
                uses: actions/setup-dotnet@v4
                with:
                    dotnet-version: '10.0.x'
            - name: Build
                run: dotnet build --configuration Release
            - name: Deploy to Azure Static Web Apps
                uses: Azure/static-web-apps-deploy@v1
                with:
                    azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
                    repo_token: ${{ secrets.GITHUB_TOKEN }}
                    action: "upload"
                    app_location: "src/web"
                    api_location: "src/api"
                    output_location: "wwwroot"

Notes & best practices
- Keep APIs lightweight (HTTP triggers) when used as backend for a static web app.
- Prefer .NET isolated Functions for .NET 10 support.
- Use CI to build both projects so the deployment artifact matches local behavior.
- Add a root README, .gitignore, and a solution-level launch/debug configs as needed.

If you want, I can:
- produce a sample HTTP trigger function code
- produce a ready-to-use GitHub Actions file with exact Azure Static Web Apps action configuration
- create solution and project files (commands/scripts) to run locally