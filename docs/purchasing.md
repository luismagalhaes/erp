# Gestão de compras

Plano do módulo `Erp.Purchasing`: encomendas a fornecedores, receção de mercadoria, registo de faturas
de fornecedor e autofaturação.

> Documento de desenho. As decisões marcadas **[decisão]** condicionam o resto e são caras de mudar
> depois — vale a pena discordar delas agora.

---

## O que tem de ficar resolvido

1. **Encomendar a fornecedores** e saber o que está por receber.
2. **Receber mercadoria** e dar entrada em stock, no armazém certo.
3. **Registar a fatura do fornecedor** sem duplicar a entrada de stock que a receção já fez.
4. Não deixar entrar **a mesma fatura duas vezes** — o IVA seria deduzido a dobrar.
5. **Devolver ao fornecedor**, com o documento de transporte que a lei exige.
6. **Autofaturação**, quando somos nós a emitir a fatura em nome do fornecedor.
7. O **custo de compra** passar a valorizar o stock, em vez do custo fixo da ficha do artigo.

---

## Que documentos de compras são fiscalmente relevantes

Esta é a pergunta que decide metade do desenho, por isso vem primeiro. A resposta curta é **um só**:
a autofaturação. Tudo o resto ou é interno, ou é um documento do fornecedor que nós apenas
registamos.

| Documento | Quem o emite | Certificado por nós? | Onde pesa |
|---|---|---|---|
| Encomenda a fornecedor | Nós | **Não** | Nenhures. É um compromisso interno |
| Receção de mercadoria | Nós | **Não** | Stock. O documento fiscal do transporte é o do fornecedor |
| Fatura de fornecedor | **O fornecedor** | **Não** | IVA dedutível, contabilidade, pagamentos |
| Nota de crédito de fornecedor | **O fornecedor** | **Não** | Regularização de IVA a favor do Estado |
| Guia de devolução ao fornecedor | Nós | **Sim** | Documento de transporte — já existe no Sales (`GD`) |
| **Autofatura** | **Nós, por conta do fornecedor** | **Sim** | Série, assinatura, ATCUD, QR, SAF-T |

### O que "não certificado" quer mesmo dizer

Uma fatura de fornecedor **não é um documento nosso**. Quem a numera, assina e comunica é o
fornecedor. Registá-la no ERP é escriturá-la, não emiti-la. Em concreto, e isto é o oposto de tudo o
que o `Erp.Sales` faz:

- **sem série nossa e sem numeração sequencial** — o número é o dele, tal como vem;
- **sem cadeia de assinatura, sem ATCUD, sem QR** — nada disso nos pertence;
- **é editável e apagável**, ao contrário de tudo o que emitimos. Um erro de escrituração corrige-se
  corrigindo; não há documento retificativo a emitir porque não fomos nós que emitimos nada.

O que ela tem de fiscalmente sério é outra coisa: é a base do **IVA dedutível**, e por isso o registo
tem de guardar o número e a data do fornecedor, o NIF dele, a repartição por taxa, e — quando existir
— o **ATCUD do documento dele**, que é o que permite conferir contra o e-Fatura.

### Duplicação de faturas é o risco fiscal real deste módulo

Lançar a mesma fatura duas vezes deduz o IVA duas vezes. É um erro banal (chega o documento em papel
e outra vez por email) e não dá erro nenhum: a contabilidade fecha, o IVA é que fica errado.

> **[decisão]** O par **(empresa, NIF do fornecedor, número do documento)** é único, com índice
> na base de dados e não só validação no serviço. Um duplicado é recusado, não avisado.

Note-se que o número do fornecedor **não** é único por si só: dois fornecedores diferentes emitem
ambos a sua `FT 2026/1`. É a combinação com o NIF que identifica.

### Autofaturação: o único documento certificado deste módulo

