Use o repositrio `https://github.com/leandrosflora/logistica-envios-demo-arch` como fonte de contexto arquitetural.

Este repositrio representa um microservice da arquitetura Logística Envios.

4. No crie integrao HTTP/evento sem validar o contrato em `logistica-envios-demo-arch/docs/contracts`.

1. Verifique o contrato consolidado em `logistica-envios-demo-arch/docs/contracts/logistica-envios-apis.openapi.yaml`.
Antes de publicar ou consumir eventos, verifique `logistica-envios-demo-arch/docs/contracts/kafka-events.md`.
- `docs/adr`
- `docs/c4`
- `docs/sequence-diagrams`

Não invente dependências, padrões, integrações, eventos ou contratos fora dos padrões definidos no repositório de arquitetura.

## Responsabilidade deste microservice

Este repositório representa um microservice da arquitetura Meli Envios.

Ao implementar ou alterar código:

1. Mantenha o limite de responsabilidade do serviço.
2. Não mova regra de negócio para outro domínio indevidamente.
3. Não acesse banco de dados de outro microservice.
4. Não crie integração HTTP/evento sem validar o contrato em `meli-envios-architecture/docs/contracts`.
5. Não altere eventos Kafka sem validar `docs/contracts/kafka-events.md`.
6. Não altere fluxos principais sem considerar os diagramas em `docs/sequence-diagrams`.

## Padrões técnicos

Use como padrão:

- .NET 8
- C#
- ASP.NET Core Minimal APIs ou Controllers, conforme padrão já usado no repo
- Clean Architecture / Hexagonal Architecture
- Separação clara entre `Api`, `Application`, `Domain`, `Infrastructure` e `Contracts`
- DTOs explícitos para requests/responses
- Validação de entrada
- Logs estruturados
- `X-Correlation-Id`
- `Idempotency-Key` em comandos críticos
- Timeouts explícitos para chamadas HTTP
- Retry apenas em operações idempotentes
- Circuit breaker para chamadas downstream críticas

## Contratos HTTP

Antes de criar ou alterar endpoints:

1. Verifique o contrato consolidado em `meli-envios-architecture/docs/contracts/meli-envios-apis.openapi.yaml`.
2. Se o endpoint já existir no OpenAPI, implemente exatamente o request/response definido.
3. Se precisar alterar o contrato, atualize primeiro o OpenAPI consolidado.
4. Se este serviço consumir outro microservice, o contrato canônico é sempre o contrato do serviço dono da API.

## Eventos Kafka

Antes de publicar ou consumir eventos, verifique `meli-envios-architecture/docs/contracts/kafka-events.md`.

Use envelope padrão com:

- `eventId`
- `eventType`
- `schemaVersion`
- `occurredAt`
- `correlationId`
- `producer`
- `payload`

Não crie tópico novo sem documentar no repositório de arquitetura.

## Qualidade mínima

Antes de considerar a alteração concluída, execute:

```bash
dotnet restore
dotnet build
dotnet test