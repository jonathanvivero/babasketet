using Babasketet.API.Application;
using Babasketet.API.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Infrastructure;

[Authorize]
public sealed class HouseHub(
    BabasketetDbContext db,
    ICallerContextAccessor callerAccessor) : Hub
{
    public static string GroupName(Guid houseId) => $"house:{houseId:D}";

    public async Task JoinHouseGroup(Guid houseId)
    {
        var caller = callerAccessor.Current;
        var hasAccess = await db.HouseMembers.AnyAsync(m =>
            m.HouseId == houseId &&
            ((caller.UserId != null && m.UserId == caller.UserId) ||
             (caller.DeviceId != null && m.DeviceId == caller.DeviceId)));

        if (!hasAccess)
        {
            throw new HubException("No tienes acceso a esta casa.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(houseId));
    }

    public Task LeaveHouseGroup(Guid houseId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(houseId));
}

public sealed class SignalRHouseNotifier(IHubContext<HouseHub> hubContext) : IHouseNotifier
{
    public Task NotifyListUpdatedAsync(Guid houseId, CancellationToken ct = default) =>
        hubContext.Clients.Group(HouseHub.GroupName(houseId))
            .SendAsync("ListUpdated", new { houseId, timestamp = DateTimeOffset.UtcNow }, ct);

    public Task NotifyBasketUpdatedAsync(Guid houseId, CancellationToken ct = default) =>
        hubContext.Clients.Group(HouseHub.GroupName(houseId))
            .SendAsync("BasketUpdated", new { houseId, timestamp = DateTimeOffset.UtcNow }, ct);

    public Task NotifyPurchaseCompletedAsync(Guid houseId, Guid purchaseId, CancellationToken ct = default) =>
        hubContext.Clients.Group(HouseHub.GroupName(houseId))
            .SendAsync("PurchaseCompleted", new { houseId, purchaseId, timestamp = DateTimeOffset.UtcNow }, ct);

    public Task NotifyActiveStoreChangedAsync(Guid houseId, Guid storeId, CancellationToken ct = default) =>
        hubContext.Clients.Group(HouseHub.GroupName(houseId))
            .SendAsync("ActiveStoreChanged", new { houseId, storeId, timestamp = DateTimeOffset.UtcNow }, ct);

    public Task NotifyCatalogUpdatedAsync(Guid houseId, string catalogType, CancellationToken ct = default) =>
        hubContext.Clients.Group(HouseHub.GroupName(houseId))
            .SendAsync("CatalogUpdated", new { houseId, catalogType, timestamp = DateTimeOffset.UtcNow }, ct);
}
