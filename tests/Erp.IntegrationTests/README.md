# Testes de integração

Correm contra um **SQL Server a sério**. Existem para responder às duas perguntas que os testes com
storages substituídos não conseguem responder:

- **O modelo concorda com o esquema?** Uma coluna que o modelo chama de uma maneira e a migração de
  outra compila, migra, e só falha na primeira consulta — com um erro que parece uma migração
  partida e não é.
- **Os bloqueios de linha serializam mesmo alguma coisa?** A numeração das séries e os saldos de
  stock assentam inteiramente em `WITH (UPDLOCK, ROWLOCK)`. Um substituto devolve a mesma linha aos
  dois chamadores e ambos passam, exista o bloqueio ou não.

## Preparação, uma vez

```
sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE [ErpIntegrationTests]"
```

Os testes **migram-na sozinhos** e **nunca a apagam** — poder abri-la depois de uma falha vale mais
do que deixar o servidor arrumado. É descartável: apague-a e volte a criá-la quando quiser.

Para apontar para outro servidor, defina `ERP_TEST_SQL` com a *connection string* completa.

## Correr

```
dotnet test tests/Erp.IntegrationTests/Erp.IntegrationTests.csproj
```

Ou só estes, a partir da solução:

```
dotnet test Erp.slnx --filter "Category=Integration"
```

## Isolamento

Cada teste cria **a sua própria empresa**. Quase tudo neste sistema tem âmbito de empresa, o que
torna esse isolamento barato e verdadeiro — e evita esvaziar tabelas entre testes, que seria lento e
frágil. Os dados acumulam-se entre execuções; é uma base de dados de testes, e isso não faz mal.

## No CI

**Não correm.** O *runner* não tem SQL Server, por isso o workflow exclui-os com
`--filter "Category!=Integration"`.

> É uma dívida assumida, não um esquecimento: um teste que só corre na máquina de alguém acaba por
> apodrecer. Fica em aberto pôr um SQL Server no CI — um serviço em contentor — para estes passarem
> a correr em cada *push*.
