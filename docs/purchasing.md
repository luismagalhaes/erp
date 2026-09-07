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
| **Autofatura** | **Nós, por conta do fornecedor** | **Sim** | Série, assinatura, ATCUD, QR, e SAF-T próprio do tipo `"S"` |

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

#### A autofaturação é um SAF-T distinto, não uma extensão do de faturação

Este ponto é contraintuitivo e é o que mais condiciona o desenho, por isso vem em destaque.

O `TaxAccountingBasis` tem um valor dedicado — **`"S"` para Autofaturação** — a par de `"C"`
contabilidade, `"F"` faturação e `"E"` faturação emitida por terceiros. Está na anotação do próprio
esquema oficial.

A distinção é **entre ficheiros, não entre blocos**, e é fácil enunciá-la mal:

- o ficheiro de autofaturação **tem** `SalesInvoices` — é lá que as autofaturas vão, como quaisquer
  outras faturas emitidas;
- o que não acontece é essas faturas aparecerem no SAF-T `"F"` de faturação normal, nem no nosso nem
  no do fornecedor. Pertencem ao ficheiro `"S"` e só a ele.

Por outras palavras: a estrutura é a mesma, o ficheiro é que é outro.

Dentro dele, quem vai na tabela `Customer` é o **cliente autofaturador** — quem emite por conta do
fornecedor, portanto **nós**, com `SelfBillingIndicator = 1`. Não é o fornecedor. Faz sentido assim
que se olha do lado certo: o documento titula uma **venda do fornecedor**, e nessa venda o cliente
somos nós.

E o `Header` leva o **NIF do fornecedor**, o autofaturado. O ficheiro é dos documentos dele; nós é
que o geramos, porque fomos nós que os emitimos em seu nome. Daí que se produza **um ficheiro `"S"`
por fornecedor**, e não um só para todos.

### O nosso SAF-T `"F"` continua a não levar compras nenhumas

O `SaftXmlWriter` escreve `TaxAccountingBasis = "F"`, faturação. Um ficheiro desse tipo **não leva
`GeneralLedgerEntries` nem a tabela `Supplier`**: só o que foi emitido. Nem sequer as autofaturas,
que vão no seu ficheiro `"S"`.

Portanto, e ao contrário do que se poderia esperar, **implementar as fases 1 a 4 não muda nada no
SAF-T que hoje exportamos.** O que muda é ganharmos uma exportação nova, ao lado da que existe.

As compras propriamente ditas só aparecem no SAF-T no dia em que houver um ficheiro de contabilidade
(`"C"` ou `"I"`), e isso é trabalho do módulo Accounting, não deste. O que este módulo tem de
garantir é que os dados existem e estão certos para quando esse dia chegar: NIF, taxas, e o
`SupplierID` a bater com o código do fornecedor no Core.

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

#### O que o `Erp.Series` passa a servir, e o que não

O ponto de o extrair é deixar de ser coisa do Sales: o mesmo registo passa a numerar do **lado do
cliente** (faturas, guias, recibos) e do **lado do fornecedor** (autofaturas), com um só ecrã e um só
fluxo de comunicação à AT.

O que **não** entra lá é o contador interno das encomendas e das receções. Um `ENC2026/7` parece uma
série mas não é:

| | Série da AT | Número de encomenda |
|---|---|---|
| Quem o conhece | A AT | Só nós |
| Código de validação | Obrigatório para emitir | Não existe |
| Cadeia de assinatura | Sim | Não |
| Concorrência | `WITH (UPDLOCK, ROWLOCK)` na linha | Índice único |
| Uma falha na sequência | Tem de ser explicada | Custa um número |

Passá-lo pela `Series` exigiria uma série que emite sem código de validação — enfraquecer o
`CanIssue`, que é uma guarda fiscal, para acomodar um documento que não é fiscal. Fica onde está.

Se um dia se quiser um contador partilhado para números internos, é um segundo tipo dentro do
`Erp.Series`, não o mesmo. Hoje não há procura para isso: são dez linhas por módulo.

