using System.Data;
using System.Net.Http.Headers;
using System.Text;
using HimLedger.Infrastructure;
using HimLedger.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api.Services;

public sealed class NotificationOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<NotificationOutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var endpoint = configuration["Notifications:WebhookUrl"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var webhookUri)
            || webhookUri.Scheme is not ("https" or "http"))
        {
            logger.LogWarning(
                "Notification outbox dispatch is disabled because Notifications:WebhookUrl is not configured with an HTTP(S) URL.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var messages = await LeaseBatchAsync(stoppingToken);
            if (messages.Count == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            foreach (var message in messages)
            {
                await DispatchAsync(webhookUri, message, stoppingToken);
            }
        }
    }

    private async Task<IReadOnlyList<NotificationOutboxMessage>> LeaseBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        var now = DateTimeOffset.UtcNow;
        var lockId = Guid.NewGuid().ToString("N");
        var messages = await context.NotificationOutboxMessages
            .Where(item => item.DispatchedAt == null
                && (item.NextAttemptAt == null || item.NextAttemptAt <= now)
                && (item.LockedUntil == null || item.LockedUntil <= now))
            .OrderBy(item => item.NotificationOutboxMessageId)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.LockId = lockId;
            message.LockedUntil = now.AddMinutes(2);
        }
        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return messages;
    }

    private async Task DispatchAsync(
        Uri webhookUri,
        NotificationOutboxMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, webhookUri)
            {
                Content = new StringContent(message.Payload, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("Idempotency-Key", message.IdempotencyKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await httpClientFactory.CreateClient(nameof(NotificationOutboxDispatcher))
                .SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            await MarkDispatchedAsync(message.NotificationOutboxMessageId, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Notification outbox delivery failed for message {MessageId}.", message.NotificationOutboxMessageId);
            await RecordFailureAsync(message.NotificationOutboxMessageId, exception.Message, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Notification outbox delivery timed out for message {MessageId}.", message.NotificationOutboxMessageId);
            await RecordFailureAsync(message.NotificationOutboxMessageId, exception.Message, cancellationToken);
        }
    }

    private async Task MarkDispatchedAsync(long id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await context.NotificationOutboxMessages.SingleAsync(
            item => item.NotificationOutboxMessageId == id, cancellationToken);
        message.DispatchedAt = DateTimeOffset.UtcNow;
        message.LockedUntil = null;
        message.LockId = null;
        message.LastError = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(long id, string error, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await context.NotificationOutboxMessages.SingleAsync(
            item => item.NotificationOutboxMessageId == id, cancellationToken);
        message.Attempts++;
        message.LastError = error.Length <= 2000 ? error : error[..2000];
        message.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(3600, Math.Pow(2, Math.Min(message.Attempts, 10))));
        message.LockedUntil = null;
        message.LockId = null;
        await context.SaveChangesAsync(cancellationToken);
    }
}
