# ERP

ERP modular em .NET 10, com autenticação centralizada (Duende IdentityServer), interface web em Blazor com MudBlazor e um modelo multi-empresa (cada utilizador pode pertencer a várias empresas com papéis distintos).

**Monólito modular, não microserviços.** Os módulos de negócio — Core, Sales, Notification e os que vierem — mantêm projetos, camadas e schema de base de dados próprios, mas correm **num único host** (`Erp.Api`). Separam-se em processos quando houver uma razão concreta para isso: escala independente, equipa dedicada ou cadência de deploy diferente. Até lá, a fronteira é o projeto, não o processo.

Correm em processo separado apenas os que têm razão para isso:

| Processo | Porquê |
|---|---|
| `Erp.Identity` | É um servidor OIDC — fronteira de segurança real |
| `Erp.Api` | Todos os módulos de negócio |
| `Erp.Notification.Worker` | Processamento assíncrono em segundo plano |
| `Erp.Main` | Interface web |

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
        │                                      │  + API de utilizadores
        │ access token (Bearer)                │
        ▼                                      ▼
Erp.Api  ─── Core · Sales · Notification ──►  SQL Server
   (JWT Bearer, um audience, scopes globais de leitura e escrita)
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
| [Erp.Identity.Common](src/Identity/Erp.Identity.Common/) | Constantes do host de Identity: utilizador administrador semeado, ids e URLs de callback dos clients. |
| [Erp.Identity.Dependencies](src/Identity/Erp.Identity.Dependencies/) | Integrações externas — cliente HTTP para o serviço de notificações. |

### Erp.Api — o host dos módulos de negócio

[Erp.Api](src/Erp.Api/) serve todos os módulos, com os controllers organizados por módulo:

```
Controllers/Core/           Access, Companies, UserCompanies,
                            Products, ProductFamilies, ProductSubfamilies, Brands,
                            Customers, Suppliers
Controllers/Sales/          Invoices, Series
Controllers/Notification/   Notifications
Controllers/HealthController.cs
```

Um único audience (`erp-api`) e **dois scopes globais** (`erp.read` e `erp.write`), aplicados pelas políticas em [ErpPolicies](src/Erp.Api/Authorization/ErpPolicies.cs). Os módulos partilham o host e a base de dados; o que os separa são os projetos de camadas e o seu próprio `DbContext`.

Módulos ainda por implementar: Inventory (obrigação de comunicação de inventários), Purchasing, Accounting e Reporting. Entram como mais uma pasta de controllers e o seu conjunto de camadas.

Cada módulo segue a mesma divisão em camadas:

| Módulo | Domain | Infrastructure | Application | Storage |
|---|---|---|---|---|
| Core | [Erp.Core.Domain](src/Services/Erp.Core.Domain/) | [Erp.Core.Infrastructure](src/Services/Erp.Core.Infrastructure/) | [Erp.Core.Application](src/Services/Erp.Core.Application/) | [Erp.Core.Storage](src/Services/Erp.Core.Storage/) |
| | Empresas, acessos e **dados mestre**: artigos com família, subfamília e marca, clientes e fornecedores | | | |
| Sales | [Erp.Sales.Domain](src/Services/Erp.Sales.Domain/) | [Erp.Sales.Infrastructure](src/Services/Erp.Sales.Infrastructure/) | [Erp.Sales.Application](src/Services/Erp.Sales.Application/) | [Erp.Sales.Storage](src/Services/Erp.Sales.Storage/) |
| Notification | [Erp.Notification.Domain](src/Notification/Erp.Notification.Domain/) | [Erp.Notification.Infrastructure](src/Notification/Erp.Notification.Infrastructure/) | [Erp.Notification.Application](src/Notification/Erp.Notification.Application/) | [Erp.Notification.Storage](src/Notification/Erp.Notification.Storage/) |

### Shared

