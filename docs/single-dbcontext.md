# Um `DbContext` para os módulos de negócio

Plano para juntar os cinco contextos de EF Core num só, e deitar fora a maquinaria que existe apenas
para os manter coerentes.

> Documento de desenho. As decisões marcadas **[decisão]** condicionam o resto e são caras de mudar
> depois — vale a pena discordar delas agora.

---

## De onde vem isto

A pergunta apareceu a meio da extração do registo de séries (fase 5a do
[plano das compras](purchasing.md)). Ao tirar a `Series` do `SalesDbContext`, as **chaves
estrangeiras** de `SalesDocument`, `StockMovement` e `Payment` para ela deixavam de poder existir
como relação: passariam a `Guid` soltos, como já acontece com o `CompanyId`.

Isso contraria uma coisa que o [README](../README.md) celebra explicitamente sobre a fusão para uma
base de dados única — *"o que devolve as chaves estrangeiras entre módulos que a separação anterior
impedia"*. E levanta a pergunta certa: se a base de dados já é uma só, **porquê cinco contextos?**

---

## O que os cinco contextos custavam

> Este capítulo descreve o estado **anterior**, que é o que justifica a mudança. Nada disto existe
> já: as peças abaixo foram todas apagadas nas fases 3 a 5.

Todos os módulos de negócio escrevem na mesma base, tudo em `dbo`. Os contextos separados são uma
escolha de código, não uma consequência da infraestrutura — e essa escolha paga-se:

| O que existe | Porque existe |
|---|---|
| `SharedDbConnection` | Dar a mesma ligação a contextos diferentes |
| `IAmbientDbTransaction` + `AmbientDbTransaction` | Publicar a transação de um contexto para os outros a apanharem |
| `SalesUnitOfWork`, `InventoryUnitOfWork`, `PurchasingUnitOfWork` (+ 3 interfaces) | Cada um com a dança `JoinedTransaction` / transação própria, copiada de módulo para módulo |
| `QualifiedTableName` × 3 | Uma cópia por projeto de *storage* |
| 5 tabelas `__EFMigrationsHistory_*` | 20 migrations em cinco filas que têm de ser aplicadas na ordem certa |

E uma armadilha documentada: **um pedido não pode consultar dois contextos ao mesmo tempo**, porque
uma ligação não serve dois leitores. Está escrito no README e no `SharedDbConnection`, e não há nada
que o impeça a não ser lembrar-se.

### E está meio aplicado

Só o **Sales**, o **Inventory** e o **Purchasing** partilham a ligação. O **Core** e o
**Notification** registam o contexto com a *connection string* diretamente — ver
[`AddCoreStorage`](../src/Modules/Erp.Core/Storage/DependencyInjection.cs).

Ou seja: hoje **não é possível emitir um documento e alterar um dado mestre do Core na mesma
transação.** Ninguém deu por isso porque nenhum caso de uso precisou — mas é uma limitação que
ninguém decidiu, apenas aconteceu.

---

## O que tem de ficar resolvido

1. **Uma transação** para qualquer escrita de um pedido, venha de que módulo vier.
2. **Chaves estrangeiras entre módulos**, já que a base de dados é uma só.
3. O contexto **não pode conhecer os módulos** — a dependência tem de continuar a apontar dos
   módulos para o partilhado, ou acrescentar um módulo passa a implicar editar código partilhado.
4. Um caminho de **migrations** que não perca dados.

---

## Decisões de desenho

### [decisão] Um `ErpDbContext` para os módulos de negócio; o Identity fica de fora

O `Erp.Identity` é outro processo, com outra base de dados e os *stores* do Duende. Não entra nesta
conversa e continua com os seus três contextos.

Dentro do `Erp.Api`, os cinco passam a um: `ErpDbContext`, com um só
`__EFMigrationsHistory`.

### [decisão] O contexto não conhece as entidades — cada módulo contribui o seu mapeamento

É o ponto que decide se isto é uma boa ideia ou o princípio de uma bola de lama.