### [decisão] A exportação do SAF-T passa a compor-se a partir dos módulos

Hoje o `SaftExportService` vive no `Erp.Sales.Application` e lê diretamente os três *storages* de
vendas. Isso já é uma inclinação errada: **o SAF-T é um ficheiro da empresa, não das vendas** — o
controller já lhe tem de juntar a empresa, que pertence ao Core.

O modelo do ficheiro já está pronto para isto. O `SaftAuditFile` no `Erp.FiscalPT` é uma estrutura
neutra — clientes, artigos, taxas, faturas, movimentos, recibos — e o `SaftXmlWriter` **calcula os
totais de controlo a partir do que recebe**, nunca os aceita de fora. Juntar documentos de duas
proveniências antes de escrever é, por isso, seguro por construção.

O desenho:

- uma interface `ISaftDocumentSource` — "dá-me o que tens para este período, já em modelo SAF-T",
  declarando **para que tipo de ficheiro** serve;
- o `Erp.Sales.Application` implementa-a para o ficheiro `"F"`: faturas, guias e recibos;
- o `Erp.Purchasing.Application` implementa-a para o ficheiro `"S"`: as autofaturas, por fornecedor;
- um `SaftExportService` neutro pergunta às fontes do tipo pedido, junta e manda escrever.

**Não são dois módulos a alimentar o mesmo ficheiro — são dois ficheiros de tipos diferentes.** É por
isso que o tipo entra na interface em vez de ficar implícito: o `TaxAccountingBasis` deixa de ser a
constante `"F"` que o `SaftXmlWriter` escreve hoje e passa a vir de quem exporta, o que a estrutura
oficial suporta bem — `"F"` e `"S"` são tipos de ficheiro distintos, não variantes do mesmo.

**O que não muda é a estrutura.** Ambos os ficheiros levam `SalesInvoices`, ambos derivam os seus
*master files* dos próprios documentos, e ambos passam pelo mesmo `SaftAuditFile` e pelo mesmo
`SaftXmlWriter`. A separação está em **que documentos entram em que ficheiro** e no `Header` de cada
um — não em haver duas formas de escrever.

Daí que a interface tenha de dizer três coisas e não uma: que **tipo** de ficheiro serve, de que
**entidade** é o cabeçalho, e que documentos traz. Uma fonte que só devolvesse documentos não
chegaria — o ficheiro `"S"` sai um por fornecedor, com o NIF dele no `Header`, enquanto o `"F"` sai
um só, com o nosso.

Nada disto muda o `Erp.FiscalPT`, tirando o `TaxAccountingBasis` deixar de ser constante: o escritor
já não sabe de onde vêm os documentos, e é essa a razão de a proposta funcionar.

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
| 1 | Encomendas a fornecedores: emissão, estado, e o que está por receber | **Feito** |
| 2 | Receção de mercadoria, com entrada em stock e conferência contra a encomenda | **Feito** |
| 3 | Registo de faturas de fornecedor, com a regra do documento integrador e o índice anti-duplicação | **Feito** |
| 4 | Devoluções e notas de crédito de fornecedor | **Feito** |
| 5a | Regra da série para o `Erp.FiscalPT`, linha para o `Erp.Series`, e exportação do SAF-T composta por módulo | Por fazer |
| 5b | Autofaturação no `Erp.Purchasing`, com série própria e exportação SAF-T `"S"` por fornecedor | Por fazer |
| 6 | Custo médio ponderado a partir do razão, substituindo o custo da ficha na valorização | Por fazer |

A fase 5a é refactorização pura: no fim dela o sistema faz exatamente o mesmo que fazia, e os testes
existentes do SAF-T e das séries são a rede que diz que assim é. Convém fazê-la **antes** de escrever
a autofaturação e não ao mesmo tempo, para que uma falha se saiba logo de que lado veio.