| Projeto | Descrição |
|---|---|
| [Erp.Common](src/Shared/Erp.Common/) | Constantes que descrevem o contrato do access token — roles, claims, scopes e api resources — partilhadas pela `Erp.Api` e pelo `Erp.Main`, para não dependerem de um projeto do Identity. |
| [Erp.FiscalPT](src/Shared/Erp.FiscalPT/) | Primitivas de fiscalidade portuguesa sem dependências de infraestrutura: string e assinatura RSA dos documentos, ATCUD, número de documento, mensagem do código QR e arredondamento fiscal. |

### Notification

| Projeto | Descrição |
|---|---|
| [Erp.Notification.Domain](src/Notification/Erp.Notification.Domain/) | `EmailNotification`, `EmailNotificationRequest`, estados. |
| [Erp.Notification.Infrastructure](src/Notification/Erp.Notification.Infrastructure/) | Interfaces de serviço, storage e envio. |
| [Erp.Notification.Application](src/Notification/Erp.Notification.Application/) | Fila de emails, histórico e envio SMTP. |
| [Erp.Notification.Storage](src/Notification/Erp.Notification.Storage/) | `NotificationDbContext` e repositório. |
| [Erp.Notification.Worker](src/Notification/Erp.Notification.Worker/) | Worker que drena a fila em intervalos fixos e regista as falhas na própria notificação. |

### Testes

| Projeto | Descrição |
|---|---|
| [Erp.FiscalPT.Tests](tests/Erp.FiscalPT.Tests/) | Assinatura, ATCUD, código QR e arredondamento fiscal |
| [Erp.Sales.Tests](tests/Erp.Sales.Tests/) | Emissão, numeração de séries e anulação |
| [Erp.Core.Tests](tests/Erp.Core.Tests/) | Empresas, acessos, catálogo de artigos, clientes e fornecedores, e o tratamento das claims JWT |
| [Erp.Identity.Tests](tests/Erp.Identity.Tests/) | Invariantes do seed (clients, scopes, resources), serviços e o cliente de email |
| [Erp.Notification.Tests](tests/Erp.Notification.Tests/) | Fila de emails, processamento e histórico |

---

## Portas de desenvolvimento

| Serviço | HTTPS | HTTP |
|---|---|---|
| Erp.Main | https://localhost:7019 | http://localhost:5191 |
| Erp.Identity | https://localhost:7081 | http://localhost:5269 |
| Erp.Api | https://localhost:7072 | http://localhost:5096 |
| Erp.Notification.Worker | — | — |

Estas portas estão referenciadas em configuração (URLs de callback OIDC, CORS, `Services:*` no [Erp.Main/appsettings.json](src/UI/Erp.Main/appsettings.json) e nos clients semeados). Alterar uma porta implica atualizar também esses pontos.

O `Erp.Api` expõe, em desenvolvimento, o documento **OpenAPI** em `/openapi/v1.json` e a referência interativa **Scalar** em `/scalar` (a raiz `/` redireciona para lá), e responde a `GET /health` sem autenticação através de um `HealthController`.

---

## Autenticação e autorização

O `Erp.Identity` é o único emissor de tokens. O `Erp.Main` autentica por **Authorization Code + PKCE** com cookie de sessão (30 dias, sliding) e as APIs validam **JWT Bearer**, cada uma com a sua audience.

**Audiences**: `erp-api` para os módulos de negócio e `identity-api` para a API de utilizadores do Identity. O que separa o acesso entre módulos é o **scope**, não o audience.

**Scopes** (definidos em [Constants.cs](src/Shared/Erp.Common/Constants.cs)):

| Scope | Quem o recebe | Para quê |
|---|---|---|
| `erp.read` | `blazor-wasm` | Ler qualquer módulo da `erp-api` |
| `erp.write` | `blazor-wasm` | Escrever em qualquer módulo da `erp-api` |
| `erp.notification.send` | `identity-service` | Identity → ERP API, para enfileirar email |
| `erp.identity.read` | `blazor-wasm` | Blazor → Identity API, para consultar utilizadores |

