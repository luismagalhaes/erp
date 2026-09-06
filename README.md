# ERP

ERP modular em .NET 10, construído como um conjunto de microserviços com autenticação centralizada (Duende IdentityServer), interface web em Blazor com MudBlazor e um modelo multi-empresa (cada utilizador pode pertencer a várias empresas com papéis distintos).

---

## Stack

| Área | Tecnologia |
|---|---|
| Runtime | .NET 10 (`net10.0`) |
| UI | Blazor (Interactive Server) + MudBlazor 8 |
| Identidade | Duende IdentityServer 7 + ASP.NET Core Identity |
| APIs | ASP.NET Core Web API (Controllers) + OpenAPI/Scalar |
| Dados | EF Core 10 + SQL Server (LocalDB em desenvolvimento) |
| Logging | Serilog |
| Testes | xUnit + FluentAssertions + NSubstitute |
| Análise estática | SonarAnalyzer.CSharp |

Solução: [Erp.slnx](Erp.slnx) (formato `.slnx`, requer Visual Studio 2022 17.13+ ou `dotnet` SDK recente).

---

## Arquitetura

Cada módulo segue a mesma separação em camadas — repare que a colocação de interfaces e implementações é deliberada e difere da Clean Architecture "de manual":

| Camada | Responsabilidade |
|---|---|
| **Domain** | Modelos, entidades e DTOs |
| **Infrastructure** | Interfaces / abstrações (`IUserService`, `IClientStorage`, …) |
| **Application** | Implementações dos serviços e casos de uso |
| **Storage** | EF Core: `DbContext`, migrations e repositórios |
| **Api / UI** | Apresentação e composição de DI |

Cada camada expõe um `DependencyInjection.cs` com um extension method (`AddCoreStorage`, `AddIdentityApplication`, …) que o host compõe no `Program.cs`.

```
Erp.Main (Blazor Server)  ──OIDC──►  Erp.Identity (Duende IdentityServer)
        │                                      │
        │ access token (Bearer)                │ tokens / claims
        ▼                                      ▼
Erp.*.Api (JWT Bearer)  ──────────────►  SQL Server
```

---

## Mapa da solução

### UI

| Projeto | Descrição |
|---|---|
| [Erp.Main](src/UI/Erp.Main/) | Aplicação principal em Blazor Interactive Server. Autentica por OIDC contra o Identity, guarda tokens em cookie e injeta o access token nas chamadas às APIs via [UserAccessTokenHandler](src/UI/Erp.Main/Services/UserAccessTokenHandler.cs). |

### Identity

| Projeto | Descrição |
|---|---|
| [Erp.Identity](src/Identity/Erp.Identity/) | Host do IdentityServer + UI de conta e backoffice (componentes Blazor). |
| [Erp.Identity.Domain](src/Identity/Erp.Identity.Domain/) | Modelos e DTOs (`ClientEditItem`, `UserListItem`, `ApplicationUser`, …). |
| [Erp.Identity.Infrastructure](src/Identity/Erp.Identity.Infrastructure/) | Interfaces de serviços e de storage. |
| [Erp.Identity.Application](src/Identity/Erp.Identity.Application/) | Implementações: utilizadores, roles, clients, API scopes/resources, identity resources e providers. |
| [Erp.Identity.Storage](src/Identity/Erp.Identity.Storage/) | `ApplicationDbContext`, stores do IdentityServer, migrations e [SeedData](src/Identity/Erp.Identity.Storage/Data/SeedData.cs). |
| [Erp.Identity.Common](src/Identity/Erp.Identity.Common/) | Constantes partilhadas (roles, scopes, api resources, clients). |
| [Erp.Identity.Dependencies](src/Identity/Erp.Identity.Dependencies/) | Integrações externas — cliente HTTP para o serviço de notificações. |

### Serviços (APIs)

