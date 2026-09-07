# Gestão de stocks e inventário

Plano do módulo `Erp.Inventory`: existências por armazém, movimentação a partir dos documentos,
contagens de inventário e o ficheiro de inventário para a AT.

> Documento de desenho. As decisões marcadas **[decisão]** condicionam o resto e são caras de mudar
> depois — vale a pena discordar delas agora.

---

## O que tem de ficar resolvido

1. Uma empresa tem **vários armazéns** e o stock é gerido ao nível do armazém.
2. Há **séries que dão entrada** de stock e séries que dão **saída**.
3. Nos **documentos integradores** — guia de remessa que origina fatura — se a guia já movimentou
   stock, a fatura **não pode movimentar outra vez**.
4. **Ficheiro de inventário para a AT** (Portaria 126/2019).
5. **Teste de stock por artigo**: verificar se o saldo bate certo com os movimentos.
6. **Inventário parcial ou total**: documento que zera as quantidades, e depois a contagem.

---

## Decisões de desenho

### [decisão] O armazém é dados mestre e vive no `Erp.Core`

O `Warehouse` fica ao lado do `Product`, do `Customer` e do `Fornecedor`: é ficheiro mestre, tem o
seu próprio ecrã de manutenção, e é usado por mais do que um módulo — as guias de movimentação já
referem locais de carga e descarga, e as compras vão precisar do mesmo.

O `Erp.Inventory` guarda `WarehouseId` como `Guid` **sem chave estrangeira física**, exatamente como
já faz com `CompanyId`. É a regra que mantém os módulos separáveis.

### [decisão] O stock é um razão *append-only* com uma projeção de saldos

Duas tabelas, e a distinção entre elas é o que faz o requisito 5 ser possível:

| Tabela | O que é | Escrita |
|---|---|---|
| `StockLedgerEntry` | Cada movimento de stock, com sinal, origem e data | INSERT apenas |
| `StockBalance` | Saldo por empresa + armazém + artigo | INSERT + UPDATE |

O saldo é uma **projeção** do razão, mantida na mesma transação. O **teste de stock** recalcula o
saldo a partir do razão e compara com o guardado: se divergirem, houve escrita fora do caminho
normal, e isso é precisamente o que se quer detetar. Guardar só o saldo tornaria o teste impossível;
guardar só o razão tornaria a leitura de existências cara.

O razão é imutável pela mesma razão que os documentos fiscais o são: um acerto é uma entrada nova,
nunca a alteração de uma anterior.

### [decisão] O efeito no stock é uma propriedade da série

A `Series` ganha um `StockEffect`: `None`, `In` ou `Out`. É onde o utilizador já configura o
comportamento do tipo de documento, e responde ao requisito 2 sem inventar um segundo sítio de
configuração.

Valores por omissão ao criar a série, que o utilizador pode alterar:

| Tipo | Efeito | Porquê |
|---|---|---|
| `GR`, `GT`, `GC` | Saída | A mercadoria sai fisicamente |
| `GD` | Entrada | Devolução, a mercadoria volta |
| `GA` | Nenhum | Ativos próprios, não é venda nem compra |
| `FT`, `FS`, `FR` | Saída | Venda direta, sem guia prévia |
| `NC` | Entrada | Crédito por devolução |
| `ND` | Nenhum | Corrige valor, não quantidade |

### [decisão] O documento integrador não movimenta duas vezes

A linha de fatura já guarda o `OriginatingLineId` da linha de guia que fatura — foi construído para
a faturação de guias. A regra é: **uma linha que vem de um documento que já movimentou stock não
movimenta outra vez**. Não é uma configuração, é uma consequência de haver origem.

O razão guarda a linha de documento que o originou, e há uma restrição de unicidade sobre ela: mesmo
que a regra falhasse, a base recusaria o movimento repetido. É o mesmo princípio de defesa em
profundidade que já usamos na numeração das séries.

### [decisão] Anular um documento devolve o stock, sem apagar nada

O razão é *append-only*, por isso anular não remove o que foi escrito: acrescenta a entrada
contrária, e as duas ficam visíveis. Quem olhar para o histórico vê a mercadoria a sair e a voltar,
que é o que de facto aconteceu.

A entrada de reversão **não leva `SourceLineId`**. Essa coluna é única e significa "esta linha já
movimentou o seu stock", o que continua verdade depois de o documento ser anulado.

Anular duas vezes não devolve o stock duas vezes: as reversões já existentes são emparelhadas com
as entradas originais antes de se escrever seja o que for.

### [decisão] O acerto de inventário é um documento com contagem

Uma contagem (`InventoryCount`) é um documento com um âmbito — total, ou parcial por armazém,
família ou lista de artigos — e uma linha por artigo contado. Ao fechar, escreve no razão a
**diferença** entre o contado e o saldo do momento.

A "zeragem" que o requisito 6 pede não é um modo à parte: é uma contagem com quantidade zero no
âmbito escolhido. Fica assim um único mecanismo, auditável, em vez de dois caminhos que podem
divergir.

---

## Estrutura de dados