Como todos os módulos de negócio correm num só host, não há um par de scopes por módulo: o token diz apenas se a aplicação **lê** ou **escreve**, e o que o chamador alcança dentro da API é depois decidido por role e por pertença à empresa. Repare que `erp.notification.send` e `erp.identity.read` apontam em sentidos opostos e validam audiences diferentes: o primeiro é o Identity a chamar a `erp-api`, o segundo é a UI a chamar a `identity-api`.

**Políticas** ([ErpPolicies.cs](src/Erp.Api/Authorization/ErpPolicies.cs)):

| Política | Exige |
|---|---|
| `Read` | scope `erp.read` ou `erp.write` |
| `Write` | scope `erp.write` |
| `Admin` | scope `erp.write` **e** role `SuperAdmin` |
| `NotificationSend` | scope `erp.notification.send` |

O valor da constante `Admin` (`"erp.admin"`) é apenas o **nome da política**, não um scope: não está semeado nem existe em token nenhum. A administração do tenant depende de quem é o utilizador, não do que a aplicação pode fazer — por isso exige a role e não um scope, o que impede um token de serviço de reconfigurar empresas ou séries.

Os scopes são lidos da claim `scope`, aceitando tanto a forma separada por espaços como claims repetidas.

**Clients semeados**:

| ClientId | Tipo | Finalidade |
|---|---|---|
| `blazor-wasm` | Code + PKCE | Erp.Main |
| `identity-service` | Client Credentials | O próprio Identity a enfileirar emails |

**Roles**: apenas `SuperAdmin` e `User`. O `SuperAdmin` configura o tenant; todos os outros são `User`, e o que podem ver depende da empresa a que pertencem. O menu de Backoffice do `Erp.Main` e o backoffice do Identity só são visíveis a `SuperAdmin`, e os endpoints administrativos exigem a política `Admin`.

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

# 3. Aplicar as migrations dos módulos de negócio
dotnet ef database update --context CoreDbContext `
  --project .\src\Services\Erp.Core.Storage\Erp.Core.Storage.csproj `
  --startup-project .\src\Erp.Api\Erp.Api.csproj

dotnet ef database update --context SalesDbContext `
  --project .\src\Services\Erp.Sales.Storage\Erp.Sales.Storage.csproj `
  --startup-project .\src\Erp.Api\Erp.Api.csproj

dotnet ef database update --context NotificationDbContext `
  --project .\src\Notification\Erp.Notification.Storage\Erp.Notification.Storage.csproj `
  --startup-project .\src\Erp.Api\Erp.Api.csproj

# 4. Arrancar os três processos (em terminais separados)
dotnet run --project .\src\Identity\Erp.Identity\Erp.Identity.csproj --launch-profile https
dotnet run --project .\src\Erp.Api\Erp.Api.csproj --launch-profile https
dotnet run --project .\src\UI\Erp.Main\Erp.Main.csproj --launch-profile https

