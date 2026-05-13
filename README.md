# Content Understanding Static Web App

This repository contains a minimal Azure Static Web Apps solution built with:

- `src/web`: .NET 10 Blazor WebAssembly frontend
- `src/api`: .NET 10 Azure Functions isolated backend

The app lets authenticated users submit a JSON schema that describes the information they want Azure Content Understanding to extract from their content.

## Azure app registration and permissions

1. Create a Microsoft Entra app registration for the Static Web App sign-in flow.
2. Add a **Single-page application** redirect URI:
   - `https://<your-static-web-app>/.auth/login/aad/callback`
3. Configure Microsoft Entra ID as the identity provider for the Azure Static Web App.
4. Grant the Function app identity access to the Azure AI Foundry Content Understanding resource:
   - **Cognitive Services Contributor** to create or update analyzers
   - **Cognitive Services User** to invoke the service
5. Set these application settings on the Function app:
   - `CONTENT_UNDERSTANDING_ENDPOINT`
   - `CONTENT_UNDERSTANDING_API_VERSION` (optional, defaults to `2025-11-01`)
   - `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_CLIENT_SECRET` if you are using an app registration instead of managed identity

The frontend includes `staticwebapp.config.json` so only authenticated users can access the UI or call `/api/*`.

## Local development

```bash
dotnet build ContentUnderstanding.slnx
dotnet run --project /home/runner/work/contentunderstanding/contentunderstanding/src/web/web.csproj
```

To run the Functions app locally, install Azure Functions Core Tools and copy `src/api/local.settings.example.json` to `src/api/local.settings.json` before running `func start` from `src/api`.
