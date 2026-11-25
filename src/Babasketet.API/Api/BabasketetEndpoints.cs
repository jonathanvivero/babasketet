using Babasketet.API.Application;
using Babasketet.API.Infrastructure;

namespace Babasketet.API.Api;

public static class BabasketetEndpoints
{
    public static IEndpointRouteBuilder MapBabasketetApi(this IEndpointRouteBuilder app)
    {
        var v1 = app.MapGroup("/api/v1");

        // === Perfil del llamante (/api/v1/me) ===
        v1.MapGet("/me", async (HttpContext http, UserDirectoryService userDir, CancellationToken ct) =>
            Results.Ok(await userDir.GetMeAsync(ExtractBearer(http), ct)))
            .AllowAnonymous();

        // === Casas e Invitaciones ===
        v1.MapGet("/houses", async (HouseService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetMyHousesAsync(ct)))
            .AllowAnonymous();

        v1.MapPost("/houses", async (CreateHouseRequest req, HouseService svc, CancellationToken ct) =>
            Results.Created("/api/v1/houses", await svc.CreateHouseAsync(req, ct)))
            .AllowAnonymous();

        v1.MapPost("/houses/{houseId:guid}/invitations", async (Guid houseId, HouseService svc, CancellationToken ct) =>
            Results.Ok(await svc.CreateInvitationAsync(houseId, ct)))
            .RequireAuthorization();

        v1.MapPost("/invitations/{token}/join", async (string token, HouseService svc, CancellationToken ct) =>
            Results.Ok(await svc.JoinByInvitationAsync(token, ct)))
            .AllowAnonymous();

        v1.MapPost("/houses/claim", async (
            ClaimHouseRequest? body,
            HttpContext http,
            HouseService svc,
            CancellationToken ct) =>
        {
            var token = body?.DeviceToken ?? http.Request.Headers["X-Device-Token"].FirstOrDefault();
            return Results.Ok(await svc.ClaimAnonymousHousesAsync(new ClaimHouseRequest(token), ct));
        }).RequireAuthorization();

        // === Tiendas / Supermercados ===
        v1.MapGet("/houses/{houseId:guid}/stores", async (Guid houseId, HouseService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetStoresAsync(houseId, ct)))
            .RequireAuthorization();

        v1.MapPost("/houses/{houseId:guid}/stores", async (Guid houseId, CreateStoreRequest req, HouseService svc, CancellationToken ct) =>
            Results.Created($"/api/v1/houses/{houseId}/stores", await svc.CreateStoreAsync(houseId, req, ct)))
            .RequireAuthorization();

        v1.MapPut("/houses/{houseId:guid}/active-store", async (Guid houseId, SetActiveStoreRequest req, HouseService svc, CancellationToken ct) =>
        {
            await svc.SetActiveStoreAsync(houseId, req, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // === Tipos de Producto ===
        v1.MapGet("/houses/{houseId:guid}/product-types", async (Guid houseId, HouseService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetProductTypesAsync(houseId, ct)))
            .RequireAuthorization();

        v1.MapPost("/houses/{houseId:guid}/product-types", async (Guid houseId, CreateProductTypeRequest req, HouseService svc, CancellationToken ct) =>
            Results.Created($"/api/v1/houses/{houseId}/product-types", await svc.CreateProductTypeAsync(houseId, req, ct)))
            .RequireAuthorization();

        // === Productos (Autocompletado / Historial) ===
        v1.MapGet("/houses/{houseId:guid}/products", async (Guid houseId, string? q, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.SearchProductsAsync(houseId, q, ct)))
            .RequireAuthorization();

        // === Pestaña Lista ===
        v1.MapGet("/houses/{houseId:guid}/list", async (Guid houseId, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetListAsync(houseId, ct)))
            .RequireAuthorization();

        v1.MapPost("/houses/{houseId:guid}/list/items", async (Guid houseId, AddListItemRequest req, ShoppingService svc, CancellationToken ct) =>
            Results.Created($"/api/v1/houses/{houseId}/list/items", await svc.AddListItemAsync(houseId, req, ct)))
            .RequireAuthorization();

        v1.MapPatch("/houses/{houseId:guid}/list/items/{itemId:guid}", async (Guid houseId, Guid itemId, UpdateListItemRequest req, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateListItemAsync(houseId, itemId, req, ct)))
            .RequireAuthorization();

        v1.MapPut("/houses/{houseId:guid}/list/reorder", async (Guid houseId, ReorderListRequest req, ShoppingService svc, CancellationToken ct) =>
        {
            await svc.ReorderListAsync(houseId, req, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        v1.MapPost("/houses/{houseId:guid}/list/items/{itemId:guid}/move-to-basket", async (Guid houseId, Guid itemId, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.MoveListItemToBasketAsync(houseId, itemId, ct)))
            .RequireAuthorization();

        v1.MapDelete("/houses/{houseId:guid}/list/items/{itemId:guid}", async (Guid houseId, Guid itemId, ShoppingService svc, CancellationToken ct) =>
        {
            await svc.DeleteListItemAsync(houseId, itemId, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        // === Pestaña Cesta y Precios SCD-2 ===
        v1.MapGet("/houses/{houseId:guid}/basket", async (Guid houseId, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetBasketAsync(houseId, ct)))
            .RequireAuthorization();

        v1.MapPatch("/houses/{houseId:guid}/basket/items/{itemId:guid}", async (Guid houseId, Guid itemId, UpdateBasketItemRequest req, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateBasketItemAsync(houseId, itemId, req, ct)))
            .RequireAuthorization();

        v1.MapPut("/houses/{houseId:guid}/basket/items/{itemId:guid}/price", async (Guid houseId, Guid itemId, SetPriceRequest req, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.SetBasketItemPriceAsync(houseId, itemId, req, ct)))
            .RequireAuthorization();

        v1.MapPost("/houses/{houseId:guid}/basket/items/{itemId:guid}/move-to-list", async (Guid houseId, Guid itemId, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.MoveBasketItemToListAsync(houseId, itemId, ct)))
            .RequireAuthorization();

        v1.MapDelete("/houses/{houseId:guid}/basket/items/{itemId:guid}", async (Guid houseId, Guid itemId, ShoppingService svc, CancellationToken ct) =>
        {
            await svc.DeleteBasketItemAsync(houseId, itemId, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        v1.MapPost("/houses/{houseId:guid}/basket/checkout", async (Guid houseId, ShoppingService svc, CancellationToken ct) =>
            Results.Created($"/api/v1/houses/{houseId}/purchases", await svc.CheckoutBasketAsync(houseId, ct)))
            .RequireAuthorization();

        // === Pestaña Compras ===
        v1.MapGet("/houses/{houseId:guid}/purchases", async (Guid houseId, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPurchasesAsync(houseId, ct)))
            .RequireAuthorization();

        v1.MapGet("/houses/{houseId:guid}/purchases/{purchaseId:guid}", async (Guid houseId, Guid purchaseId, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetPurchaseDetailAsync(houseId, purchaseId, ct)))
            .RequireAuthorization();

        v1.MapPatch("/houses/{houseId:guid}/purchases/{purchaseId:guid}/total", async (Guid houseId, Guid purchaseId, UpdateManualTotalRequest req, ShoppingService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateManualTotalAsync(houseId, purchaseId, req, ct)))
            .RequireAuthorization();

        v1.MapPost("/houses/{houseId:guid}/purchases/{purchaseId:guid}/receipt", async (Guid houseId, Guid purchaseId, ShoppingService svc, CancellationToken ct) =>
        {
            await svc.AttachReceiptAsync(houseId, purchaseId, ct);
            return Results.Ok();
        }).RequireAuthorization();

        // === Panel de Administración (/api/v1/admin/* - solo Admin) ===
        var admin = v1.MapGroup("/admin").RequireAuthorization();

        admin.MapGet("/houses", async (HttpContext http, AdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetHousesAsync(ExtractBearer(http), ct)));

        admin.MapGet("/houses/{houseId:guid}", async (Guid houseId, HttpContext http, AdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetHouseDetailAsync(houseId, ExtractBearer(http), ct)));

        admin.MapGet("/users", async (HttpContext http, AdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetUsersAsync(ExtractBearer(http), ct)));

        admin.MapGet("/users/{userId:guid}", async (Guid userId, HttpContext http, AdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetUserDetailAsync(userId, ExtractBearer(http), ct)));

        admin.MapPut("/users/{userId:guid}", async (Guid userId, AdminUpdateUserRequest req, HttpContext http, AdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateUserAsync(userId, req, ExtractBearer(http), ct)));

        admin.MapGet("/items", async (string? q, HttpContext http, AdminService svc, CancellationToken ct) =>
            Results.Ok(await svc.GetItemsAsync(q, ExtractBearer(http), ct)));

        app.MapHub<HouseHub>("/hubs/house");

        return app;
    }

    private static string? ExtractBearer(HttpContext http)
    {
        var authHeader = http.Request.Headers.Authorization.FirstOrDefault();
        return authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? authHeader["Bearer ".Length..].Trim()
            : null;
    }
}