Fora deste plano, e deliberadamente: contas correntes de fornecedores e pagamentos (é outro módulo),
lançamento contabilístico (Accounting), e a conferência contra o e-Fatura (precisa dos *webservices*
da AT, que ainda não estão integrados para nada).

---

### Como ficou a fase 1

O módulo `Erp.Purchasing` segue a divisão em camadas dos outros e tem o seu próprio
`__EFMigrationsHistory_Purchasing`. Não depende do Core: o controller lê o fornecedor e o armazém e
entrega-os, tal como o Sales já recebe o cliente em vez de o ir buscar.

Três coisas decididas ao escrever, que o desenho não tinha fechado:

- **A quantidade recebida é guardada na linha da encomenda**, não derivada como no Sales. Lá, o já
  faturado é derivado das faturas precisamente porque a guia é *append-only* e não pode ser tocada;
  aqui a encomenda é mutável e a leitura direta é mais simples e mais barata.
- **As linhas deixam de poder mudar assim que chega mercadoria.** Alterá-las depois reescreveria em
  silêncio aquilo contra o que a receção foi medida. Até lá, reescrevem-se à vontade.
- **Receber a mais é aceite**, porque os fornecedores fazem-no, mas nunca gera dívida negativa: o
  `PendingQuantity` tem chão em zero. Quem decide se aceita o excesso é a receção, na fase 2.

A distinção entre **fechar** e **anular** é a mesma ideia por outras palavras: mercadoria que chegou
não se desencomenda. Uma encomenda com receções fecha-se, dando o resto por não vindo; uma sem
receções anula-se. O ecrã oferece só a que é possível.

O número (`ENC2026/7`) é nosso e não tem significado fiscal, por isso **não leva bloqueio de linha**
como uma série da AT: uma colisão é apanhada pelo índice único e uma falha custa um número, não uma
explicação à AT.

### Como ficou a fase 2

É aqui que a compra encontra o stock, e o que se reaproveitou foi tudo: o `IStockRecorder` já era
agnóstico do módulo, por isso a receção regista-se nele como qualquer documento — direção `In`, tipo
`REC` — sem nada de novo no Inventory.

A **transação partilhada** entra em serviço pela primeira vez fora do Sales. O `PurchasingUnitOfWork`
abre a transação e publica-a no `IAmbientDbTransaction`; o `StockStorage` junta-se-lhe. Assim a
receção, as entradas no razão e as quantidades recebidas da encomenda **caem juntas ou não caem**.
Sem isso, o armazém e a encomenda ficariam a discordar sobre o que chegou, e ninguém daria por isso
até um teste de stocks.

Duas regras que valem a pena reter:

- **Não se recebe mais do que a encomenda ainda deve.** É a conferência a três, e é o mesmo problema
  do "não faturar mais do que a guia moveu" no Sales — logo, a mesma solução: a encomenda é
  **bloqueada** antes de se ler o que falta. O acumulado conta também dentro do mesmo pedido, para
  que a mesma linha duas vezes não passe por duas vias.
- **Mercadoria sem encomenda é aceite.** Chega, e recusá-la não ajudaria ninguém: a linha fica sem
  origem e movimenta stock na mesma.

Ao contrário da encomenda, **a receção não se edita** — já moveu stock, e alterá-la deixaria o razão
a dizer uma coisa e a receção outra. Um erro anula-se: o stock sai por lançamento contrário e a
encomenda recebe de volta a quantidade, ficando outra vez a dever o que devia.

**O custo entra aqui.** Cada linha leva o seu `UnitCost` para o razão, que é o campo que estava por
usar a sério. É o que torna a fase 6 possível — sem isto, não há de onde tirar um custo médio.

### Como ficou a fase 3

O **índice anti-duplicação** existe e é `(CompanyId, SupplierTaxId, SupplierDocumentNumber)`, único.
Teve de ser escrito à mão na migração: atravessa uma coluna da fatura e outra do *snapshot* do
fornecedor, que é um tipo *owned*, e o construtor de modelos do EF não sabe exprimir isso. Como
ambas são colunas da mesma tabela física, a base de dados garante-o na mesma. O serviço verifica-o
antes, só para dar uma mensagem que se percebe.

