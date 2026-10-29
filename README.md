# Cartões API

Microsserviço de **emissão de cartões de crédito** do fluxo de concessão de crédito. Consome o evento `PropostaGerada` e, quando a proposta é aprovada, emite um ou dois cartões conforme a faixa de score.

| | |
|---|---|
| **Swagger (produção)** | https://parana-cartoes-api-production-48cf.up.railway.app/swagger |
| **Front da solução** | https://parana-credito-web.vercel.app |
| **Stack** | .NET 10 · ASP.NET Core · MongoDB · MassTransit 8 + RabbitMQ · xUnit |

## Sumário

- [A solução completa](#a-solução-completa)
- [Regra de emissão](#regra-de-emissão)
- [Fluxo de mensagens](#fluxo-de-mensagens)
  - [Evento consumido: PropostaGerada](#evento-consumido-propostagerada)
- [Modelagem no MongoDB](#modelagem-no-mongodb)
- [Resiliência](#resiliência)
- [Endpoints](#endpoints)
- [Arquitetura](#arquitetura)
- [Decisões técnicas](#decisões-técnicas)
- [Como executar localmente](#como-executar-localmente)
  - [Variáveis de ambiente](#variáveis-de-ambiente)
- [Testes](#testes)
- [Deploy](#deploy)
- [Próximos passos](#próximos-passos)

## A solução completa

O fluxo é dividido em três microsserviços independentes, um por repositório, que se comunicam apenas por eventos:

| Serviço | Responsabilidade | Repositório |
|---|---|---|
| **Clientes** | Cadastra o cliente e publica `ClienteCadastrado` | [parana-clientes-api](https://github.com/gustavobordig/parana-clientes-api) |
| **Propostas** | Calcula o score, decide o crédito e publica `PropostaGerada` | [parana-propostas-api](https://github.com/gustavobordig/parana-propostas-api) |
| **Cartões** | Emite os cartões da proposta aprovada | este |
| Front | Cadastro e acompanhamento do fluxo em tempo real | [parana-credito-web](https://github.com/gustavobordig/parana-credito-web) |

![Fluxograma da solução](docs/fluxograma.svg)

Versão editável: [`docs/fluxograma.drawio`](docs/fluxograma.drawio), que abre no [draw.io](https://app.diagrams.net).

## Regra de emissão

| Proposta | Cartões emitidos | Limite por cartão |
|---|---|---|
| Negada (score 0 – 100) | nenhum | — |
| Aprovada (score 101 – 500) | 1 | R$ 1.000,00 |
| Aprovada (score 501 – 1000) | 2 | R$ 5.000,00 cada (R$ 10.000,00 no total) |

- A quantidade e o limite vêm do evento `PropostaGerada`; a decisão de crédito é do serviço de Propostas.
- Cada cartão recebe um final de 4 dígitos, distinto do outro cartão da mesma proposta.
- O número completo do cartão **nunca é armazenado nem exposto**: só os 4 últimos dígitos (`**** **** **** 1234`), seguindo a prática do PCI DSS de exibir o número truncado.

## Fluxo de mensagens

1. O consumer `PropostaGeradaConsumer` recebe o evento da fila `cartao-proposta-gerada`.
2. Proposta negada: o evento é consumido sem emitir nada.
3. Proposta já atendida: o evento é descartado (idempotência).
4. Emite os cartões e grava um documento no MongoDB.

### Evento consumido: `PropostaGerada`

```json
{
  "propostaId": "guid", "clienteId": "guid", "score": 750, "status": "Aprovada",
  "limitePorCartao": 5000.00, "quantidadeCartoes": 2, "ocorridoEm": "2026-10-29T10:00:01Z", "versao": 1
}
```

## Modelagem no MongoDB

Um documento por proposta aprovada, com os cartões embutidos. Eles são sempre lidos juntos e nunca passam de dois, então não há motivo para uma coleção separada.

```json
{
  "_id": "propostaId",
  "clienteId": "guid",
  "emitidoEm": "2026-10-29T10:00:02Z",
  "cartoes": [
    { "id": "guid", "numeroFinal": "1234", "limite": 5000.00 },
    { "id": "guid", "numeroFinal": "8731", "limite": 5000.00 }
  ]
}
```

- `_id` = id da proposta: o próprio banco impede uma segunda emissão para a mesma proposta.
- Índice em `clienteId`, criado na inicialização, para a consulta por cliente.
- Valores monetários como `Decimal128`, sem perda de precisão.

## Resiliência

| Mecanismo | Configuração |
|---|---|
| **Retry exponencial** | 3 novas tentativas, começando em 1 s, para falhas transitórias (Mongo fora, timeout). |
| **Sem retry para erro de regra** | `DomainException` (ex.: quantidade de cartões inválida) vai direto para a fila de erro. |
| **Dead-letter** | Esgotadas as tentativas, a mensagem vai para `cartao-proposta-gerada_error` e um `Fault<PropostaGerada>` é publicado. |
| **Circuit breaker** (kill switch) | Se 15% das últimas 10 mensagens falharem, o consumo pausa por 1 minuto. |
| **Idempotência** | Checagem prévia + `_id` único: uma inserção duplicada (consumo concorrente) é tratada como já emitida, sem erro. |
| **Inicialização tolerante** | Se o Mongo estiver fora no startup, a API sobe mesmo assim e o `/health` reporta a indisponibilidade. |

## Endpoints

| Método | Rota | Respostas |
|---|---|---|
| `GET` | `/api/v1/cartoes/{clienteId}` | `200` com a lista de cartões (vazia se negada ou ainda em processamento) |
| `GET` | `/health` | `Healthy` quando MongoDB e RabbitMQ estão acessíveis |

## Arquitetura

```
src/
├── Cartoes.Api             Controller, ProblemDetails, Swagger, versionamento, middlewares
├── Cartoes.Application     Casos de uso (emitir, listar) e porta do repositório
├── Cartoes.Domain          EmissaoCartoes e Cartao, sem dependências externas
├── Cartoes.Infrastructure  MongoDB (documentos, repositório, índice, health check) e MassTransit
└── Cartoes.Contracts       Record do evento de integração
tests/
└── Cartoes.Tests.Unit      xUnit + NSubstitute + test harness do MassTransit
```

## Decisões técnicas

| Decisão | Motivo |
|---|---|
| **MongoDB** | Persistência poliglota: a emissão é um agregado lido sempre inteiro, o caso típico de documento. |
| **Documentos separados do domínio** | As classes de domínio não carregam atributos do driver; o mapeamento fica na Infrastructure. |
| **Sem outbox neste serviço** | O Cartões não publica eventos. O outbox do MassTransit para Mongo exigiria replica set; a idempotência pelo `_id` cobre as reentregas. |
| **Aleatoriedade injetável** | O gerador dos finais é um `Random` injetado: os testes são determinísticos. |
| **MassTransit 8** | Retry, kill switch e dead-letter prontos. Fixado na v8 por licença (a v9 é comercial). |

## Como executar localmente

**Pré-requisitos:** .NET 10 SDK e Docker.

O RabbitMQ vem do `docker-compose` do [parana-clientes-api](https://github.com/gustavobordig/parana-clientes-api), que cria a rede `parana-credito`. Suba aquele primeiro e depois este, que sobe o MongoDB e a API:

```bash
docker compose up -d --build
```

Swagger em http://localhost:5103/swagger.

Para rodar fora do Docker, com o RabbitMQ do Clientes e o MongoDB deste compose no ar:

```bash
dotnet run --project src/Cartoes.Api
```

### Variáveis de ambiente

| Variável | Descrição | Padrão local |
|---|---|---|
| `Mongo__ConnectionString` | Conexão com o MongoDB | `mongodb://localhost:27017` |
| `Mongo__Database` | Nome do banco | `cartoes_db` |
| `RabbitMq__Url` | URL AMQP do RabbitMQ | `amqp://guest:guest@localhost:5672/` |
| `Cors__Origens__0` | Origem liberada para o front | `http://localhost:5173` |
| `PORT` | Porta HTTP (usada pelo Railway) | `8080` no container |

## Testes

```bash
dotnet test
```

22 testes de unidade com xUnit, no padrão AAA e com nomes `Metodo_Cenario_ResultadoEsperado`:

- **Emissão:**
  - um cartão de R$ 1.000 e dois de R$ 5.000;
  - finais distintos com 4 dígitos, mesmo quando o sorteio se repete;
  - zeros à esquerda;
  - quantidade e limite inválidos são rejeitados.
- **Caso de uso:** emite para proposta aprovada; ignora negada; ignora proposta já atendida; emissão concorrente não gera erro.
- **Listagem:** cartões com número mascarado e lista vazia.
- **Consumer** (test harness do MassTransit, sem RabbitMQ real): emite para proposta aprovada; consome negada sem emitir; Mongo fora gera `Fault` depois de 1 + 3 tentativas; erro de regra gera `Fault` sem retry.

O CI (GitHub Actions) roda build e testes a cada push e pull request.

## Deploy

As três APIs rodam no **Railway**, cada uma a partir do próprio `Dockerfile`, junto com SQL Server, MongoDB e RabbitMQ no mesmo projeto. O front está na Vercel.

![Projeto no Railway: as três APIs, SQL Server, MongoDB e RabbitMQ](docs/deploy-railway.webp)

As setas mostram as dependências de cada serviço: Clientes e Propostas usam o SQL Server (`server`), Cartões usa o MongoDB, e as três se conectam ao RabbitMQ.

## Próximos passos

- Testes de integração com Testcontainers (MongoDB e RabbitMQ reais).
- Evento `CartoesEmitidos` para notificar o cliente, com outbox em um MongoDB com replica set.
- Provisionar a topologia do RabbitMQ no deploy, para não depender da ordem em que os serviços sobem.
