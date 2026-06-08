# NotificationService

Serviço de notificações em C# / .NET 8 responsável por transformar eventos de negócio em entregas assíncronas por e-mail, SMS e push notification.

## Recursos implementados

- Planejamento idempotente de notificações a partir de eventos `TrackingStatusChangedIntegrationEvent`.
- Catálogo centralizado de políticas transacionais/opt-out.
- Templates versionados com snapshot de assunto e corpo renderizados na entrega.
- Uma `NotificationDelivery` independente por canal.
- Dispatch assíncrono via `BackgroundService`, claim concorrente com lease e `FOR UPDATE SKIP LOCKED`.
- Adapters HTTP por canal com `Idempotency-Key` baseada no `DeliveryId`.
- Outbox para publicação posterior de eventos de entrega.
- Webhook simplificado para receipts de providers.
- Endpoints para consulta de notificações, alteração de preferências e health checks.