A **regra do documento integrador** não precisou de nada novo: as linhas vão todas ao
`IStockRecorder` com o `ReceiptLineId` como origem, e ele salta as que já se moveram. Uma fatura que
segue uma receção não é recusada — simplesmente não tem nada para movimentar. As que não têm receção
atrás, e são de existências, dão entrada aqui: é a fatura que veio com o camião.

Quem decide se uma linha mexe em stock é a **natureza da dedução**, não um campo à parte. Só
`Existências` movimenta; imobilizado e serviços não. É um campo de que a declaração de IVA precisa de
qualquer maneira, e evita inventar outro que dissesse o mesmo.

Uma decisão que refina o que este documento dizia. Escrevi acima que as faturas de fornecedor são
editáveis, e são — **exceto depois de terem dado entrada em stock**. Aí reescrevê-las deixaria o razão
a dizer uma coisa e a fatura outra, tal como na receção. Nesse caso anula-se: o registo deixa de
contar para o IVA dedutível e o stock que trouxe sai. O stock que veio por receção fica, porque essa
receção continua de pé e desfazê-la é decisão dela.

O **já faturado por linha de receção é derivado** das linhas de fatura, nunca guardado na receção — é
isso que evita ter de reescrever a receção. Uma fatura anulada não conta, portanto o que ela tomava
volta a ficar disponível. E a receção é **bloqueada** antes de se ler o que falta, pela mesma razão de
sempre.

### Como ficou a fase 4

São **duas coisas separadas**, e mantê-las separadas é o essencial desta fase:

| | Devolução | Nota de crédito |
|---|---|---|
| O que é | Mercadoria a sair | Dinheiro a voltar |
| Quem faz | Nós | O fornecedor |
| Stock | **Sai**, ao custo a que entrou | **Nenhum** |
| Onde | `SupplierReturn` | `PurchaseInvoice` com tipo `NC` |

A tentação seria a nota de crédito devolver o stock. Não pode: a mercadoria já saiu na devolução, e
se a nota de crédito a movimentasse outra vez, o armazém ficaria com existências que estão
fisicamente no fornecedor. Quem decide é o **tipo de documento** — `MovedStock` é sempre falso numa
`NC`, mesmo com as mesmas linhas que numa fatura movimentariam.

A `SupplierReturn` é o espelho da receção e segue-lhe as regras: só sai o que foi recebido e ainda cá
está, com a receção **bloqueada** antes da leitura; o já devolvido é derivado das linhas de devolução,
nunca guardado na receção; e uma devolução anulada devolve nada, portanto a quantidade fica outra vez
disponível. Não se edita — já moveu stock.

O armazém **não é perguntado**: a mercadoria sai de onde está, e a receção já o diz. Linhas de
armazéns diferentes são recusadas, porque um movimento sai de um sítio só.

Os montantes de uma nota de crédito ficam **positivos no documento**, como aparecem no papel do
fornecedor; o sinal pertence ao tipo e aplica-se ao somar. É o mesmo critério do lado das vendas.

> [!NOTE]
> A devolução regista o **movimento**, não o transporte. Mercadoria que viaja de volta precisa de um
> documento de transporte, e **esse é nosso e é fiscal**: uma guia de devolução (`GD`), emitida pelo
> `Erp.Sales`, que já existe. Os ecrãs dizem-no e ligam para lá.

---

## Dívida a pagar pelo caminho

O `Erp.Purchasing` vai usar os fornecedores do Core, e do lado do Core eles estão mal arrumados:
`SupplierStorage` vive dentro de `BrandStorage.cs` e `SupplierService` dentro de `CustomerService.cs`,
contra a convenção de um tipo por ficheiro. Vale a pena separar antes de acrescentar código que os
use, não depois.
