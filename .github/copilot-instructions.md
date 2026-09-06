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
- Ensure that the Identity and Notification Portal projects are implemented as Blazor WebAssembly applications.
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

## UI Design Requirements
- For Identity Razor Pages UI, use a shared layout with `@RenderBody` and a Mud-like visual style.
- Implement a left-side menu with options for Users and Clients, styled with a richer look including icons.
- The Home page should display only authenticated user information.

## Copilot Usage Preferences
- Write all code in English, but provide Copilot explanations and responses in Portuguese. Responder em português nas respostas do Copilot, mantendo o código em inglês.
- Persist recurring project preferences in .github/copilot-instructions.md to avoid repeating requests.