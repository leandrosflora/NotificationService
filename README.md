# NotificationService

Microserviço de notificações construído em **C# / .NET 8** para transformar eventos de negócio em entregas assíncronas por **e-mail**, **SMS** e **push notification**. O serviço recebe eventos de alteração de status de rastreamento, aplica políticas de notificação, renderiza templates versionados, respeita preferências do destinatário quando permitido e despacha cada entrega para o provider correto com idempotência.

## Índice

- [Visão geral](#visão-geral)
- [Principais responsabilidades](#principais-responsabilidades)
- [Arquitetura](#arquitetura)
- [Fluxo de processamento](#fluxo-de-processamento)
- [Tecnologias e dependências](#tecnologias-e-dependências)
- [Configuração](#configuração)
- [Como executar localmente](#como-executar-localmente)
- [Endpoints HTTP](#endpoints-http)
- [Contratos de entrada e saída](#contratos-de-entrada-e-saída)
- [Domínio](#domínio)
- [Persistência](#persistência)
- [Workers em segundo plano](#workers-em-segundo-plano)
- [Providers externos](#providers-externos)
- [Idempotência, resiliência e retries](#idempotência-resiliência-e-retries)
- [Observabilidade e saúde](#observabilidade-e-saúde)
- [Estrutura de pastas](#estrutura-de-pastas)
- [Limitações e próximos passos](#limitações-e-próximos-passos)

## Visão geral

O `NotificationService` atua como uma camada dedicada para planejamento e entrega de notificações. Em vez de um serviço de negócio enviar mensagens diretamente para providers externos, ele publica um evento para este microserviço, que passa a controlar:

1. **Deduplicação do evento recebido** por meio de uma tabela de inbox.
2. **Mapeamento do evento para um tipo de notificação**.
3. **Aplicação de políticas por tipo de notificação**, incluindo prioridade, canais padrão e possibilidade de opt-out.
4. **Resolução de contatos do destinatário** por canal.
5. **Seleção de templates ativos e versionados**.
6. **Criação de uma entrega independente por canal**.
7. **Despacho assíncrono** com workers, lease de processamento e retries.
8. **Publicação futura de eventos de entrega** via outbox.
9. **Atualização de status por callbacks de providers**.

## Principais responsabilidades

- Planejar notificações a partir do evento `TrackingStatusChangedIntegrationEvent`.
- Garantir que o mesmo evento de origem não gere notificações duplicadas.
- Criar entregas independentes para e-mail, SMS e push notification.
- Respeitar preferências de opt-out para notificações transacionais opcionais.
- Manter snapshot do template utilizado no momento da criação da entrega.
- Enviar requisições HTTP aos providers configurados por canal.
- Aplicar resiliência em chamadas HTTP com timeout, retry, circuit breaker e timeout total via `Microsoft.Extensions.Http.Resilience`.
- Registrar eventos de negócio em outbox para publicação posterior.
- Processar receipts/webhooks de providers para marcar entregas como entregues, com bounce ou falha.
- Expor endpoints de consulta, preferências e health checks.

## Arquitetura

O projeto segue uma organização em camadas simples:

```text
Cliente / Serviço de negócio
          |
          v
Minimal APIs
          |
          v
Application Services
          |
          +--> Domain
          |
          +--> Infrastructure / EF Core / PostgreSQL
          |
          +--> Providers HTTP
          |
          v
Background Workers
```

### Camadas

| Camada | Responsabilidade | Exemplos |
| --- | --- | --- |
| `Api` | Define endpoints HTTP por meio de Minimal APIs. | Notificações, preferências e callbacks de providers. |
| `Contracts` | Define DTOs e eventos de integração usados nas bordas do serviço. | Eventos recebidos, eventos publicados e payload de receipt. |
| `Domain` | Contém entidades, enums e regras de estado do domínio. | `Notification`, `NotificationDelivery`, `NotificationPreference`. |
| `Application` | Orquestra casos de uso e regras de aplicação. | Planejamento, despacho, processamento de receipts e renderização de templates. |
| `Infrastructure` | Implementa persistência, inbox, outbox e workers. | `NotificationDbContext`, `NotificationRepository`, `OutboxDispatcher`. |
| `Providers` | Encapsula integração HTTP com canais externos. | Email, SMS e Push. |

## Fluxo de processamento

### 1. Entrada do evento de rastreamento

O endpoint `POST /notifications/tracking-status-changed` recebe um `TrackingStatusChangedIntegrationEvent` e delega o processamento ao `NotificationPlanner`.

### 2. Deduplicação por inbox

Antes de criar uma nova notificação, o serviço consulta `inbox_messages` usando o `MessageId` do evento. Se o evento já foi processado, o método retorna sem criar novos registros.

### 3. Mapeamento para tipo de notificação

O status do rastreamento é convertido para um `NotificationType`:

| `CurrentStatus` recebido | Tipo de notificação gerado |
| --- | --- |
| `OutForDelivery` | `OutForDelivery` |
| `Delivered` | `Delivered` |
| `Exception` | `DeliveryException` |

Qualquer status sem mapeamento explícito gera erro de configuração/regra.

### 4. Aplicação da política de notificação

Cada tipo possui uma política com canais padrão, prioridade e indicação se o usuário pode optar por não receber a notificação.

| Tipo | Canais padrão | Opt-out permitido | Prioridade |
| --- | --- | --- | --- |
| `OrderConfirmed` | Email, Push | Não | High |
| `OutForDelivery` | Push, Email | Sim | High |
| `Delivered` | Push, Email | Sim | Normal |
| `DeliveryException` | Push, Email, SMS | Não | Critical |
| `PaymentFailed` | Email, Push | Não | Critical |
| `ShipmentCreated` | Email, Push | Sim | Normal |

### 5. Resolução de contatos e preferências

Para cada canal configurado na política, o serviço:

1. Busca o contato do destinatário em `recipient_contacts`.
2. Resolve o destino correto do canal:
   - Email: `Email`.
   - SMS: `PhoneNumber`.
   - Push: `PushToken`.
3. Ignora o canal se não houver destino cadastrado.
4. Se a política permitir opt-out, consulta `notification_preferences` e ignora canais explicitamente desabilitados.

### 6. Template e renderização

O serviço busca templates ativos para o tipo, canal e locale da notificação. O template de maior versão é escolhido por canal.

Os placeholders aceitos seguem o formato:

```text
{{nomeDaChave}}
```

Para eventos de rastreamento, os valores disponíveis são:

| Placeholder | Origem |
| --- | --- |
| `{{trackingCode}}` | Código de rastreamento do evento. |
| `{{status}}` | Status atual recebido no evento. |
| `{{estimatedDeliveryDate}}` | Data estimada formatada como `dd/MM/yyyy`, ou `não informada`. |
| `{{exceptionCode}}` | Código de exceção, ou string vazia. |

### 7. Criação da notificação e entregas

Uma `Notification` é criada para o evento de origem. Para cada canal elegível é criada uma `NotificationDelivery` independente, contendo:

- Canal.
- Destino.
- Template e versão usados.
- Snapshot do assunto e corpo renderizados.
- Estado inicial `Pending`.
- Data mínima para envio (`NotBefore`).

Se nenhum canal for elegível, a notificação é marcada como `Suppressed`.

### 8. Dispatch assíncrono

O `NotificationDispatchWorker` executa a cada segundo, reivindica até 100 entregas pendentes e processa os envios em paralelo com grau máximo de paralelismo igual a 20.

A reivindicação usa SQL com:

- Atualização para status `Sending`.
- `processing_token` para identificar o lote reivindicado.
- `processing_lease_until` com lease de 2 minutos.
- Incremento de `attempts`.
- `FOR UPDATE SKIP LOCKED` para concorrência segura entre instâncias.

A ordenação prioriza canais na seguinte ordem:

1. Push.
2. SMS.
3. Email.

### 9. Envio para providers

Cada entrega é enviada pelo adapter do canal correspondente:

| Canal | Endpoint do provider | Payload principal |
| --- | --- | --- |
| Email | `POST /v1/emails` | `to`, `subject`, `body` |
| SMS | `POST /v1/sms` | `to`, `text` |
| Push | `POST /v1/push` | `token`, `title`, `body` |

Todas as chamadas incluem o header `Idempotency-Key` com o `DeliveryId` em formato `N`, permitindo idempotência no provider externo.

### 10. Atualização de status e outbox

Quando o provider aceita uma entrega, ela é marcada como `Accepted`, o `ProviderMessageId` é salvo e um `NotificationAcceptedIntegrationEvent` é adicionado à outbox.

Em falhas permanentes, a entrega é marcada como `Failed` e um `NotificationDeliveryFailedIntegrationEvent` é adicionado à outbox.

Em callbacks de providers, uma entrega pode ser marcada como:

- `Delivered`, com emissão de `NotificationDeliveredIntegrationEvent`.
- `Bounced`, quando o provider informar `Bounced` ou `Failed`.

Após cada atualização de entrega, o status agregado da notificação é recalculado.

## Tecnologias e dependências

- **.NET 8** com ASP.NET Core Minimal APIs.
- **Entity Framework Core 8**.
- **Npgsql Entity Framework Core Provider** para PostgreSQL.
- **Microsoft.Extensions.Http.Resilience** para resiliência em chamadas HTTP.
- **Health Checks do Entity Framework Core**.
- **Swashbuckle.AspNetCore** para Swagger em ambiente de desenvolvimento.

## Configuração

As configurações principais ficam em `appsettings.json`.

```json
{
  "ConnectionStrings": {
    "NotificationDb": "Host=localhost;Port=5432;Database=notification_service;Username=postgres;Password=postgres"
  },
  "Providers": {
    "Email": {
      "BaseUrl": "https://email-provider.local"
    },
    "Sms": {
      "BaseUrl": "https://sms-provider.local"
    },
    "Push": {
      "BaseUrl": "https://push-provider.local"
    }
  }
}
```

### Variáveis/configurações esperadas

| Chave | Obrigatória | Descrição |
| --- | --- | --- |
| `ConnectionStrings:NotificationDb` | Sim | String de conexão PostgreSQL usada pelo `NotificationDbContext`. |
| `Providers:Email:BaseUrl` | Sim | URL base do provider de e-mail. |
| `Providers:Sms:BaseUrl` | Sim | URL base do provider de SMS. |
| `Providers:Push:BaseUrl` | Sim | URL base do provider de push notification. |

> Observação: se uma URL de provider não estiver configurada, a aplicação lança `InvalidOperationException` na inicialização do respectivo `HttpClient`.

## Como executar localmente

### Pré-requisitos

- .NET SDK 8 instalado.
- PostgreSQL acessível pela string de conexão configurada.
- Tabelas compatíveis com o modelo EF Core do serviço.
- Providers HTTP de e-mail, SMS e push disponíveis ou simulados.

### Restaurar dependências

```bash
dotnet restore
```

### Compilar

```bash
dotnet build
```

### Executar

```bash
dotnet run
```

Em ambiente de desenvolvimento, a documentação Swagger fica disponível pelo middleware do Swashbuckle quando a aplicação estiver rodando.

> Importante: o repositório não possui migrations versionadas atualmente. Antes de rodar em um ambiente real, crie/aplique migrations ou provisione o schema PostgreSQL de acordo com o mapeamento do `NotificationDbContext`.

## Endpoints HTTP

### Health checks

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/health` | Executa os checks registrados. |
| `GET` | `/health/live` | Check simples de vida da aplicação. |
| `GET` | `/health/ready` | Check de prontidão com validação do `NotificationDbContext`. |

### Notificações

#### Consultar notificação

```http
GET /notifications/{notificationId}
```

Retorna a notificação, seus dados principais e entregas associadas. O destino é mascarado para reduzir exposição de dados sensíveis.

Exemplo de resposta:

```json
{
  "id": "c7ff7a2f-8b3d-4be5-8105-5a6b3e1f69a1",
  "recipientId": "7a57a92c-810d-44dd-a4c5-265c6a1a4cc7",
  "type": "OutForDelivery",
  "status": "Sent",
  "priority": 3,
  "createdAt": "2026-06-10T12:00:00+00:00",
  "deliveries": [
    {
      "id": "2c93823f-4629-4d1f-b5f7-b86b4296f43a",
      "channel": "Push",
      "status": "Accepted",
      "templateVersion": 2,
      "attempts": 1,
      "acceptedAt": "2026-06-10T12:00:02+00:00",
      "deliveredAt": null,
      "destination": "***abcd"
    }
  ]
}
```

#### Receber evento de status de rastreamento

```http
POST /notifications/tracking-status-changed
Content-Type: application/json
```

Payload:

```json
{
  "messageId": "9e00db85-4e16-4c39-8e34-02e5e08fa412",
  "shipmentId": "86a192e3-2d30-42e9-899f-a8642cf7dc5c",
  "buyerId": "7a57a92c-810d-44dd-a4c5-265c6a1a4cc7",
  "trackingCode": "BR123456789",
  "currentStatus": "OutForDelivery",
  "occurredAt": "2026-06-10T12:00:00+00:00",
  "estimatedDeliveryDate": "2026-06-11",
  "exceptionCode": null
}
```

Resposta esperada:

```http
202 Accepted
```

### Preferências de notificação

#### Criar ou atualizar preferência

```http
PUT /notification-preferences/{recipientId}/{type}/{channel}
Content-Type: application/json
```

Parâmetros de rota:

| Parâmetro | Exemplo | Descrição |
| --- | --- | --- |
| `recipientId` | `7a57a92c-810d-44dd-a4c5-265c6a1a4cc7` | Identificador do destinatário. |
| `type` | `OutForDelivery` | Valor de `NotificationType`. |
| `channel` | `Email` | Valor de `NotificationChannel`. |

Payload:

```json
{
  "enabled": false
}
```

Resposta esperada:

```http
204 No Content
```

### Callbacks de providers

#### Receber receipt de entrega

```http
POST /providers/{provider}/receipts
Content-Type: application/json
```

Payload:

```json
{
  "providerEventId": "evt_123",
  "providerMessageId": "msg_456",
  "status": "Delivered",
  "occurredAt": "2026-06-10T12:05:00+00:00",
  "signature": "assinatura-do-provider"
}
```

Status reconhecidos atualmente:

| Status do provider | Efeito |
| --- | --- |
| `Delivered` | Marca a entrega como `Delivered` e grava evento `NotificationDeliveredIntegrationEvent` na outbox. |
| `Bounced` | Marca a entrega como `Bounced`. |
| `Failed` | Marca a entrega como `Bounced`. |

Resposta esperada:

```http
202 Accepted
```

## Contratos de entrada e saída

### Evento recebido: `TrackingStatusChangedIntegrationEvent`

| Campo | Tipo | Descrição |
| --- | --- | --- |
| `MessageId` | `Guid` | Identificador único do evento, usado para idempotência no inbox. |
| `ShipmentId` | `Guid` | Identificador da entrega/remessa. |
| `BuyerId` | `Guid` | Identificador do destinatário/comprador. |
| `TrackingCode` | `string` | Código de rastreamento. |
| `CurrentStatus` | `string` | Status atual a ser mapeado para `NotificationType`. |
| `OccurredAt` | `DateTimeOffset` | Data/hora em que o evento ocorreu. |
| `EstimatedDeliveryDate` | `DateOnly?` | Data estimada de entrega. |
| `ExceptionCode` | `string?` | Código de exceção, quando aplicável. |

### Eventos gravados na outbox

Todos os eventos abaixo são gravados com tópico `notification.events` e `aggregateKey` igual ao `NotificationId`.

| Evento | Quando ocorre |
| --- | --- |
| `NotificationAcceptedIntegrationEvent` | Provider aceita uma entrega. |
| `NotificationDeliveryFailedIntegrationEvent` | Entrega falha de forma permanente ou excede tentativas. |
| `NotificationDeliveredIntegrationEvent` | Provider confirma entrega via receipt. |

## Domínio

### Tipos de notificação

```text
OrderConfirmed
ShipmentCreated
OutForDelivery
Delivered
DeliveryException
OrderCancelled
PaymentFailed
```

> Nem todos os tipos possuem política cadastrada atualmente. O catálogo possui políticas para `OrderConfirmed`, `OutForDelivery`, `Delivered`, `DeliveryException`, `PaymentFailed` e `ShipmentCreated`.

### Canais

```text
Email
Sms
Push
```

### Status da notificação

| Status | Significado |
| --- | --- |
| `Pending` | Notificação criada e ainda sem processamento agregado final. |
| `Processing` | Há entregas em andamento, pendentes ou em retry. |
| `Sent` | Todas as entregas foram aceitas ou entregues. |
| `PartiallySent` | Parte das entregas teve sucesso e as demais terminaram em falha/supressão/bounce. |
| `Suppressed` | Nenhuma entrega elegível foi criada. |
| `Failed` | Todas as entregas terminaram em falha, bounce ou supressão. |

### Status da entrega

| Status | Significado |
| --- | --- |
| `Pending` | Entrega criada e aguardando claim do worker. |
| `Sending` | Entrega reivindicada e em processamento. |
| `RetryScheduled` | Falha transitória com nova tentativa agendada. |
| `Accepted` | Provider aceitou a mensagem. |
| `Delivered` | Provider confirmou entrega final. |
| `Bounced` | Provider reportou bounce ou falha posterior. |
| `Suppressed` | Entrega suprimida. |
| `Failed` | Falha permanente ou esgotamento de tentativas. |

## Persistência

O `NotificationDbContext` mapeia as seguintes tabelas:

| Tabela | Descrição |
| --- | --- |
| `notifications` | Cabeçalho da notificação planejada. |
| `notification_deliveries` | Entregas individuais por canal. |
| `notification_templates` | Templates versionados por tipo, canal e locale. |
| `recipient_contacts` | Contatos do destinatário por canal. |
| `notification_preferences` | Preferências de opt-in/opt-out por destinatário, tipo e canal. |
| `inbox_messages` | Controle de idempotência para eventos recebidos. |
| `outbox_messages` | Eventos de integração pendentes de publicação. |

### Índices importantes

- `notifications.source_event_id` é único para impedir duplicação de notificações por evento de origem.
- `notification_deliveries` possui índice para busca por status, datas de tentativa e lease.
- `notification_deliveries.provider_message_id` permite localizar entregas a partir de receipts.
- `notification_templates` possui unicidade por tipo, canal, locale e versão.
- `notification_preferences` possui unicidade por destinatário, tipo e canal.
- `outbox_messages` possui índice para busca de mensagens não processadas e próximas tentativas.

## Workers em segundo plano

### `NotificationDispatchWorker`

- Executa a cada 1 segundo.
- Reivindica até 100 entregas por ciclo.
- Processa entregas em paralelo com `MaxDegreeOfParallelism = 20`.
- Usa escopos de DI separados para cada processamento.

### `OutboxDispatcher`

- Executa a cada 5 segundos.
- Busca até 50 mensagens de outbox pendentes.
- Atualmente registra em log que a mensagem está pronta para o tópico/agregado e marca como processada.
- Deve ser evoluído para publicar efetivamente em um broker, como Kafka, RabbitMQ, SNS/SQS ou outro mecanismo adotado pela plataforma.

## Providers externos

### Email

- URL base: `Providers:Email:BaseUrl`.
- Endpoint: `POST /v1/emails`.
- Timeout do `HttpClient`: 5 segundos.
- Resiliência configurada:
  - Timeout total: 8 segundos.
  - Timeout por tentativa: 4 segundos.
  - Circuit breaker com razão de falha 0,5, throughput mínimo 10, janela de 30 segundos e abertura de 20 segundos.

### SMS

- URL base: `Providers:Sms:BaseUrl`.
- Endpoint: `POST /v1/sms`.
- Resiliência configurada:
  - Timeout total: 8 segundos.
  - Timeout por tentativa: 4 segundos.

### Push

- URL base: `Providers:Push:BaseUrl`.
- Endpoint: `POST /v1/push`.
- Resiliência configurada:
  - Timeout total: 8 segundos.
  - Timeout por tentativa: 4 segundos.

### Tratamento de erros dos providers

| Canal | Códigos tratados como falha permanente |
| --- | --- |
| Email | `400`, `401`, `403`, `404` |
| SMS | `400`, `401`, `403`, `404` |
| Push | `400`, `401`, `403`, `404`, `410` |

Demais códigos não bem-sucedidos geram exceção transitória e entram no fluxo de retry.

## Idempotência, resiliência e retries

### Idempotência de entrada

- O `MessageId` do evento recebido é registrado em `inbox_messages`.
- Se o mesmo `MessageId` for recebido novamente, o processamento é ignorado.
- `notifications.source_event_id` também possui índice único.

### Idempotência de saída

- Cada chamada ao provider envia `Idempotency-Key` igual ao `DeliveryId` sem hífens.
- Isso permite que o provider deduplique reenvios da mesma entrega.

### Retry de entrega

- Tentativas são incrementadas durante o claim da entrega.
- Falhas transitórias agendam retry com backoff exponencial.
- O backoff usa `2^attempt` segundos, limitado a 300 segundos, com jitter de até 1000 ms.
- O limite máximo é de 8 tentativas.
- Ao exceder o limite, a entrega é marcada como `Failed`.

### Lease de processamento

- Cada entrega reivindicada recebe lease de 2 minutos.
- Caso o processo morra ou o lease expire, outra instância pode reivindicar novamente a entrega em status `Sending` com lease vencido.

## Observabilidade e saúde

### Logs

O serviço registra logs para:

- Falhas no ciclo de dispatch de notificações.
- Falhas no ciclo de dispatch de outbox.
- Falhas transitórias de envio por delivery.
- Mensagens de outbox prontas para publicação.

### Health checks

- `/health/live`: indica que o processo está vivo.
- `/health/ready`: valida prontidão incluindo acesso ao `NotificationDbContext`.
- `/health`: executa os checks registrados conforme configuração padrão do ASP.NET Core.

## Estrutura de pastas

```text
.
├── Api/
│   ├── NotificationEndpoints.cs
│   ├── PreferenceEndpoints.cs
│   └── ProviderCallbackEndpoints.cs
├── Application/
│   ├── NotificationPlanner.cs
│   ├── NotificationDispatchProcessor.cs
│   ├── NotificationPolicyCatalog.cs
│   ├── ProviderReceiptProcessor.cs
│   ├── TemplateRenderer.cs
│   └── Ports/
├── Contracts/
│   ├── IntegrationEvents.cs
│   ├── ProviderContracts.cs
│   └── Responses.cs
├── Domain/
│   ├── Enums.cs
│   ├── Notification.cs
│   ├── NotificationDelivery.cs
│   ├── NotificationPreference.cs
│   ├── NotificationTemplate.cs
│   └── RecipientContact.cs
├── Infrastructure/
│   ├── Inbox/
│   ├── Outbox/
│   ├── Persistence/
│   └── Workers/
├── Providers/
│   ├── EmailChannelSender.cs
│   ├── SmsChannelSender.cs
│   ├── PushChannelSender.cs
│   ├── INotificationChannelSender.cs
│   └── NotificationSenderFactory.cs
├── Program.cs
├── appsettings.json
└── NotificationService.csproj
```

## Limitações e próximos passos

- Implementar migrations EF Core e versionar o schema do banco.
- Substituir o `OutboxDispatcher` atual por publicação real em broker de mensagens.
- Validar assinaturas de callbacks usando o campo `Signature` do receipt.
- Adicionar autenticação/autorização nos endpoints administrativos e callbacks.
- Adicionar testes unitários e de integração.
- Criar seed de templates e contatos para ambiente local.
- Atualizar `NotificationService.http` com exemplos reais dos endpoints atuais.
- Adicionar métricas para latência, taxa de sucesso, retries, bounces e backlog de outbox/deliveries.
- Avaliar mascaramento adicional/criptografia para destinos sensíveis em repouso.
