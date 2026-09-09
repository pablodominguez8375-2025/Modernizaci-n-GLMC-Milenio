using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PMGM.Api.Data;
using PMGM.Api.Modules.Notifications;
using PMGM.Api.Modules.Notifications.Entities;
using Xunit;

namespace PMGM.Api.Tests.Integration;

[Collection(PostgresIntegrationCollection.Name)]
public sealed class NotificationPostgreSqlTests
{
    [Fact]
    public async Task Notification_migration_queue_and_idempotency_work_on_postgresql()
    {
        var connectionString = Environment.GetEnvironmentVariable("PMGM_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new NotificationDbContext(options);
        await db.Database.MigrateAsync(cancellationToken);

        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        Assert.Contains("20260909071500_AddInstitutionalNotifications", applied);

        var uniqueCode = $"test.notification.{Guid.NewGuid():N}";
        var template = new NotificationTemplate
        {
            Code = uniqueCode,
            Version = 1,
            Name = "Plantilla QA",
            SubjectTemplate = "Aviso {{numero}}",
            BodyTemplate = "La solicitud {{numero}} cambió de estado.",
            AllowedVariablesJson = JsonSerializer.Serialize(new[] { "numero" }),
            Sensitivity = NotificationCodes.Classification.Internal,
            Status = NotificationCodes.TemplateStatus.Active
        };
        db.NotificationTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);

        var service = new InstitutionalNotificationService(db);
        var idempotencyKey = $"qa:{Guid.NewGuid():N}";
        var command = new QueueNotificationCommand(
            uniqueCode,
            null,
            "qa.notification",
            "qa-user",
            null,
            new[] { NotificationCodes.Channel.Internal },
            new Dictionary<string, string?> { ["numero"] = "123" },
            idempotencyKey,
            "qa-correlation",
            "qa-event",
            "/qa/123",
            true,
            null);

        var first = await service.QueueAsync(command, cancellationToken);
        var second = await service.QueueAsync(command, cancellationToken);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.MessageId, second.MessageId);

        var message = await db.NotificationMessages
            .AsNoTracking()
            .Include(x => x.Deliveries)
            .ThenInclude(x => x.Attempts)
            .SingleAsync(x => x.Id == first.MessageId, cancellationToken);

        Assert.Equal("Aviso 123", message.Subject);
        Assert.Single(message.Deliveries);
        Assert.Equal(NotificationCodes.DeliveryStatus.Delivered, message.Deliveries.Single().Status);
        Assert.Single(message.Deliveries.Single().Attempts);
    }
}