Prevista no artigo 36.º n.º 11 do CIVA. Quando há acordo prévio com o fornecedor e ele aceita cada
documento, somos nós a emitir a fatura pela compra que lhe fizemos. Nesse caso, **é uma fatura
emitida pelo nosso programa**, com tudo o que isso arrasta: série comunicada à AT, numeração
sequencial, assinatura encadeada, ATCUD, código QR, imutabilidade e anulação por registo de estado.
O documento impresso tem de dizer **"Autofaturação"**.

No SAF-T sai em `SalesInvoices` com `SpecialRegimes/SelfBillingIndicator = 1`. O esquema já o suporta
— tanto na estrutura `SpecialRegimes` da fatura como no `SelfBillingIndicator` do `Customer` e do
`Supplier` nos *master files*.

> Como o `Invoice` do SAF-T exige `CustomerID`, o fornecedor entra na tabela `Customer` desse
> ficheiro, com `SelfBillingIndicator = 1`. Este ponto **merece confirmação contra a documentação
> técnica da AT** antes de se escrever o gerador — é o único detalhe deste documento que não está
> fechado só a ler o XSD.

### O nosso SAF-T ainda não leva compras nenhumas

O `SaftXmlWriter` escreve `TaxAccountingBasis = "F"`, faturação. Um ficheiro desse tipo **não leva
`GeneralLedgerEntries` nem a tabela `Supplier`**: só o que foi emitido. Portanto, e ao contrário do
que se poderia esperar, implementar as compras **não muda nada no SAF-T** — a autofatura entra por
ser uma fatura emitida, não por ser uma compra.

As compras só aparecem no SAF-T no dia em que o ficheiro passar a ser de contabilidade (`"C"`), e
isso é trabalho do módulo Accounting, não deste. O que este módulo tem de garantir é que os dados
existem e estão certos para quando esse dia chegar: NIF, taxas, e o `SupplierID` a bater com o código
do fornecedor no Core.

### Onde as compras pesam mesmo

| Obrigação | O que precisa das compras |
|---|---|
| **Declaração Periódica de IVA** | IVA dedutível por taxa e por natureza (existências, imobilizado, outros bens e serviços); regularizações |
| **Autoliquidação** (*reverse charge*) | Aquisições intracomunitárias e o artigo 2.º n.º 1 j) — construção, sucata. A fatura vem sem IVA e somos nós a liquidá-lo e a deduzi-lo |
| **Inventário** | O custo de aquisição é o que valoriza as existências |
| **Contabilidade** | Lançamento do fornecedor e do IVA dedutível |
| **e-Fatura** | Conferir o que registámos contra o que os fornecedores comunicaram. É conferência, não obrigação de submeter |

---

## Decisões de desenho

### [decisão] A autofatura é um documento do `Erp.Purchasing`, não do `Erp.Sales`

A tentação é pô-la no Sales, porque é lá que vive a maquinaria de emissão certificada. Seria enfiar
fornecedores dentro do módulo de vendas para poupar trabalho — e o `SalesDocument` passaria a ter uma
contraparte que às vezes é cliente e às vezes fornecedor, sem que nada no módulo o justifique.

O argumento contra — "duas cadeias de assinatura podem divergir" — **não se sustenta contra o
código**. A cadeia é **por série**, e cada série pertence a exatamente uma família de documentos. O
`GetLastHashAsync(seriesId)` já está implementado **três vezes**, no `SalesDocumentStorage`, no
`StockMovementStorage` e no `PaymentStorage`, cada uma a procurar só na sua tabela. Uma quarta
implementação no Purchasing é o padrão estabelecido, não um risco novo.

O que é mesmo partilhado divide-se em três, e cada parte tem a sua resposta:

| O que | Onde vive hoje | Resposta |
|---|---|---|
| Assinatura, ATCUD, número, QR | `Erp.FiscalPT`, funções puras sem estado | Purchasing usa como o Sales usa. Nada a fazer |
| Cadeia de hash da série | Uma consulta por família, já triplicada | Purchasing acrescenta a sua. Nada a fazer |
| **Registo de séries** | `Erp.Sales.Domain.Series` | **É o único problema a sério.** Parte vai para o FiscalPT, parte não pode. Ver a decisão seguinte |

