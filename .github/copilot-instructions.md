# Copilot Instructions

## Project Guidelines
- In this ERP Blazor app, all pages must require authentication and users should be redirected to Identity login immediately on app open when unauthenticated. 
- Implement this behavior using a runtime URL-based redirect suitable for deployment, rather than via launchSettings.
- Use Clean Architecture organization where:
  - Infrastructure contains interfaces
  - Domain contains models
  - Application contains implementations of interfaces
  - A separate Storage project contains DB-related data access.
- In client management, treat Client Secret as a list since a client can have multiple secrets.
- In the Backoffice, ensure Index pages display only listings; creation should be on a separate Create page.
- In the Backoffice of Identity, UserClaims (ApiScopes/IdentityResources) and Scopes/UserClaims (ApiResources) are optional and should not trigger required field validation when submitted empty.
- The Blazor front ends (Erp.Main and Erp.Identity) are Blazor Web Apps using the Interactive Server render mode (`AddInteractiveServerComponents` + `AddInteractiveServerRenderMode`), not Blazor WebAssembly. Never add WebAssembly-only settings such as `inspectUri` in launchSettings.
- Organize the App.razor, Routes.razor, and _Imports.razor files within a dedicated folder in both projects (Erp.Main and Erp.Identity).
- Migrate all pages in Erp.Identity to Blazor components, discontinuing the use of Razor Pages.
- Standardize the visual layout across Erp.Main, Erp.Identity, and secondary projects by using MudBlazor with a consistent left-side menu and layout structure throughout the application.
- In Erp.Identity, remove the 'Account' group from the side menu, keep logout functional only in the header, and use 'Home' instead of 'Dashboard', showing only user data on the home page. Additionally, implement a collapsible/expandable submenu and display the Backoffice menu only for SuperAdmin users.
- Apply layout/UX changes consistently across all Backoffice pages in Identity.
- In Erp.Identity, Account pages that are unauthenticated (SignIn, SignUp, ForgotPassword, ResetPassword and confirmations) must use a dedicated auth layout without header and side menu, with centered auth content over a background image.
- In Identity Backoffice listings, keep the primary add/create action aligned to the right and maintain consistent list header spacing and typography.
- In Identity Backoffice listings, use enhanced table styling consistently (improved header, zebra rows, hover states, and balanced cell spacing).
- In Identity Backoffice edit pages with multiple related sections, organize sections using tabs (as in Clients edit) instead of long linear forms.
- In Identity Backoffice listings, represent Create/Edit/Delete actions with icon buttons and keep the Actions column as the final table column.
- Use Controllers instead of Minimal APIs to organize endpoints.
- All page routes and navigation links in Erp.Main must be in English (`/invoices`, `/companies`, `/products`), even though the UI text shown to users stays in Portuguese.
- The business modules (Core, Sales, Notification, and future ones) run in a single host, `src/Erp.Api`, with controllers grouped per module in `Controllers/<Module>/`. Only Erp.Identity, Erp.Notification.Worker and Erp.Main are separate processes. Each module keeps its own Domain/Infrastructure/Application/Storage projects, schema and database: the boundary is the project, not the process. Do not create a new host without an explicit reason such as independent scaling or deployment.
- All business modules share the `erp-api` audience; access between modules is separated by scope, not by audience.
- Every host must expose a health endpoint through a `HealthController` and publish OpenAPI, with the Scalar reference available in development.
- Blazor pages are organized one folder per feature under `Pages` (for example `Pages/Backoffice/Companies`, `Pages/Sales/Invoices`), keeping the listing and its create/edit pages together. Never leave pages loose at the root of an area.
- In Erp.Main Blazor pages, PageHeaders must only display the title without description/subtitle. For detail/create/edit pages, use breadcrumbs in the format "<Listagem> / <Ação>" (e.g., "Artigos / Editar artigo", "Artigos / Novo artigo"), where the first level links to the listing and the last is disabled.
- All ERP listing pages in Erp.Main must follow the same grid layout established in `Pages/Master/Products/Products.razor`: a MudDataGrid inside a MudPaper with Class="erp-card", the grid using Class="erp-grid", server-side data via VirtualizeServerData over the OData endpoint (Virtualize=true, ItemSize=36, OverscanCount=12), FixedHeader=true with Height="var(--erp-grid-height)", Dense/Hover/Striped enabled, SortMode.Multiple, FilterMode=DataGridFilterMode.ColumnFilterMenu with ShowFilterIcons=true and ShowColumnOptions=false, Hideable and ShowMenuIcon=true for the columns panel, DragDropColumnReordering and ColumnResizeMode.Column, a debounced search MudTextField in ToolBarContent, a NoRecordsContent empty state, a PagerContent footer showing the total record count plus a refresh button, and a final actions TemplateColumn with StickyRight=true that is not sortable/filterable/hideable. Foreign-key columns should use a FilterTemplate with a searchable multi-select (MudAutocomplete plus MudChipSet) instead of free text.

## UI Design Requirements
- For Identity Razor Pages UI, use a shared layout with `@RenderBody` and a Mud-like visual style.
- Implement a left-side menu with options for Users and Clients, styled with a richer look including icons.
- The Home page should display only authenticated user information.

## Copilot Usage Preferences
- Write all code in English, but provide Copilot explanations and responses in Portuguese. Responder em português nas respostas do Copilot, mantendo o código em inglês.
- Persist recurring project preferences in .github/copilot-instructions.md to avoid repeating requests.