# 5. Opcional: envio efetivo dos emails em fila
dotnet run --project .\src\Notification\Erp.Notification.Worker\Erp.Notification.Worker.csproj
```

> A recuperação de password do Identity enfileira o email no `Erp.Api`. Sem esse processo a correr, o pedido falha — arranque-o sempre que testar o fluxo de reset.

Abrir https://localhost:7019 — o acesso não autenticado é redirecionado para o login do Identity.

Em Visual Studio existem os perfis de arranque múltiplo **"All"** e **"All + Worker"** ([Erp.slnLaunch.user](Erp.slnLaunch.user)).

> Em `Development` o `Erp.Identity` corre o seed automaticamente no arranque (ver [Program.cs](src/Identity/Erp.Identity/Program.cs)); o `--seed` serve para forçar o mesmo noutros ambientes. O seed **substitui** os clients, scopes e resources configurados em código — alterações feitas pelo backoffice a esses registos são perdidas no arranque seguinte.

---

## Base de dados

| Base de dados | Connection string | Utilizada por |
|---|---|---|
| `ErpPortugal` | `ErpDb` | Todos os módulos de negócio e o worker |
| `ErpPortugal_Identity` | `IdentityDb` | Erp.Identity |

**Uma base de dados para o ERP**, com todas as tabelas em `dbo`. É o que permite que emitir uma fatura, dar saída de stock e gerar o lançamento contabilístico caibam numa transação — sem transações distribuídas nem sagas dentro de um único processo — e o que devolve as chaves estrangeiras entre módulos que a separação anterior impedia.

Cada módulo mantém a **sua própria tabela de histórico de migrations** (`__EFMigrationsHistory_Core`, `_Sales`, `_Notification`), pelo que as migrations continuam independentes.

A base do **Identity** fica separada: é outro processo, com ciclo de vida próprio e os stores do Duende.

> Com tudo em `dbo`, os nomes das tabelas têm de carregar o módulo quando houver risco de colisão. `Products` é o catálogo partilhado do Core; um conceito próprio das compras seria `PurchaseItem` ou equivalente, não outro `Products`.

O Identity usa três contextos (ASP.NET Identity, Configuration Store e Persisted Grant Store) sobre a mesma base de dados, com as migrations no assembly `Erp.Identity.Storage`.

```powershell
# Nova migration no Core (o startup project é sempre o Erp.Api)
dotnet ef migrations add <Nome> --context CoreDbContext `
  --project .\src\Services\Erp.Core.Storage\Erp.Core.Storage.csproj `
  --startup-project .\src\Erp.Api\Erp.Api.csproj

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

185 testes em cinco projetos, sem dependência de base de dados: as camadas Application são testadas com storages substituídos (NSubstitute), a biblioteca fiscal é testada diretamente, e o cliente de email e a obtenção de tokens do Identity com um `HttpMessageHandler` e um `TimeProvider` de teste.

---

## Endpoints do módulo Core

| Método | Rota | Autorização |
|---|---|---|
| `GET` | `/api/companies` | `Read` |
| `GET` | `/api/companies/{id}` | `Read` |
| `POST` | `/api/companies` | `Admin` |
| `PUT` | `/api/companies/{id}` | `Admin` |
| `GET` | `/api/user-companies?companyId=` | `Read` |
| `GET` | `/api/user-companies/{id}` | `Read` |
| `POST` | `/api/user-companies` | `Admin` |
| `PUT` | `/api/user-companies/{id}` | `Admin` |
| `DELETE` | `/api/user-companies/{id}` | `Admin` |
| `GET` | `/api/products?companyId=` · `/api/products/{id}` | `Read` |
| `POST` `PUT` | `/api/products` · `/api/products/{id}` | `Write` |
| `GET` `POST` `PUT` | `/api/product-families` | `Read` / `Write` |
| `GET` `POST` `PUT` | `/api/product-subfamilies?companyId=&familyId=` | `Read` / `Write` |
| `GET` `POST` `PUT` | `/api/brands` | `Read` / `Write` |
| `GET` `POST` `PUT` | `/api/customers` | `Read` / `Write` |
| `GET` `POST` `PUT` | `/api/suppliers` | `Read` / `Write` |
| `GET` | `/api/access/me/companies` | Autenticado |
| `GET` | `/api/access/me/companies/{companyId}/role` | Autenticado |
| `POST` | `/api/access/check-role` | Autenticado |

---

## Endpoints do módulo Sales

