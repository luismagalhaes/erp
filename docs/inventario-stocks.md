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
| 1 | Armazéns no Core, razão de stock, saldos, acertos manuais e teste de stock | Em curso |
| 2 | `StockEffect` nas séries, movimentação na emissão e regra do documento integrador | Por fazer |
| 3 | Contagens de inventário, parciais e totais, com zeragem | Por fazer |
| 4 | Ficheiro de inventário para a AT (Portaria 126/2019) | Por fazer |

### O que a fase 2 tem de resolver com cuidado

O movimento de stock tem de ser gravado **na mesma transação** que o documento: um documento emitido
sem o movimento correspondente, ou o contrário, é uma inconsistência que ninguém deteta a tempo.

Como o `SalesDbContext` e o `InventoryDbContext` são contextos distintos sobre a mesma base, isso
exige partilharem a ligação e a transação. É a única peça de canalização não trivial deste plano, e
está isolada na fase 2 de propósito.

### O que a fase 4 precisa

O ficheiro de inventário da Portaria 126/2019 tem esquema próprio, distinto do SAF-T. Segue-se o
mesmo padrão do SAF-T: o XSD oficial fica no repositório, embebido no *assembly*, e cada ficheiro
gerado é validado contra ele antes de ser entregue.