### [decisão] A *regra* da série vai para o `Erp.FiscalPT`; a *linha* fica num módulo próprio

A pergunta natural é: por que não pôr a série inteira no `Erp.FiscalPT`, que Sales e Purchasing já
referenciam? A resposta obriga a separar duas coisas que a `Series` de hoje mistura.

O `Erp.FiscalPT` não tem EF Core, não tem *storage*, não tem `DbContext` — só QRCoder e os XSD
embebidos. Tudo o que lá vive é função pura ou modelo imutável, e é isso que o torna testável sem
nada montado. A `Series`, tal como está, não cabe:

| Campo / comportamento | Natureza |
|---|---|
| `SeriesStatus`, `CanIssue`, `Communicate`, `TakeNextSequence` | **Lei fiscal.** Cabe no FiscalPT |
| `RowVersion` | Token de concorrência do EF Core. Infraestrutura pura |
| `CompanyId` | Conceito do Core |
| `StockEffect` | Configuração de negócio — se a série dá entrada ou saída. Não é fiscal |
| A leitura com `WITH (UPDLOCK, ROWLOCK)` | **É a razão de a série ser uma linha e não um cálculo** |

E há o argumento que fecha a questão: **mesmo que a classe vivesse no FiscalPT, algum `DbContext`
teria de possuir a tabela e as suas migrations.** Se fosse o `SalesDbContext`, o Purchasing passaria
a depender do *storage* do Sales — exatamente o acoplamento que se está a evitar. Pôr só a classe lá
pouparia criar um projeto e custaria ao FiscalPT a pureza que é o seu valor.

Então o corte é entre a regra e a linha:

- **no `Erp.FiscalPT`**, ao lado do ATCUD e do número de documento, que são a mesma matéria: o estado
  da série e a regra de numeração, como tipos puros;
- **num módulo `Erp.Series`**, a linha: a entidade EF, a leitura bloqueada, a comunicação à AT, o
  ecrã. Sales e Purchasing tiram números de lá.

O ganho de levar a regra para o FiscalPT não é poupar código — são dez linhas. É que a série, o
ATCUD e a assinatura são **um corpo só de regras**, saídas da mesma legislação, e hoje estão em dois
sítios. O ATCUD constrói-se a partir do código de validação da série e da sequência: tê-los
separados é o que obriga a saber que existe essa ligação em vez de a ver.

> **A numeração tem de acontecer na mesma transação que a inserção do documento**, senão um número é
> consumido por um documento que nunca chega a existir. Com a série noutro `DbContext` isso continua
> a funcionar, porque a peça já existe: `SharedDbConnection` e `IAmbientDbTransaction` no
> `Erp.Common`, que é exatamente como o Sales e o Inventory já escrevem juntos.

**O custo, dito com franqueza**: é refactorizar código que funciona. Move-se a tabela `Series` para o
histórico de migrations de outro módulo, mudam-se as referências em três serviços do Sales, e o ecrã
de séries muda de dono. Não é grande, mas é real, e não traz funcionalidade nenhuma no dia em que se
faz.

**Alternativa considerada e rejeitada**: o `Purchasing.Application` referenciar o
`Erp.Sales.Infrastructure` — só as interfaces — e pedir lá os números. Há precedente exato disto no
código, que é como o `Erp.Sales.Application` fala com o `Erp.Inventory.Infrastructure`, e não moveria
nada. Rejeitada porque o custo é conceptual e permanente: o Purchasing passaria a depender do Sales
para uma coisa que não é venda, e o ecrã de séries ficaria em Faturação a numerar autofaturas. É o
mesmo problema de pôr fornecedores no Sales, um degrau abaixo — resolver-se-ia hoje e pagar-se-ia
sempre.