Além da faturação, o módulo emite os **documentos de movimentação de mercadorias** — guias de remessa (`GR`), transporte (`GT`), ativos próprios (`GA`), consignação (`GC`) e devolução (`GD`), exportados no SAF-T em `MovementOfGoods`. Seguem exatamente as mesmas regras dos documentos de faturação: numeração sequencial por série, cadeia de assinatura, ATCUD, código QR e imutabilidade. Acrescentam o que o regime de bens em circulação exige: locais de carga e descarga, início do transporte, matrícula do veículo, e o **código que a AT devolve na comunicação prévia** — sem o qual a mercadoria não pode circular.

O **documento impresso** tem página própria para cada família (`/invoices/{id}/print` e as equivalentes das guias e dos recibos), em HTML dimensionado para A4 e com um layout sem menu nem barra. As menções que a lei exige vivem em componentes partilhados em vez de copiadas por página: o cabeçalho com o emitente completo e a designação por extenso, e o rodapé com os 4 caracteres do hash seguidos de *Processado por programa certificado n.º XXXX/AT*, o ATCUD e o código QR — este renderizado pelo `QrCodeImage` do `Erp.FiscalPT`, que usa o renderizador de bytes do QRCoder e por isso não depende de nenhuma biblioteca de desenho. O número do certificado é lido do campo `R` do próprio QR, não da configuração atual: um documento emitido antes de o certificado mudar continua a imprimir o número com que foi emitido.

A **exportação do SAF-T (PT) 1.04_01** vive em [`Erp.FiscalPT/Saft`](src/Shared/Erp.FiscalPT/Saft/), que é a biblioteca fiscal partilhada: recebe um `SaftAuditFile` e escreve o XML, sem saber nada de EF Core nem do módulo de vendas. O `SaftExportService` preenche esse modelo a partir dos documentos do período, e o controller junta-lhe a empresa, que pertence ao Core. Os *master files* (`Customer`, `Product`, `TaxTable`) são derivados dos próprios documentos — cada um traz o snapshot do cliente, dos artigos e das taxas — por isso o ficheiro é coerente consigo mesmo mesmo que as fichas tenham mudado entretanto. Os totais de controlo são calculados pelo escritor, nunca recebidos de fora, e os documentos anulados vão no ficheiro com estado `A` mas fora dos totais. A exportação faz-se em `/saft`, por mês, trimestre, ano ou intervalo livre.

O [esquema oficial da AT](src/Shared/Erp.FiscalPT/Saft/Schemas/SAFTPT1.04_01.xsd) está no repositório e vai embebido no assembly, e **cada exportação é validada contra ele** antes de o ficheiro ser entregue: os erros vão para o log e a contagem viaja no cabeçalho `X-Saft-Validation-Errors`, que a página mostra. Como o esquema publicado é XSD 1.1 e o .NET só implementa 1.0, as 19 regras `xs:assert` (co-ocorrência, como exigir motivo de isenção quando o imposto é zero) não são verificadas — passar aqui é necessário, não suficiente.

Emite também os **recibos** — `RC` (regime de IVA de caixa) e `RG` (restantes), exportados no SAF-T em `Payments`. Um recibo é um documento fiscalmente relevante como qualquer outro: numerado por série, assinado na mesma cadeia e imutável. As suas linhas dizem **que faturas liquida e por que valor**, e o serviço recusa liquidar mais do que a fatura ainda deve, liquidar uma fatura anulada, ou repetir a mesma fatura no mesmo recibo. Os meios de pagamento (`NU`, `CH`, `CD`, `CC`, `TB`, ...) têm de somar exatamente o total do recibo. Fora do regime de IVA de caixa o `TaxPayable` é zero: o IVA já foi apurado na fatura, o recibo só movimenta dinheiro. Anular um recibo não altera o registo original — escreve uma mudança de estado, e as faturas voltam a ficar em dívida.