```sql
Warehouse                      -- Erp.Core
  Id                uniqueidentifier  PK
  CompanyId         uniqueidentifier
  Code              nvarchar(20)      -- único por empresa
  Name              nvarchar(200)
  Address, City, PostalCode, Country
  IsDefault         bit               -- o que a emissão assume
  IsActive          bit

StockLedgerEntry               -- Erp.Inventory, INSERT apenas
  Id                uniqueidentifier  PK
  CompanyId         uniqueidentifier
  WarehouseId       uniqueidentifier
  ProductCode       nvarchar(60)      -- snapshot, como nos documentos
  ProductDescription nvarchar(200)
  Direction         tinyint           -- In, Out, Adjustment
  Quantity          decimal(19,6)     -- sempre positiva; o sinal vem da direção
  UnitCost          decimal(19,6) NULL
  MovementDate      date
  SystemEntryDateUtc datetime2
  SourceDocumentType nvarchar(4)  NULL
  SourceDocumentNumber nvarchar(60) NULL
  SourceDocumentId  uniqueidentifier NULL
  SourceLineId      uniqueidentifier NULL  -- único: impede movimentar a mesma linha duas vezes
  InventoryCountId  uniqueidentifier NULL
  CreatedByUserId   nvarchar(450) NULL

StockBalance                   -- projeção
  Id                uniqueidentifier  PK
  CompanyId, WarehouseId, ProductCode -- únicos em conjunto
  Quantity          decimal(19,6)
  LastMovementUtc   datetime2
  RowVersion        rowversion

InventoryCount
  Id, CompanyId, WarehouseId NULL   -- nulo = todos os armazéns
  Reference         nvarchar(60)
  Scope             tinyint          -- Total, Warehouse, Family, Products
  Status            tinyint          -- Open, Closed
  CountDate         date
  ClosedAtUtc       datetime2 NULL

InventoryCountLine
  Id, CountId, ProductCode, ProductDescription
  SystemQuantity    decimal(19,6)    -- saldo no momento da abertura
  CountedQuantity   decimal(19,6)
  Difference        decimal(19,6)
```

---

## Fases

| Fase | Conteúdo | Estado |
|---|---|---|
| 1 | Armazéns no Core, razão de stock, saldos, acertos manuais e teste de stock | **Feito** |
| 2 | `StockEffect` nas séries, movimentação na emissão, regra do documento integrador e reversão na anulação | **Feito** |
| 2b | Ecrãs: armazéns, existências com razão e acerto, e teste de stock | **Feito** |
| 3 | Contagens de inventário, parciais e totais, com zeragem | Por fazer |
| 4 | Ficheiro de inventário para a AT (Portaria 126/2019) | **Parcial** — ficheiro gerado e ecrã em `/inventory-file`; falta validar contra o XSD oficial |

### Como ficou resolvida a transação partilhada

O movimento de stock é gravado **na mesma transação** que o documento: um documento emitido sem o
movimento correspondente, ou o contrário, é uma inconsistência que ninguém deteta a tempo.

Como o `SalesDbContext` e o `InventoryDbContext` são contextos distintos sobre a mesma base, isso
exige partilharem a ligação e a transação. A solução tem três peças, todas em `Erp.Common` para que
nenhum módulo tenha de conhecer o outro:

- **`SharedDbConnection`** — uma ligação por pedido, que todos os `DbContext` recebem em vez da
  *connection string*. O primeiro módulo a registá-la ganha; os restantes juntam-se.
- **`IAmbientDbTransaction`** — onde o `SalesUnitOfWork` publica a transação que abriu.
- O `StockStorage` chama `UseTransaction` antes de escrever, entrando na transação em curso.

A consequência a conhecer: um pedido não pode correr consultas em dois contextos ao mesmo tempo,
porque uma ligação não serve dois leitores. Todo o nosso código espera por uma chamada antes de
começar a seguinte, o que é o que torna isto seguro.

O `Erp.Sales.Application` referencia o `Erp.Inventory.Infrastructure` — **só as interfaces**. A
implementação fica atrás do módulo de inventário.

### Como ficou a fase 4, e o que lhe falta

O gerador vive em [`Erp.FiscalPT/Inventory`](../src/Shared/Erp.FiscalPT/Inventory/), ao lado do
SAF-T, e o `InventoryFileService` preenche-o a partir do razão **à data de referência** — somando os
movimentos até esse dia, e não lendo os saldos de hoje, que dariam a posição errada para qualquer
período já fechado.

> [!WARNING]
> **O ficheiro não é validado contra o esquema oficial.** Ao contrário do SAF-T, cujo XSD está
> publicado num endereço público e vive no repositório, o esquema da comunicação de inventários fica
> na área autenticada do Portal das Finanças e não foi possível obtê-lo. A estrutura segue os campos
> que a Portaria enumera — `ProductCategory`, `ProductCode`, `ProductDescription`,
> `ProductNumberCode`, `ClosingStockQuantity`, `UnitOfMeasure`, `ClosingStockValue` — mas o
> *namespace* declarado em `InventoryConstants.Namespace` **está por confirmar**.
>
> Assim que o XSD estiver em `src/Shared/Erp.FiscalPT/Inventory/Schemas/`, aplica-se o mesmo padrão
> do SAF-T: embebido no *assembly*, e cada ficheiro validado antes de ser entregue.

**A valorização vem do custo unitário do artigo**, um campo novo no ficheiro de artigos. É custo
padrão: o custeio a sério — média ponderada ou FIFO, movido por cada compra — é trabalho à parte e
não existe. Enquanto não existir, é isto que valoriza o stock, e o ecrã avisa quantos artigos vão
com valor zero por não terem custo preenchido.