**Fica na fase 5a**, junto com a autofaturação, que é a primeira coisa que precisa dela. As fases 1 a
4 não tocam em séries — nenhum documento de compras é numerado por nós.

### [decisão] A exportação do SAF-T passa a compor-se a partir dos módulos

Hoje o `SaftExportService` vive no `Erp.Sales.Application` e lê diretamente os três *storages* de
vendas. Isso já é uma inclinação errada: **o SAF-T é um ficheiro da empresa, não das vendas** — o
controller já lhe tem de juntar a empresa, que pertence ao Core.

O modelo do ficheiro já está pronto para isto. O `SaftAuditFile` no `Erp.FiscalPT` é uma estrutura
neutra — clientes, artigos, taxas, faturas, movimentos, recibos — e o `SaftXmlWriter` **calcula os
totais de controlo a partir do que recebe**, nunca os aceita de fora. Juntar documentos de duas
proveniências antes de escrever é, por isso, seguro por construção.

O desenho:

- uma interface `ISaftDocumentSource` — "dá-me o que tens para este período, já em modelo SAF-T";
- o `Erp.Sales.Application` implementa-a para faturas, guias e recibos;
- o `Erp.Purchasing.Application` implementa-a para as autofaturas;
- um `SaftExportService` neutro pergunta a todas as fontes registadas, junta e manda escrever.

As autofaturas entram no mesmo bloco `SalesInvoices` das outras faturas — são faturas emitidas —
distinguidas pelo `SelfBillingIndicator`. Os *master files* continuam a ser derivados dos documentos,
agora dos de ambas as fontes, com os clientes, artigos e taxas desduplicados na junção.

Nada disto muda o `Erp.FiscalPT`: ele já não sabe de onde vêm os documentos, e é essa a razão de a
proposta funcionar.

### [decisão] A fatura do fornecedor é registo, não emissão — e portanto é editável

Tudo o que o `Erp.Sales` faz para tornar um documento imutável existe porque **nós** o emitimos. Aqui
não emitimos nada: escrituramos o documento de outra pessoa. Um documento escriturado com o valor
errado corrige-se corrigindo — não há retificativo a emitir, porque o retificativo, a existir, virá
do fornecedor.

A linha não é entre módulos, é entre **o que emitimos e o que escrituramos** — e passa dentro do
`Erp.Purchasing`:

| | Emitido por nós | Escriturado |
|---|---|---|
| **Quais** | Autofaturas, guias de devolução | Faturas e notas de crédito de fornecedor, encomendas, receções |
| **Regime** | *Append-only*: numeração por série, assinatura, ATCUD, anulação por mudança de estado | Editável e apagável |

Vale a pena dizê-lo em voz alta, porque o mesmo módulo vai ter os dois regimes lado a lado e é fácil
copiar o padrão errado de um para o outro.

O que **não** é editável, mesmo do lado escriturado, é o que já passou para o razão de stock: alterar
a quantidade recebida depois de ela ter movimentado tem de gerar movimento, não reescrever o antigo.

### [decisão] O stock entra na receção, e a fatura não volta a movimentar

É a regra do documento integrador do `Erp.Inventory`, ao contrário: onde nas vendas a guia de remessa
dá saída e a fatura que a consome não repete, nas compras a **receção dá entrada** e a fatura que a
consome não repete.

O mecanismo já existe e não precisa de nada novo: o `IStockRecorder` é agnóstico do módulo — recebe
tipo, número e id de documento — e o `DocumentStockLine.OriginatingLineId` é exatamente o campo que
diz "esta linha vem daquela, que já movimentou".

Mas há o caso comum de **a fatura acompanhar a mercadoria**, sem receção separada. Nesse caso é a
fatura que movimenta. Quem decide é o documento, como nas vendas:

| Documento de compra | Efeito no stock |
|---|---|
| Encomenda a fornecedor | Nenhum — é um compromisso, não uma mercadoria |
| Receção de mercadoria | **Entrada** |
| Fatura de fornecedor | **Entrada**, exceto nas linhas que vêm de uma receção |
| Nota de crédito de fornecedor | **Saída**, se for por devolução; nenhum, se for só de valor |
| Devolução ao fornecedor | **Saída** |

> Nas vendas, o efeito no stock é uma propriedade da **série**, porque a série é o que a AT conhece.
> Nas compras não há séries nossas, por isso o efeito é uma propriedade do **tipo de documento**, com
> o mesmo padrão do `DefaultStockEffects`. Uma nota de crédito de fornecedor é o único caso ambíguo
> — se é devolução de mercadoria ou desconto de valor — e isso pergunta-se ao utilizador ao registar.

### [decisão] O custo de compra é o que passa a valorizar o stock

Hoje a valorização das existências vem do `Product.UnitCost`, um custo padrão escrito à mão na ficha.
Está documentado como limitação e é aqui que se resolve: **as compras são o único sítio por onde um
custo real entra no sistema**.

O razão de stock já guarda `UnitCost` por movimento — o campo existe e está por usar a sério. A
receção preenche-o com o custo da linha de compra, e a partir daí o **custo médio ponderado** por
artigo e armazém é uma projeção do razão, calculada da mesma maneira que o saldo de quantidade já é.

> Fica **fora** deste plano decidir entre custo médio ponderado e FIFO. O médio ponderado é o que se
> tira do razão sem estrutura nova; o FIFO exige guardar camadas de custo e consumi-las por ordem, e
> é outro documento. O que este módulo tem de garantir é que o custo entra no razão — sem isso,
> nenhum dos dois é possível.

Quando existir, o custeio substitui o `Product.UnitCost` como fonte da valorização no ficheiro de
inventário. Até lá, o custo da ficha continua a servir, e o ecrã continua a avisar quantos artigos vão
com valor zero.

### [decisão] Conferência a três: encomenda → receção → fatura

A cada passo, o seguinte não pode exceder o anterior:

- não se recebe mais do que se encomendou (com tolerância configurável — é normal receber 1010 de uma
  encomenda de 1000);
- não se fatura mais do que se recebeu.

É o mesmo problema que o "não faturar mais do que a guia moveu", já resolvido no Sales, e resolve-se
da mesma maneira: o já recebido e o já faturado são **derivados** das linhas que referenciam a
origem, e as linhas de origem são **bloqueadas** antes dessa leitura. Sem o bloqueio, duas faturas
registadas ao mesmo tempo contra a mesma receção passam ambas.

### [decisão] O fornecedor é copiado para o documento, como o cliente já é

`SupplierSnapshot` ao lado do `CustomerSnapshot`. Editar a ficha do fornecedor não pode alterar o que
já foi registado — vale aqui pela mesma razão que vale nas vendas, e ainda mais: o NIF que consta da
fatura registada é o NIF que estava na fatura, não o que está hoje na ficha.

---

## Esquema

```
PurchaseOrder                     PurchaseOrderLine
  Id, CompanyId, SupplierId         Id, OrderId, LineNumber
  Number (interna, sem série AT)    ProductCode, Description
  OrderDate, ExpectedDate           Quantity, UnitPrice, UnitOfMeasure
  Status (Open/Partial/Closed)      TaxCode, TaxPercentage
  WarehouseId (destino)
  SupplierSnapshot

GoodsReceipt                      GoodsReceiptLine
  Id, CompanyId, SupplierId         Id, ReceiptId, LineNumber
  Number (interna)                  OrderLineId?      ← o que se está a receber
  ReceiptDate, WarehouseId          ProductCode, Description
  SupplierDocumentNumber            Quantity, UnitCost
  SupplierDocumentDate              ↑ o custo com que entra no razão
  SupplierSnapshot

PurchaseInvoice                   PurchaseInvoiceLine
  Id, CompanyId, SupplierId         Id, InvoiceId, LineNumber
  SupplierDocumentNumber   ← dele   ReceiptLineId?    ← já movimentou stock?
  SupplierDocumentDate     ← dele   OrderLineId?
  SupplierAtcud?           ← dele   ProductCode, Description
  DocumentType (FT/FS/NC/ND)        Quantity, UnitPrice
  ReceivedDate, DueDate             TaxCode, TaxPercentage, TaxAmount
  ReverseCharge (bool)              DeductionNature   ← existências / imobilizado / outros
  NetTotal, TaxPayable, GrossTotal
  SupplierSnapshot                PurchaseInvoiceTaxSummary
                                    TaxCode, TaxPercentage, TaxableBase, TaxAmount
```