| Método | Rota | Autorização |
|---|---|---|
| `GET` | `/api/invoices?companyId=` | `Read` |
| `GET` | `/api/invoices/{id}` | `Read` |
| `POST` | `/api/invoices` | `Write` |
| `POST` | `/api/invoices/{id}/void` | `Write` |
| `GET` | `/api/stock-movements?companyId=` | `Read` |
| `GET` | `/api/stock-movements/{id}` | `Read` |
| `POST` | `/api/stock-movements` | `Write` |
| `POST` | `/api/stock-movements/{id}/communicate` | `Write` |
| `POST` | `/api/stock-movements/{id}/void` | `Write` |
| `GET` | `/api/payments?companyId=` | `Read` |
| `GET` | `/api/payments/outstanding-invoices?companyId=&customerTaxId=` | `Read` |
| `GET` | `/api/payments/{id}` | `Read` |
| `POST` | `/api/payments` | `Write` |
| `POST` | `/api/payments/{id}/void` | `Write` |
| `GET` | `/api/saft/summary?companyId=&startDate=&endDate=` | `Read` |
| `GET` | `/api/saft?companyId=&startDate=&endDate=` | `Read` |
| `GET` | `/api/series?companyId=` | `Read` |
| `GET` | `/api/series/{id}` | `Read` |
| `POST` | `/api/series` | `Admin` |
| `POST` | `/api/series/{id}/communicate` | `Admin` |

O acesso é por **scope** do token (políticas em [ErpPolicies.cs](src/Erp.Api/Authorization/ErpPolicies.cs)); a gestão de séries exige adicionalmente a role `SuperAdmin`, através da política `Admin`. Não existe endpoint de alteração nem de remoção de documentos: correções fazem-se por documento retificativo e a anulação escreve um registo de mudança de estado.

---

## API do Identity

| Método | Rota | Autorização |
|---|---|---|
| `GET` | `/api/users` | `erp.identity.read` + Admin/SuperAdmin |
| `GET` | `/api/users/{id}` | `erp.identity.read` + Admin/SuperAdmin |

Os utilizadores vivem no Identity, por isso o backoffice do `Erp.Main` lê-os daqui em vez de manter uma cópia. É uma API só de leitura, servida pelo próprio host do IdentityServer com o audience `identity-api`, num esquema Bearer separado — os esquemas por omissão continuam a ser os cookies do ASP.NET Identity, portanto a UI do Identity não é afetada.

**A associação utilizador ↔ empresa vive no Core** (`/api/user-companies`), porque é lá que as empresas existem. É essa tabela que alimenta o seletor de empresa no cabeçalho: um utilizador sem associações não vê empresa nenhuma.

---

## Endpoints do módulo Notification

| Método | Rota | Autorização |
|---|---|---|
| `POST` | `/api/notifications/email` | `erp.notification.send` |
| `GET` | `/api/notifications` | `Read` |
| `GET` | `/api/notifications/{id}` | `Read` |
| `POST` | `/api/notifications/{id}/requeue` | `Write` |

O enfileiramento é chamado serviço a serviço (o Identity, na recuperação de password) com um token de **client credentials** obtido no próprio Identity pelo client `identity-service`, cujo único scope é `erp.notification.send`. O [ClientCredentialsTokenProvider](src/Identity/Erp.Identity.Dependencies/Services/ClientCredentialsTokenProvider.cs) pede o token e reutiliza-o até perto de expirar; o [ServiceTokenHandler](src/Identity/Erp.Identity.Dependencies/Services/ServiceTokenHandler.cs) anexa-o ao pedido.

