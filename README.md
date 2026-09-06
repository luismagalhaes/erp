# ERP Solution (C# / .NET 10)

## Visão geral
Esta solução está organizada por domínios/módulos e por camadas de responsabilidade.

Padrão usado nos módulos principais:
- **Domain**: modelos e contratos de domínio.
- **Infrastructure**: interfaces/abstrações.
- **Application**: implementações de casos de uso/serviços.
- **Storage**: persistência e acesso a dados (EF Core, DbContexts, migrations).
- **Web/API/UI**: camada de apresentação e composição de DI.

---

## Projetos da solução

### Identity
- **Erp.Identity**: aplicação web de identidade (UI de conta/backoffice, autenticação, integração com IdentityServer).
- **Erp.Identity.Application**: serviços de aplicação do módulo Identity.
- **Erp.Identity.Domain**: modelos/DTOs de domínio do Identity.
- **Erp.Identity.Infrastructure**: contratos/interfaces do Identity.
- **Erp.Identity.Storage**: acesso a dados, DbContexts, seed e migrations do IdentityServer/ASP.NET Identity.
- **Erp.Identity.Common**: constantes e elementos partilhados do módulo Identity.
- **Erp.Identity.Dependencies**: integrações externas do Identity (ex.: chamada ao Notification Service para enfileirar emails).

### Notification
- **Erp.Notification**: aplicação web do módulo Notification (API + UI Blazor Server para histórico).
- **Erp.Notification.Application**: serviços de aplicação do Notification.
- **Erp.Notification.Domain**: modelos de domínio (email notifications, estados, etc.).
- **Erp.Notification.Infrastructure**: interfaces/abstrações do Notification.
- **Erp.Notification.Storage**: persistência e acesso a dados do Notification.
- **Erp.Notification.Worker**: worker para processamento assíncrono de notificações pendentes.

### UI
- **Erp.Main**: frontend principal em Blazor WebAssembly.

### Serviços (APIs)
- **Erp.Core.Api**
- **Erp.Sales.Api**
- **Erp.Inventory.Api**
- **Erp.Purchasing.Api**
- **Erp.Accounting.Api**
- **Erp.Reporting.Api**

### Shared
- **Erp.Shared.Contracts**: contratos partilhados entre módulos.
- **Erp.Shared.Kernel**: utilitários/base comum.
- **Erp.Shared.FiscalPT**: componentes partilhados de fiscalidade PT.

### Testes
- **Erp.Sales.Tests**
- **Erp.FiscalPT.Tests**

---

## Diferença entre o Blazor do Notification e o “Blazor” do Identity

## 1) Notification: Blazor Server
O projeto **Erp.Notification** usa **Blazor Server** para a UI de histórico de emails.

Características:
- Renderização e execução no servidor.
- Estado da UI mantido por circuito SignalR.
- Autenticação OIDC configurada no servidor (cookie + OpenIdConnect middleware).
- Acesso à UI protegido por autorização (policy `NotificationAdmin`, role `Admin`).

Quando usar:
- Backoffice interno.
- Forte controlo server-side.
- Integração direta com serviços/dados no backend.

## 2) Identity: não é Blazor UI neste momento
O projeto **Erp.Identity** é uma aplicação web de identidade com páginas de conta/backoffice (Razor Pages) e IdentityServer.

Características:
- Fluxo de login/logout e gestão de identidade centralizados.
- Emite tokens para os clientes (ex.: `blazor-wasm`, `notification-ui`, service clients).

> Nota: embora ambos sejam ASP.NET Core Web, a UI atual do Identity não está implementada como Blazor Server/WASM.

---

## Main vs Notification (ambos com tecnologia Blazor na solução)

- **Erp.Main (Blazor WebAssembly)**:
  - Executa no browser.
  - Requer configuração explícita de callbacks/redirect no cliente (`OidcConfiguration`).

- **Erp.Notification (Blazor Server)**:
  - Executa no servidor.
  - Fluxo OIDC configurado no `Program.cs` com middleware server-side.

---

## Seed de clientes no Identity
Para garantir que o cliente `notification-ui` existe na configuração do Identity, executar o seed:

```powershell
dotnet run --project .\src\Identity\Erp.Identity\Erp.Identity.csproj -- --seed
```

Isto aplica migrations e repõe dados configurados (clients, scopes, resources, utilizador admin base).