Se o `ErpDbContext` tivesse `DbSet<SalesDocument>`, `DbSet<StockBalance>` e companhia, o projeto que
o contém teria de referenciar o Domain de **todos** os módulos, e a dependência inverter-se-ia:
o partilhado a conhecer os módulos, em vez do contrário. Acrescentar um módulo passaria a implicar
editar código partilhado.

Em vez disso:

- o `ErpDbContext` vive num projeto `Erp.Storage` que **não referencia Domain nenhum**;
- cada módulo declara as suas tabelas numa `IModuleModelConfiguration`, no seu próprio projeto, ao
  lado das entidades que mapeia;
- o `OnModelCreating` percorre as configurações que lhe foram injetadas.

```csharp
// Erp.Storage — não conhece nenhuma entidade
public sealed class ErpDbContext(
    DbContextOptions<ErpDbContext> options,
    IEnumerable<IModuleModelConfiguration> modules) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var module in modules)
            module.Configure(modelBuilder);
    }
}

// Erp.Sales — o módulo diz como se mapeia a si próprio
public sealed class SalesModelConfiguration : IModuleModelConfiguration { … }
```

> **Uma configuração por módulo, não por entidade.** O plano inicial dizia `IEntityTypeConfiguration<T>`
> do próprio EF, que teria dado cerca de **35 ficheiros novos** — um por entidade. Optou-se por uma
> configuração por módulo porque permite mover o `OnModelCreating` existente **tal e qual**, sem
> reescrever nada: cinco ficheiros em vez de trinta e cinco, e a mesma garantia de que o contexto não
> conhece entidades. Um módulo que queira dividir-se por entidade continua a poder fazê-lo, chamando
> `modelBuilder.ApplyConfiguration(...)` de dentro da sua.

Os *storages* deixam de usar `dbContext.Series` e passam a `dbContext.Set<Series>()`. É a única
perda de conforto, e é pequena.

> **Consequência a aceitar**: o compilador deixa de garantir que o `Erp.Sales.Storage` não lê tabelas
> do Inventory — `Set<StockBalance>()` passa a compilar em qualquer módulo. Passa a ser convenção, e
> a fronteira real continua a ser a interface de *storage* que cada módulo expõe. É o preço, e é
> menor do que o que se paga hoje.

### [decisão] Os *storages* ficam por módulo; o que desaparece são os *unit of work*

Nada muda na arquitetura de camadas: continua a haver `ISalesDocumentStorage`, `IStockStorage`, e
cada módulo continua a expor o seu `DependencyInjection.cs`. O que sai são as três peças que só
existiam para coser contextos:

- `SharedDbConnection`, `IAmbientDbTransaction`, `AmbientDbTransaction` — apagados;
- `ISalesUnitOfWork`, `IInventoryUnitOfWork`, `IPurchasingUnitOfWork` e as implementações — apagados;
- duas das três cópias de `QualifiedTableName`.

Com um contexto, um `SaveChangesAsync` escreve tudo o que o pedido acumulou, atomicamente, sem
transação explícita.

### [decisão] As transações explícitas ficam — onde há bloqueios

Isto é a parte que é tentador simplificar a mais, e não se pode.

O `WITH (UPDLOCK, ROWLOCK)` só segura a linha **até ao fim da transação**. Sem transação explícita,
o EF envolve cada `SaveChanges` na sua, e o bloqueio da leitura é largado assim que a instrução
acaba — antes de a escrita acontecer. Duas emissões concorrentes voltariam a poder tirar o mesmo
número de série.

Portanto, onde hoje há `BeginTransactionAsync`, continua a haver. O que muda é ser
`dbContext.Database.BeginTransactionAsync()` — uma transação simples, sem ambiente, sem publicação,
sem ninguém para se juntar a ela.

Os sítios que a exigem, todos por causa de um `UPDLOCK`:

| Onde | O que protege |
|---|---|
| Emissão de documentos (Sales) | Número de série e o *hash* anterior da cadeia |
| Notas de crédito (Sales) | O já creditado de uma fatura |
| Faturação a partir de guias (Sales) | O já faturado de uma guia |
| Fecho de contagem (Inventory) | O saldo no momento do fecho |
| Receção e devolução (Purchasing) | O por receber e o por devolver de uma encomenda/receção |
| Registo de fatura (Purchasing) | O por faturar de uma receção |

