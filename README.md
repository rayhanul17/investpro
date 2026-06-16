# FlexCms.InvestPro

Scaffolded by the FlexCms module template. The wiring is complete out of the box:

- `InvestProModule` — module entry point implementing `IFcmsModule`
- `Data/InvestProDbContext` — module-owned EF DbContext + `InvestProItem` entity
- `Controllers/AdminInvestProController` — admin CRUD + server-side DataTables
- `Controllers/PublicInvestProController` — read-only public JSON endpoint
- `Views/AdminInvestPro/` — Index / Create / Edit Razor views
- `Resources/i18n/{en,bn}.json` — translation stubs

## Workflow

```bash
# 1. Generate the EF migration (once, from this folder)
dotnet ef migrations add InitialSchema --context InvestProDbContext

# 2. Build
dotnet build

# 3. Drop the built DLL + module.json into the host's modules/<ModuleId>/ folder
#    (or upload via /admin/modules) and restart the host. The framework will:
#    - run the migration
#    - upsert the declared permissions
#    - seed the menu item
#    - run SeedDataAsync once
```

## Adding more entities

1. Define the entity class inheriting `BaseEfEntity` in `Data/`
2. Add a `DbSet<>` to `InvestProDbContext`
3. Add another `dotnet ef migrations add ...`
4. Build + drop into host + restart

## Adding more permissions

Add a constant pair to `InvestProPermissions` (short key + fully-qualified key)
AND a `new FcmsPermissionDef(...)` entry in `GetPermissions()`. ModuleActivationService
upserts on every restart — no manual SQL.