| Projeto | Audience JWT | Estado |
|---|---|---|
| [Erp.Core.Api](src/Services/Erp.Core.Api/) | `core-api` | Empresas e acessos de utilizador — implementado |
| [Erp.Sales.Api](src/Services/Erp.Sales.Api/) | `sales-api` | Faturação certificada, séries e artigos — em construção |
| [Erp.Inventory.Api](src/Services/Erp.Inventory.Api/) | `inventory-api` | Esqueleto (`/health` + OpenAPI) |
| [Erp.Purchasing.Api](src/Services/Erp.Purchasing.Api/) | `purchasing-api` | Esqueleto (`/health` + OpenAPI) |
| [Erp.Accounting.Api](src/Services/Erp.Accounting.Api/) | `accounting-api` | Esqueleto (`/health` + OpenAPI) |
| [Erp.Reporting.Api](src/Services/Erp.Reporting.Api/) | `reporting-api` | Esqueleto (`/health` + OpenAPI) |

Core e Sales seguem a mesma divisão em camadas, partilhadas pela respetiva API:

| Módulo | Domain | Infrastructure | Application | Storage |
|---|---|---|---|---|
| Core | [Erp.Core.Domain](src/Services/Erp.Core.Domain/) | [Erp.Core.Infrastructure](src/Services/Erp.Core.Infrastructure/) | [Erp.Core.Application](src/Services/Erp.Core.Application/) | [Erp.Core.Storage](src/Services/Erp.Core.Storage/) |
| Sales | [Erp.Sales.Domain](src/Services/Erp.Sales.Domain/) | [Erp.Sales.Infrastructure](src/Services/Erp.Sales.Infrastructure/) | [Erp.Sales.Application](src/Services/Erp.Sales.Application/) | [Erp.Sales.Storage](src/Services/Erp.Sales.Storage/) |

### Shared

| Projeto | Descrição |
|---|---|
| [Erp.FiscalPT](src/Shared/Erp.FiscalPT/) | Primitivas de fiscalidade portuguesa sem dependências de infraestrutura: string e assinatura RSA dos documentos, ATCUD, número de documento, mensagem do código QR e arredondamento fiscal. |

### Notification

| Projeto | Descrição |
|---|---|
| [Erp.Notification.Api](src/Notification/Erp.Notification.Api/) | API do módulo: fila de emails e histórico de envios. Audience JWT `notification-api`. |
| [Erp.Notification.Domain](src/Notification/Erp.Notification.Domain/) | `EmailNotification`, `EmailNotificationRequest`, estados. |
| [Erp.Notification.Infrastructure](src/Notification/Erp.Notification.Infrastructure/) | Interfaces de serviço, storage e envio. |
| [Erp.Notification.Application](src/Notification/Erp.Notification.Application/) | Fila de emails, histórico e envio SMTP. |
| [Erp.Notification.Storage](src/Notification/Erp.Notification.Storage/) | `NotificationDbContext` e repositório. |
| [Erp.Notification.Worker](src/Notification/Erp.Notification.Worker/) | Worker que drena a fila em intervalos fixos e regista as falhas na própria notificação. |

### Testes

| Projeto | Descrição |
|---|---|
| [Erp.FiscalPT.Tests](tests/Erp.FiscalPT.Tests/) | Assinatura, ATCUD, código QR e arredondamento fiscal |
| [Erp.Sales.Tests](tests/Erp.Sales.Tests/) | Emissão, numeração de séries, anulação e ficheiro de artigos |
| [Erp.Core.Tests](tests/Erp.Core.Tests/) | Empresas e acessos de utilizador |
| [Erp.Identity.Tests](tests/Erp.Identity.Tests/) | Invariantes do seed (clients, scopes, resources), serviços e o cliente de email |
| [Erp.Notification.Tests](tests/Erp.Notification.Tests/) | Fila de emails, processamento e histórico |

---

## Portas de desenvolvimento

| Serviço | HTTPS | HTTP |
|---|---|---|
| Erp.Main | https://localhost:7019 | http://localhost:5191 |
| Erp.Identity | https://localhost:7081 | http://localhost:5269 |
| Erp.Core.Api | https://localhost:7072 | http://localhost:5096 |
| Erp.Sales.Api | https://localhost:7163 | http://localhost:5093 |
| Erp.Inventory.Api | https://localhost:7283 | http://localhost:5077 |
| Erp.Purchasing.Api | https://localhost:7245 | http://localhost:5289 |
| Erp.Accounting.Api | https://localhost:7212 | http://localhost:5194 |
| Erp.Reporting.Api | https://localhost:7156 | http://localhost:5031 |
| Erp.Notification.Api | https://localhost:7117 | http://localhost:5117 |