O scope de envio é deliberadamente separado de `erp.read` e `erp.write`: o cliente da UI tem os dois últimos, para consultar e reenviar, mas **não** pode enfileirar email — caso contrário qualquer utilizador autenticado poderia mandar mensagens em nome do ERP.

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
| `/stock-movements` | Guias de movimentação de mercadorias |
| `/stock-movements/new` | Emissão de guia, com locais de carga e descarga e início de transporte |
| `/stock-movements/{id}` | Guia emitida, com comunicação à AT, QR e anulação |
| `/payments` | Recibos emitidos |
| `/payments/new` | Emissão de recibo, a partir das faturas em dívida do cliente |
| `/payments/{id}` | Recibo emitido, com faturas liquidadas, meios de pagamento, QR e anulação |
| `/saft` | Exportação do SAF-T (PT), por mês, trimestre, ano ou intervalo livre |
| `/invoices/{id}/print` | Documento impresso, com QR, ATCUD, menções legais e cópias |
| `/stock-movements/{id}/print` | Guia impressa, com locais, transporte e código da AT |
| `/payments/{id}/print` | Recibo impresso, com faturas liquidadas e meios de pagamento |
| `/series` | Séries de faturação |
| `/series/new`, `/series/{id}` | Criar série e registar o código de validação da AT |
| `/products` | Ficheiro de artigos, com filtros por família, marca e texto |
| `/products/new`, `/products/{id}` | Criar e editar artigo |
| `/product-families`, `/product-families/new`, `/product-families/{id}` | Famílias |
| `/product-subfamilies`, `/product-subfamilies/new`, `/product-subfamilies/{id}` | Subfamílias, agrupadas por família |
| `/brands`, `/brands/new`, `/brands/{id}` | Marcas |
| `/customers`, `/customers/new`, `/customers/{id}` | Clientes |
| `/suppliers`, `/suppliers/new`, `/suppliers/{id}` | Fornecedores |
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

- **Dados mestre no Core** — o catálogo de artigos (com família, subfamília e marca), os clientes e os fornecedores vivem no módulo Core, porque são partilhados: Sales fatura-os, Purchasing vai comprá-los e Inventory vai reportá-los. Cada documento emitido guarda a sua própria cópia, pelo que editá-los nunca altera o que já foi faturado.
- **Módulos por implementar** — Inventory, Purchasing, Accounting e Reporting ainda não existem: os hosts vazios foram removidos na fusão e entram como pasta de controllers e camadas próprias quando forem escritos. Core, Sales e Notification estão implementados.
- **Sales em construção** — a emissão certificada funciona (numeração por série, assinatura encadeada, ATCUD, QR), mas falta a comunicação automática de séries à AT, a impressão do documento e o SAF-T. Ver [o plano](docs/certificacao-at-sales.md#estado-da-implementação).
- **SMTP por configurar** — sem `Smtp:Host` e `Smtp:FromEmail`, o worker marca os emails como `Failed` com essa mensagem. É visível no backoffice de notificações e resolve-se com configuração, não com código.
- **Constantes duplicadas** — os scopes, roles e claims vivem em [Erp.Common](src/Shared/Erp.Common/Constants.cs), usado pela `Erp.Api` e pelo `Erp.Main`, mas o Identity mantém a sua cópia em `Erp.Identity.Common`. Os valores coincidem, mas alterar só um dos lados põe o seed e a API em desacordo sem erro de compilação.
- **Cobertura de testes desigual** — a lógica fiscal, a emissão e os serviços do Core estão cobertos; as camadas Storage (EF Core) e as páginas Blazor não têm testes.


### Segurança

Há segredos em código e em configuração versionada que têm de sair antes de qualquer ambiente partilhado:

- Credenciais do utilizador administrador em [Constants.cs](src/Identity/Erp.Identity.Common/Constants/Constants.cs) (`Constants.AdminUser`).
- Segredo do client `identity-service` no mesmo ficheiro, usado como recurso em desenvolvimento quando `ServiceAuthentication:ClientSecret` não está definido.

Mover para *user secrets* em desenvolvimento e para variáveis de ambiente ou um cofre de segredos em produção.

A chave privada de assinatura dos documentos do Sales segue já esta regra: vem de `Fiscal:PrivateKeyPem` (user secrets ou cofre) e nunca do repositório. Em desenvolvimento, se não estiver configurada, é gerada uma chave local em `%LOCALAPPDATA%\Erp\Sales\` — válida para testar, nunca para certificação.