### [decisão] E então a `Series` volta a ser uma tabela como as outras

Resolvido o contexto, o problema que deu origem a isto tudo desaparece: a `Series` sai do
`Erp.Sales` para um módulo próprio **sem perder chave estrangeira nenhuma**, porque as relações
passam a ser dentro do mesmo modelo.

A fase 5a do plano das compras fica à espera desta, e depois faz-se como estava desenhado: a regra
em [`SeriesState`](../src/Shared/Erp.FiscalPT/Documents/SeriesState.cs) — que **já está escrita e
testada** — e a linha num `Erp.SeriesRegistry`.

---

## O caminho das migrations

A parte chata, e a única com risco de perder dados. São **20 migrations em cinco históricos**: Core
6, Sales 7, Purchasing 4, Inventory 2, Notification 1.

O esquema físico não muda: as tabelas são as mesmas, nas mesmas colunas. O que muda é quem as
declara. Por isso não há um *diff* a aplicar — há um histórico a substituir.

**Caminho recomendado, com dados (funciona em qualquer ambiente):**

1. Gerar uma migration `InitialErp` a partir do modelo já unificado. Ela descreve o esquema inteiro.
2. **Não a executar.** Inserir a sua linha em `__EFMigrationsHistory` à mão, marcando-a como
   aplicada — o esquema já lá está.
3. Apagar as cinco tabelas `__EFMigrationsHistory_*`.
4. Confirmar com `dotnet ef migrations has-pending-model-changes` que o modelo e a base concordam.

**Atalho em desenvolvimento**: apagar a base e correr a `InitialErp` a sério, seguida do *seed*. É o
que provavelmente se vai fazer aqui, mas o caminho de cima fica escrito para o dia em que houver
dados a sério.

> As 20 migrations antigas ficam no repositório como história, mas deixam de ser aplicáveis. Vale a
> pena movê-las para uma pasta `Legacy` do respetivo módulo, ou apagá-las de vez — o que não vale é
> deixá-las onde estão a fingir que ainda contam.

---

## Fases

| Fase | Conteúdo | Estado |
|---|---|---|
| 0 | Colapsar as camadas de cada módulo num projeto só, mantendo os módulos | **Feito** |
| 1 | `Erp.Storage` com o `ErpDbContext` e a descoberta de configurações | **Feito** |
| 2 | Mover o mapeamento de cada módulo para uma configuração própria, um módulo de cada vez | **Feito** |
| 3 | Trocar os *storages* para o `ErpDbContext`; apagar os *unit of work* e a transação ambiente | **Feito** |
| 4 | Transações explícitas onde há bloqueios, agora simples | **Feito** |
| 5 | Consolidar as migrations num histórico só | **Feito** |
| 6 | Retomar a 5a das compras: `Series` para módulo próprio, agora com FKs | **Feito** |

A fase 2 é a única que se pode fazer módulo a módulo com tudo a funcionar pelo meio: um
`IEntityTypeConfiguration` aplicado ao contexto antigo dá exatamente o mesmo modelo. As fases 3 a 5
são um corte — entre elas o sistema não compila, e convém fazê-las de seguida.

### Como ficou a fase 6

A `Series` saiu do `Erp.Sales` para o `Erp.SeriesRegistry` **sem o modelo mudar uma vírgula** — o
`has-pending-model-changes` confirma-o, e as três chaves estrangeiras que tinham travado tudo
(`SalesDocument`, `StockMovement` e `Payment` para `Series`) continuam lá. Era exatamente isto que a
consolidação do contexto veio permitir.

A entidade delega agora na regra pura: o `Series` guarda os campos e chama o
[`SeriesState`](../src/Shared/Erp.FiscalPT/Documents/SeriesState.cs) para decidir se pode emitir,
como o número avança e como o estado se move. A lei ficou ao lado do ATCUD que dela se constrói; a
linha ficou onde uma linha tem de estar, para poder ser bloqueada.