Estas portas estão referenciadas em configuração (URLs de callback OIDC, CORS, `Services:*` no [Erp.Main/appsettings.json](src/UI/Erp.Main/appsettings.json) e nos clients semeados). Alterar uma porta implica atualizar também esses pontos.

Todos os microserviços expõem, em desenvolvimento, o documento **OpenAPI** em `/openapi/v1.json` e a referência interativa **Scalar** em `/scalar` (a raiz `/` redireciona para lá). Todos respondem também a `GET /health`, sem autenticação, através de um `HealthController`.

---

## Autenticação e autorização

O `Erp.Identity` é o único emissor de tokens. O `Erp.Main` autentica por **Authorization Code + PKCE** com cookie de sessão (30 dias, sliding) e as APIs validam **JWT Bearer**, cada uma com a sua audience.

**Scopes** (definidos em [Constants.cs](src/Identity/Erp.Identity.Common/Constants/Constants.cs)): `erp.core.read/write`, `erp.sales.read/write`, `erp.inventory.read/write`, `erp.purchasing.read/write`, `erp.accounting.read/write`, `erp.reporting.read`, `erp.notification.read/write` e `erp.notification.send` (só para serviços).

**Clients semeados**:

| ClientId | Tipo | Finalidade |
|---|---|---|
| `blazor-wasm` | Code + PKCE | Erp.Main |
| `notification-ui` | Code + PKCE | Portal de notificações (https://localhost:7125) |
| `sales-service` | Client Credentials | Máquina-a-máquina |
| `reporting-service` | Client Credentials | Máquina-a-máquina |
| `identity-service` | Client Credentials | O próprio Identity a enfileirar emails |

**Roles**: `SuperAdmin`, `Admin`, `Manager`, `User`, `Accountant`, `Auditor`. O backoffice do Identity só é visível a `SuperAdmin`; os endpoints administrativos do Core exigem `Admin` ou `SuperAdmin`.

Para que a autorização por role funcione são precisas **três** coisas, e falhando qualquer uma os endpoints com `[Authorize(Roles = ...)]` respondem **403 mesmo a um SuperAdmin**:

1. cada `ApiResource` declara `UserClaims = { "role" }` — sem isso o *access token* não leva roles nenhumas, mesmo que o utilizador as tenha;
2. cada API define `options.MapInboundClaims = false` — por omissão é `true` e o handler **renomeia** `role` para a URI WS-Federation `http://schemas.microsoft.com/ws/2008/06/identity/claims/role`;
3. cada API define `TokenValidationParameters.RoleClaimType = "role"` (e `NameClaimType = "name"`), porque é assim que o Duende emite as claims.

Os pontos 2 e 3 andam aos pares: definir o `RoleClaimType` sem desligar o mapeamento é **pior** do que não fazer nada, porque a claim passa a existir com o nome longo enquanto a autorização procura o curto. [JwtRoleClaimTests](tests/Erp.Core.Tests/JwtRoleClaimTests.cs) reproduz os dois cenários.

Depois de alterar isto é preciso reiniciar o Identity (para o seed atualizar os resources) **e voltar a autenticar**, porque o token em cache foi emitido antes. O endpoint `GET /api/access/me/claims` mostra o que a API vê no token e diz de imediato qual dos três pontos falhou.

O seed cria um utilizador administrador cujas credenciais estão definidas em `Constants.AdminUser` — ver [nota de segurança](#segurança) abaixo.

---

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- SQL Server — LocalDB chega para desenvolvimento (`(localdb)\MSSQLLocalDB`)
- Certificado de desenvolvimento HTTPS: `dotnet dev-certs https --trust`

---

## Como executar

```powershell
# 1. Restaurar e compilar
dotnet restore Erp.slnx
dotnet build Erp.slnx

# 2. Aplicar migrations e semear o Identity (clients, scopes, roles, admin)
dotnet run --project .\src\Identity\Erp.Identity\Erp.Identity.csproj -- --seed

# 3. Arrancar o Identity, a UI e a Core API (em terminais separados)
dotnet run --project .\src\Identity\Erp.Identity\Erp.Identity.csproj --launch-profile https
dotnet run --project .\src\Services\Erp.Core.Api\Erp.Core.Api.csproj --launch-profile https
dotnet run --project .\src\UI\Erp.Main\Erp.Main.csproj --launch-profile https

# 4. Opcional: faturação, notificações e envio de emails
dotnet run --project .\src\Services\Erp.Sales.Api\Erp.Sales.Api.csproj --launch-profile https
dotnet run --project .\src\Notification\Erp.Notification.Api\Erp.Notification.Api.csproj --launch-profile https
dotnet run --project .\src\Notification\Erp.Notification.Worker\Erp.Notification.Worker.csproj
```

> A recuperação de password do Identity enfileira o email no `Erp.Notification.Api`. Sem esse serviço a correr, o pedido falha — arranque-o sempre que testar o fluxo de reset.

Abrir https://localhost:7019 — o acesso não autenticado é redirecionado para o login do Identity.

Em Visual Studio existe o perfil de arranque múltiplo **"Main + Id"** ([Erp.slnLaunch.user](Erp.slnLaunch.user)), que arranca Identity + Main + Core.Api de uma vez.

> Em `Development` o `Erp.Identity` corre o seed automaticamente no arranque (ver [Program.cs](src/Identity/Erp.Identity/Program.cs)); o `--seed` serve para forçar o mesmo noutros ambientes. O seed **substitui** os clients, scopes e resources configurados em código — alterações feitas pelo backoffice a esses registos são perdidas no arranque seguinte.

---

## Base de dados

| Base de dados | Connection string | Utilizada por |
|---|---|---|
| `ErpPortugal_Identity` | `IdentityDb` | Erp.Identity |
| `ErpPortugal_Core` | `CoreDb` | Erp.Core.Api |
| `ErpPortugal_Sales` | `SalesDb` | Erp.Sales.Api |
| `ErpPortugal_Notification` | `NotificationDb` | Erp.Notification.Api e Erp.Notification.Worker |

O Identity usa três contextos (ASP.NET Identity, Configuration Store e Persisted Grant Store) sobre a mesma base de dados, com as migrations no assembly `Erp.Identity.Storage`.

```powershell
# Nova migration no Core
dotnet ef migrations add <Nome> `
  --project .\src\Services\Erp.Core.Storage\Erp.Core.Storage.csproj `
  --startup-project .\src\Services\Erp.Core.Api\Erp.Core.Api.csproj

# Nova migration no Identity (indicar o contexto e a pasta de saída)
dotnet ef migrations add <Nome> --context ApplicationDbContext `
  --output-dir Data\Migrations\Identity `
  --project .\src\Identity\Erp.Identity.Storage\Erp.Identity.Storage.csproj `
  --startup-project .\src\Identity\Erp.Identity\Erp.Identity.csproj
```

Contextos disponíveis no Identity: `ApplicationDbContext`, `ConfigurationDbContext`, `PersistedGrantDbContext`.

---

## Testes

```powershell
dotnet test Erp.slnx
```

152 testes em cinco projetos, sem dependência de base de dados: as camadas Application são testadas com storages substituídos (NSubstitute), a biblioteca fiscal é testada diretamente, e o cliente de email e a obtenção de tokens do Identity com um `HttpMessageHandler` e um `TimeProvider` de teste.

---

## API do Core

| Método | Rota | Autorização |
|---|---|---|
| `GET` | `/api/companies` | Admin, SuperAdmin |
| `GET` | `/api/companies/{id}` | Admin, SuperAdmin |
| `POST` | `/api/companies` | Admin, SuperAdmin |
| `PUT` | `/api/companies/{id}` | Admin, SuperAdmin |
| `GET` | `/api/user-companies?companyId=` | Admin, SuperAdmin |
| `GET` | `/api/user-companies/{id}` | Admin, SuperAdmin |
| `POST` | `/api/user-companies` | Admin, SuperAdmin |
| `PUT` | `/api/user-companies/{id}` | Admin, SuperAdmin |
| `DELETE` | `/api/user-companies/{id}` | Admin, SuperAdmin |
| `GET` | `/api/access/me/companies` | Autenticado |
| `GET` | `/api/access/me/companies/{companyId}/role` | Autenticado |
| `POST` | `/api/access/check-role` | Autenticado |

---

## API do Sales

| Método | Rota | Autorização |
|---|---|---|
| `GET` | `/api/invoices?companyId=` | `erp.sales.read` |
| `GET` | `/api/invoices/{id}` | `erp.sales.read` |
| `POST` | `/api/invoices` | `erp.sales.write` |
| `POST` | `/api/invoices/{id}/void` | `erp.sales.write` |
| `GET` | `/api/series?companyId=` | `erp.sales.read` |
| `GET` | `/api/series/{id}` | `erp.sales.read` |
| `POST` | `/api/series` | Admin, SuperAdmin |
| `POST` | `/api/series/{id}/communicate` | Admin, SuperAdmin |
| `GET` | `/api/products?companyId=` | `erp.sales.read` |
| `GET` | `/api/products/{id}` | `erp.sales.read` |
| `POST` | `/api/products` | `erp.sales.write` |
| `PUT` | `/api/products/{id}` | `erp.sales.write` |

O acesso é por **scope** do token (políticas em [SalesPolicies.cs](src/Services/Erp.Sales.Api/Authorization/SalesPolicies.cs)); a gestão de séries exige adicionalmente role de administrador. Não existe endpoint de alteração nem de remoção de documentos: correções fazem-se por documento retificativo e a anulação escreve um registo de mudança de estado.

---

## API do Identity

| Método | Rota | Autorização |
|---|---|---|
| `GET` | `/api/users` | `erp.identity.read` + Admin/SuperAdmin |
| `GET` | `/api/users/{id}` | `erp.identity.read` + Admin/SuperAdmin |

Os utilizadores vivem no Identity, por isso o backoffice do `Erp.Main` lê-os daqui em vez de manter uma cópia. É uma API só de leitura, servida pelo próprio host do IdentityServer com o audience `identity-api`, num esquema Bearer separado — os esquemas por omissão continuam a ser os cookies do ASP.NET Identity, portanto a UI do Identity não é afetada.

**A associação utilizador ↔ empresa vive no Core** (`/api/user-companies`), porque é lá que as empresas existem. É essa tabela que alimenta o seletor de empresa no cabeçalho: um utilizador sem associações não vê empresa nenhuma.

---

## API do Notification

| Método | Rota | Autorização |
|---|---|---|
| `POST` | `/api/notifications/email` | `erp.notification.send` |
| `GET` | `/api/notifications` | `erp.notification.read` |
| `GET` | `/api/notifications/{id}` | `erp.notification.read` |
| `POST` | `/api/notifications/{id}/requeue` | `erp.notification.write` |

O enfileiramento é chamado serviço a serviço (o Identity, na recuperação de password) com um token de **client credentials** obtido no próprio Identity pelo client `identity-service`, cujo único scope é `erp.notification.send`. O [ClientCredentialsTokenProvider](src/Identity/Erp.Identity.Dependencies/Services/ClientCredentialsTokenProvider.cs) pede o token e reutiliza-o até perto de expirar; o [ServiceTokenHandler](src/Identity/Erp.Identity.Dependencies/Services/ServiceTokenHandler.cs) anexa-o ao pedido.

O scope de envio é deliberadamente separado de `read` e `write`: o cliente da UI tem os dois últimos, para consultar e reenviar, mas **não** pode enfileirar email — caso contrário qualquer utilizador autenticado poderia mandar mensagens em nome do ERP.

O segredo do `identity-service` vem de `ServiceAuthentication:ClientSecret` (user secrets ou cofre). Em desenvolvimento, se não estiver configurado, é usado o valor semeado em `Constants` para a máquina local funcionar sem preparação.

O envio efetivo é feito pelo [Erp.Notification.Worker](src/Notification/Erp.Notification.Worker/), que drena a fila no intervalo definido em `NotificationWorker:PollingIntervalSeconds`. Uma falha de entrega marca a notificação como `Failed` com o erro e incrementa as tentativas, sem parar o ciclo.

---

## Interface (Erp.Main)

A empresa ativa escolhe-se no cabeçalho e é partilhada por todas as páginas ([CompanyState](src/UI/Erp.Main/Services/CompanyState.cs)).

| Rota | Página |
|---|---|
| `/invoices` | Lista de faturas |
| `/invoices/new` | Emissão de fatura |
| `/invoices/{id}` | Documento emitido, com hash, QR e anulação |
| `/series` | Séries de faturação |
| `/series/new`, `/series/{id}` | Criar série e registar o código de validação da AT |
| `/products` | Ficheiro de artigos |
| `/products/new`, `/products/{id}` | Criar e editar artigo |
| `/companies` | Empresas |
| `/companies/new`, `/companies/{id}` | Criar e editar empresa, com os utilizadores com acesso num separador |
| `/notifications` | Backoffice de notificações: histórico de emails por estado |
| `/notifications/{id}` | Email enviado, com corpo, erro e reenvio |

As rotas são sempre em **inglês**, mesmo com a interface em português, e as páginas organizam-se **uma pasta por funcionalidade** em `Pages` (`Backoffice/Companies`, `Sales/Invoices`, …), com a listagem e o respetivo editor juntos.

---

## Documentação

- [Certificação AT do Erp.Sales](docs/certificacao-at-sales.md) — plano de implementação da emissão de documentos de venda certificada em Portugal: cadeia de assinatura, séries e ATCUD, código QR, SAF-T (PT) e o esquema do `SalesDb`.

---

## Convenções de desenvolvimento

As convenções do projeto — organização por camadas, padrões de UI do backoffice, uso de Controllers em vez de Minimal APIs, layout MudBlazor — estão em [.github/copilot-instructions.md](.github/copilot-instructions.md). Ler esse ficheiro antes de contribuir.

Em resumo:

- Código, nomes e comentários em **inglês** — incluindo as rotas das páginas do `Erp.Main`, mesmo com a UI em português.
- Endpoints em **Controllers**, nunca Minimal APIs.
- Todos os microserviços expõem `HealthController` e OpenAPI, com Scalar em desenvolvimento.
- Páginas de listagem (`Index`) só listam; a criação vive numa página `Create` separada.
- Nas listagens do backoffice, ações em botões de ícone com a coluna *Actions* no fim.
- Páginas de edição com várias secções usam tabs.
- Páginas de conta não autenticadas usam o `AuthLayout` (sem header nem menu).

---

## Estado atual e limitações conhecidas

Registo honesto do que ainda não está feito, para evitar surpresas:

- **APIs de negócio por implementar** — Inventory, Purchasing, Accounting e Reporting só expõem `/health` e OpenAPI; não têm camadas Domain/Application/Storage próprias. Core e Sales estão implementados.
- **Sales em construção** — a emissão certificada funciona (numeração por série, assinatura encadeada, ATCUD, QR), mas falta a comunicação automática de séries à AT, a impressão do documento e o SAF-T. Ver [o plano](docs/certificacao-at-sales.md#estado-da-implementação).
- **SMTP por configurar** — sem `Smtp:Host` e `Smtp:FromEmail`, o worker marca os emails como `Failed` com essa mensagem. É visível no backoffice de notificações e resolve-se com configuração, não com código.
- **Portal de notificações inexistente** — o client `notification-ui` (https://localhost:7125) está semeado mas não há projeto correspondente. O histórico de emails vive agora no backoffice do `Erp.Main`, pelo que esse client pode deixar de fazer sentido.
- **Cobertura de testes desigual** — a lógica fiscal, a emissão e os serviços do Core estão cobertos; as camadas Storage (EF Core) e as páginas Blazor não têm testes.
- **`csproj` duplicados** — `ErpPortugal.Identity.Application.csproj` e `ErpPortugal.Identity.Storage.csproj` continuam nas pastas do Identity, fora da solução, resto de um rename anterior. Devem ser removidos.
- **Sem CI** — não há pipelines em `.github/workflows`.

### Segurança

Há segredos em código e em configuração versionada que têm de sair antes de qualquer ambiente partilhado:

- Credenciais do utilizador administrador em [Constants.cs](src/Identity/Erp.Identity.Common/Constants/Constants.cs) (`Constants.AdminUser`).
- Segredos dos clients máquina-a-máquina no mesmo ficheiro (`sales-service`, `reporting-service`).

Mover para *user secrets* em desenvolvimento e para variáveis de ambiente ou um cofre de segredos em produção.

A chave privada de assinatura dos documentos do Sales segue já esta regra: vem de `Fiscal:PrivateKeyPem` (user secrets ou cofre) e nunca do repositório. Em desenvolvimento, se não estiver configurada, é gerada uma chave local em `%LOCALAPPDATA%\Erp\Sales\` — válida para testar, nunca para certificação.
