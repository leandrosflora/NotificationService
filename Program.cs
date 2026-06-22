using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using NotificationService.Api;
using NotificationService.Application;
using NotificationService.Application.Ports;
using NotificationService.Infrastructure.Outbox;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Workers;
using NotificationService.Providers;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Environment.ApplicationName;
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:5107";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<NotificationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("NotificationDb"));
});

builder.Services.AddScoped<NotificationPlanner>();
builder.Services.AddScoped<NotificationDispatchProcessor>();
builder.Services.AddScoped<ProviderReceiptProcessor>();

builder.Services.AddSingleton<NotificationPolicyCatalog>();
builder.Services.AddSingleton<TemplateRenderer>();

builder.Services.Configure<MockNotificationRepositoryOptions>(
    builder.Configuration.GetSection(MockNotificationRepositoryOptions.SectionName));
builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection(KafkaOptions.SectionName));

if (builder.Configuration.GetValue<bool>("FeatureFlags:MockNotificationRepository"))
{
    builder.Services.AddScoped<INotificationRepository, MockNotificationRepository>();
}
else
{
    builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
}
builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();

builder.Services
    .AddHttpClient<EmailChannelSender>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Providers:Email:BaseUrl"]
            ?? throw new InvalidOperationException("Email provider URL is missing"));
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
        //options.Retry.DisableForUnsafeHttpMethods();
        options.CircuitBreaker.FailureRatio = 0.5;
        options.CircuitBreaker.MinimumThroughput = 10;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(20);
    });

builder.Services
    .AddHttpClient<SmsChannelSender>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Providers:Sms:BaseUrl"]
            ?? throw new InvalidOperationException("SMS provider URL is missing"));
    })
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
        //options.Retry.DisableForUnsafeHttpMethods();
    });

builder.Services
    .AddHttpClient<PushChannelSender>(client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["Providers:Push:BaseUrl"]
            ?? throw new InvalidOperationException("Push provider URL is missing"));
    })
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(8);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
        //options.Retry.DisableForUnsafeHttpMethods();
    });

builder.Services.AddTransient<INotificationChannelSender>(provider => provider.GetRequiredService<EmailChannelSender>());
builder.Services.AddTransient<INotificationChannelSender>(provider => provider.GetRequiredService<SmsChannelSender>());
builder.Services.AddTransient<INotificationChannelSender>(provider => provider.GetRequiredService<PushChannelSender>());

builder.Services.AddScoped<NotificationSenderFactory>();

builder.Services.AddHostedService<NotificationDispatchWorker>();
builder.Services.AddHostedService<OutboxDispatcher>();
builder.Services.AddHostedService<KafkaNotificationConsumer>();

builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<NotificationDbContext>(tags: ["ready"]);

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint("/metrics");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapNotificationEndpoints();
app.MapPreferenceEndpoints();
app.MapProviderCallbackEndpoints();

app.Run();