> **O módulo chama-se `Erp.SeriesRegistry`, não `Erp.Series`.** Um namespace `Erp.Series` colide com o
> tipo `Series`: dentro de `Erp.Sales.Domain` o compilador encontra o namespace primeiro e falha com
> *"'Series' is a namespace but is used like a type"*. Um alias não resolve — a pesquisa de nomes
> chega ao namespace antes de chegar ao alias. O nome mais longo é o preço de o tipo principal e o
> módulo se chamarem o mesmo.

### Como ficaram as fases 3 a 5

Apagados: `SharedDbConnection`, `IAmbientDbTransaction`, `AmbientDbTransaction`, os três
`*UnitOfWork` com as suas interfaces, duas das três cópias do `QualifiedTableName`, os cinco
`DbContext` de módulo e as 20 migrations antigas. No lugar ficaram um `IErpUnitOfWork` no
`Erp.Common` e uma implementação no `Erp.Storage`.

O `EnsureEnlisted` do `StockStorage` desapareceu sem substituto, e essa é a melhor medida do que se
ganhou: **não há transação para se juntar**, porque quem a abriu abriu-a neste mesmo contexto.

A fase 4 acabou por não dar trabalho nenhum. As transações explícitas já existiam nos seis sítios
certos e continuam lá — só deixaram de ser `IPurchasingTransaction` e companhia para serem
`IErpTransaction`. O `WITH (UPDLOCK, ROWLOCK)` continua a precisar delas, e ninguém foi tentado a
tirá-las.

**As migrations foram adotadas, não reaplicadas.** As 35 tabelas já existiam; a `InitialErp` apenas
volta a declará-las. Por isso foi registada como aplicada **sem correr**, e os cinco históricos
antigos apagados — o caminho que preserva os dados. No fim,
`has-pending-model-changes` confirma que o modelo e a base concordam.

> **Uma coisa que quase se perdeu.** O índice único que impede lançar a mesma fatura de fornecedor
> duas vezes estava escrito **à mão** na migration antiga, porque o construtor de modelos não o sabe
> exprimir. Gerar a migration a partir do modelo deixou-o de fora, em silêncio. Foi reposto à mão na
> nova. É o risco de tudo o que vive só na migration e não no modelo: convém procurá-lo sempre que
> se regenerar.

### Como se soube que as fases 1 e 2 não mudaram nada

Os testes não constroem o modelo do EF, por isso passarem não diz muito aqui. A verificação que
conta foi outra: `dotnet ef migrations has-pending-model-changes` nos cinco contextos, todos com
*"No changes have been made to the model since the last migration"*. Se a extração tivesse alterado
uma coluna, um índice ou uma relação, aparecia ali.

Durante a transição cada contexto antigo delega na configuração do seu módulo, em vez de a duplicar.
Assim os dois modelos não podem divergir enquanto a mudança decorre.

### O que a fase 0 deu

Vinte projetos passaram a cinco. As camadas ficaram em pastas dentro de cada módulo, e **nenhum
`using` mudou**: os *namespaces* eram já `Erp.X.Domain`, `Erp.X.Application` e companhia, e continuam
a sê-lo — só deixaram de ser *assemblies* separados. Foi isso que tornou a mudança mecânica em vez de
arriscada.

O que se perdeu, e vale a pena saber: o `Erp.Sales` referencia agora o `Erp.Inventory` **inteiro**, e
já não apenas o seu `Infrastructure`. O compilador deixou de garantir que um módulo só toca no
contrato do outro. O `IStockRecorder` continua a ser a forma certa de o fazer — passou a ser
convenção em vez de fronteira.

**A rede de segurança são os 658 testes existentes.** No fim disto o sistema faz exatamente o que
fazia; se algum teste mudar de resultado, mudou alguma coisa que não devia ter mudado.

---

## O que fica de fora, e porquê

- **O Identity.** Outro processo, outra base, outros *stores*.
- **Separar bases por módulo.** É o caminho oposto e não está em cima da mesa: a base única é o que
  permite a transação e as chaves estrangeiras.
- **Repositório genérico.** A tentação, com um contexto só, é escrever um `IRepository<T>` e acabar
  com os *storages* por módulo. Não: são eles que continuam a ser a fronteira do módulo, agora que
  o modelo já não a garante.