E, do lado emitido, a autofatura — que se parece com um `SalesDocument` e não com nada acima:

```
SelfBilledInvoice                 SelfBilledInvoiceLine
  Id, CompanyId, SupplierId         Id, InvoiceId, LineNumber
  SeriesId  ← do Erp.Series         ProductCode, Description
  Number, Atcud                     Quantity, UnitPrice
  IssueDate, SystemEntryDate        TaxCode, TaxPercentage, TaxAmount
  Hash, PreviousHash, HashControl
  QrCodePayload                   SelfBilledInvoiceStatusChange
  Status, AcceptedBySupplierAt      ↑ anulação, como no Sales
  NetTotal, TaxPayable, GrossTotal
  SupplierSnapshot
```

`AcceptedBySupplierAt` porque o artigo 36.º n.º 11 exige que o fornecedor **aceite** cada documento,
não só que tenha havido acordo prévio. Sem isso registado, o documento existe mas o regime não está
cumprido.

Índice único **`(CompanyId, SupplierTaxId, SupplierDocumentNumber)`** em `PurchaseInvoice` — a
defesa contra a dupla dedução, na base de dados e não só no serviço.

`DeductionNature` por linha, não por documento: a mesma fatura traz existências e serviços, e a
declaração de IVA separa-os.

---

## Fases

| Fase | Conteúdo | Estado |
|---|---|---|
| 1 | Encomendas a fornecedores: emissão, estado, e ecrã do que está por receber | Por fazer |
| 2 | Receção de mercadoria, com entrada em stock e conferência contra a encomenda | Por fazer |
| 3 | Registo de faturas de fornecedor, com a regra do documento integrador e o índice anti-duplicação | Por fazer |
| 4 | Devoluções e notas de crédito de fornecedor | Por fazer |
| 5a | Regra da série para o `Erp.FiscalPT`, linha para o `Erp.Series`, e exportação do SAF-T composta por módulo | Por fazer |
| 5b | Autofaturação no `Erp.Purchasing`, com série própria e `SelfBillingIndicator` no SAF-T | Por fazer |
| 6 | Custo médio ponderado a partir do razão, substituindo o custo da ficha na valorização | Por fazer |

A fase 5a é refactorização pura: no fim dela o sistema faz exatamente o mesmo que fazia, e os testes
existentes do SAF-T e das séries são a rede que diz que assim é. Convém fazê-la **antes** de escrever
a autofaturação e não ao mesmo tempo, para que uma falha se saiba logo de que lado veio.

Fora deste plano, e deliberadamente: contas correntes de fornecedores e pagamentos (é outro módulo),
lançamento contabilístico (Accounting), e a conferência contra o e-Fatura (precisa dos *webservices*
da AT, que ainda não estão integrados para nada).

---

## Dívida a pagar pelo caminho

O `Erp.Purchasing` vai usar os fornecedores do Core, e do lado do Core eles estão mal arrumados:
`SupplierStorage` vive dentro de `BrandStorage.cs` e `SupplierService` dentro de `CustomerService.cs`,
contra a convenção de um tipo por ficheiro. Vale a pena separar antes de acrescentar código que os
use, não depois.
