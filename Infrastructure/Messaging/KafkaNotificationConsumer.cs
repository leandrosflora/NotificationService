using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificationService.Application;
using NotificationService.Contracts;

namespace NotificationService.Infrastructure.Messaging;

public sealed class KafkaNotificationConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaNotificationConsumer> _logger;

    public KafkaNotificationConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaOptions> options,
        ILogger<KafkaNotificationConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            _logger.LogWarning("Kafka consumer disabled because Kafka:BootstrapServers is empty");
            return;
        }

        var topics = new[]
        {
            _options.Topics.OrderCreated,
            _options.Topics.ShipmentCreated,
            _options.Topics.ShipmentStatusUpdated
        }.Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct().ToArray();

        if (topics.Length == 0)
        {
            _logger.LogWarning("Kafka consumer disabled because no canonical topics are configured");
            return;
        }

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            ClientId = "notification-service"
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topics);

        _logger.LogInformation(
            "Kafka notification consumer subscribed to {Topics} using group {ConsumerGroupId} and bootstrap servers {BootstrapServers}",
            string.Join(", ", topics),
            _options.ConsumerGroupId,
            _options.BootstrapServers);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(stoppingToken);
                await ProcessAsync(result, stoppingToken);
                consumer.StoreOffset(result);
                consumer.Commit(result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Kafka consume failed. Reason: {Reason}", ex.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (KafkaException ex)
            {
                _logger.LogError(ex, "Kafka commit/store failed. Reason: {Reason}", ex.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while processing Kafka notification event");
            }
        }

        consumer.Close();
    }

    private async Task ProcessAsync(ConsumeResult<string, string> result, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(result.Message.Value);
        var eventType = ReadRequiredString(document.RootElement, "eventType");
        var eventId = ReadRequiredGuid(document.RootElement, "eventId");
        var correlationId = ReadOptionalString(document.RootElement, "correlationId") ?? eventId.ToString("N");

        _logger.LogInformation(
            "Consuming Kafka event from topic {Topic} with key {MessageKey}, eventType {EventType} and correlationId {CorrelationId}",
            result.Topic,
            result.Message.Key,
            eventType,
            correlationId);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var planner = scope.ServiceProvider.GetRequiredService<NotificationPlanner>();

        if (result.Topic == _options.Topics.OrderCreated)
        {
            EnsureEventTypeMatchesTopic(result.Topic, eventType, correlationId);
            var envelope = DeserializeEnvelope<OrderCreatedPayload>(document.RootElement);
            ValidateBuyerId(envelope.Payload.BuyerId, envelope.EventId, envelope.EventType, correlationId);
            await planner.HandleAsync(envelope, cancellationToken);
            return;
        }

        if (result.Topic == _options.Topics.ShipmentCreated)
        {
            EnsureEventTypeMatchesTopic(result.Topic, eventType, correlationId);
            var envelope = DeserializeEnvelope<ShipmentCreatedPayload>(document.RootElement);
            ValidateBuyerId(envelope.Payload.BuyerId, envelope.EventId, envelope.EventType, correlationId);
            await planner.HandleAsync(envelope, cancellationToken);
            return;
        }

        if (result.Topic == _options.Topics.ShipmentStatusUpdated)
        {
            EnsureEventTypeMatchesTopic(result.Topic, eventType, correlationId);
            var envelope = DeserializeEnvelope<ShipmentStatusUpdatedPayload>(document.RootElement);
            ValidateBuyerId(envelope.Payload.BuyerId, envelope.EventId, envelope.EventType, correlationId);
            await planner.HandleAsync(envelope, cancellationToken);
            return;
        }

        _logger.LogWarning(
            "Ignoring Kafka event from topic {Topic} with key {MessageKey}, eventType {EventType} and correlationId {CorrelationId} because it is outside NotificationService scope",
            result.Topic,
            result.Message.Key,
            eventType,
            correlationId);
    }

    private void EnsureEventTypeMatchesTopic(string topic, string eventType, string correlationId)
    {
        if (!string.Equals(eventType, topic, StringComparison.Ordinal))
        {
            _logger.LogError(
                "Kafka eventType {EventType} does not match topic {Topic}; correlationId {CorrelationId}",
                eventType,
                topic,
                correlationId);

            throw new JsonException($"Kafka eventType '{eventType}' must match topic '{topic}'");
        }
    }

    private void ValidateBuyerId(Guid buyerId, Guid eventId, string eventType, string correlationId)
    {
        if (buyerId == Guid.Empty)
        {
            _logger.LogError(
                "Kafka event {EventId} ({EventType}) is missing required buyerId; NotificationService cannot plan a notification without buyerId. CorrelationId: {CorrelationId}",
                eventId,
                eventType,
                correlationId);

            throw new JsonException($"Kafka event '{eventId}' of type '{eventType}' is missing required buyerId");
        }
    }

    private static KafkaEventEnvelope<TPayload> DeserializeEnvelope<TPayload>(JsonElement root)
    {
        return JsonSerializer.Deserialize<KafkaEventEnvelope<TPayload>>(root.GetRawText(), JsonOptions)
            ?? throw new JsonException($"Could not deserialize Kafka envelope for payload {typeof(TPayload).Name}");
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        return ReadOptionalString(root, propertyName)
            ?? throw new JsonException($"Kafka envelope property '{propertyName}' is required");
    }

    private static string? ReadOptionalString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static Guid ReadRequiredGuid(JsonElement root, string propertyName)
    {
        var value = ReadRequiredString(root, propertyName);
        return Guid.TryParse(value, out var id)
            ? id
            : throw new JsonException($"Kafka envelope property '{propertyName}' must be a GUID");
    }
}
