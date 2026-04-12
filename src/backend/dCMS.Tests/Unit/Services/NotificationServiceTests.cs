using dCMS.Core.Persistence;
using dCMS.Core.Services;
using FluentAssertions;
using Moq;

namespace dCMS.Tests.Unit.Services;

public sealed class NotificationServiceTests
{
    [Fact]
    public async Task GetUnreadCount_delegates_to_persistence()
    {
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.CountUnreadNotificationsAsync("t1", "u1", It.IsAny<CancellationToken>())).ReturnsAsync(3);
        var svc = new NotificationService(persistence.Object);

        var n = await svc.GetUnreadCountAsync("t1", "u1");

        n.Should().Be(3);
    }

    [Fact]
    public async Task MarkAllRead_delegates_to_persistence()
    {
        var now = DateTimeOffset.Parse("2026-04-12T12:00:00Z");
        var persistence = new Mock<ICatalogPersistence>();
        persistence.Setup(x => x.MarkAllNotificationsReadAsync("t1", "u1", now, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        var svc = new NotificationService(persistence.Object);

        var updated = await svc.MarkAllReadAsync("t1", "u1", now);

        updated.Should().Be(2);
    }
}
