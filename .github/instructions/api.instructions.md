---
applyTo: "src/api"
---

- This is a c# (dotnet 10.0) Function App that runs in Isolated Mode as part of a Static Web app in Azure
- It connects to Azure Foundry (New) using environment variables (agent service) and allows interaction from the front-end
- There is at least one function that greets a user by name  (string name parameter) - this will get called by the front-end which will eventually expand to integrate the core capabilities
