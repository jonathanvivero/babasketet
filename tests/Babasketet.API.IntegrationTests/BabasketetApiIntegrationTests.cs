using System.Net;
using System.Net.Http.Json;
using Babasketet.API.Application;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Babasketet.API.IntegrationTests;

public sealed class BabasketetApiIntegrationTests(BabasketetApiFixture fixture)
    : IClassFixture<BabasketetApiFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task IT_API_01_Seeds_ShouldContain24StoresAnd18Types()
    {
        using var client = fixture.CreateClientWithToken();
        var created = await (await client.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Seed")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();
        created.Should().NotBeNull();

        using var authed = fixture.CreateClientWithToken(created!.DeviceToken);
        var stores = await authed.GetFromJsonAsync<List<StoreDto>>($"/api/v1/houses/{created.House.Id}/stores");
        var types = await authed.GetFromJsonAsync<List<ProductTypeDto>>($"/api/v1/houses/{created.House.Id}/product-types");

        stores.Should().HaveCount(24);
        stores!.Single(s => s.IsDefault).Name.Should().Be("Default");
        types.Should().HaveCount(18);
    }

    [Fact]
    public async Task IT_API_02_AnonymousUser_ShouldBeLimitedTo1House()
    {
        using var anonClient = fixture.CreateClientWithToken();
        var first = await (await anonClient.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa 1")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();

        using var sameDevice = fixture.CreateClientWithToken(first!.DeviceToken);
        var secondResp = await sameDevice.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa 2"));

        secondResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task IT_API_03_ClaimAnonymousHouse_ShouldMigrateAndAllowUpTo4Houses()
    {
        using var anonClient = fixture.CreateClientWithToken();
        var first = await (await anonClient.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Anónima")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();

        using var devClient = fixture.CreateClientWithToken(first!.DeviceToken);
        await devClient.PostAsJsonAsync($"/api/v1/houses/{first.House.Id}/list/items",
            new AddListItemRequest(null, "Aceite", null, null));

        var userJwt = fixture.IssueZitadelJwt("zitadel-user-1");
        using var userClient = fixture.CreateClientWithToken(userJwt);
        var claimed = await (await userClient.PostAsJsonAsync("/api/v1/houses/claim",
            new ClaimHouseRequest(first.DeviceToken))).Content.ReadFromJsonAsync<HousesResponse>();

        claimed!.Houses.Should().ContainSingle(h => h.Id == first.House.Id);
        claimed.MaxHouses.Should().Be(4);

        // Puede crear 3 casas mas (total 4)
        for (var i = 2; i <= 4; i++)
        {
            var r = await userClient.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest($"Casa {i}"));
            r.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        // La 5a falla con 409 Conflict
        var fifth = await userClient.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa 5"));
        fifth.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task IT_API_04_Invitation_ShouldWorkWithin1Hour_AndFailAfter1Hour()
    {
        using var ownerAnon = fixture.CreateClientWithToken();
        var created = await (await ownerAnon.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Inv")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();
        using var owner = fixture.CreateClientWithToken(created!.DeviceToken);

        var inv = await (await owner.PostAsync($"/api/v1/houses/{created.House.Id}/invitations", null))
            .Content.ReadFromJsonAsync<InvitationResponse>();

        // Segundo dispositivo entra antes de 1h
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(30));
        using var guest1 = fixture.CreateClientWithToken();
        var joined = await guest1.PostAsync($"/api/v1/invitations/{inv!.Token}/join", null);
        joined.StatusCode.Should().Be(HttpStatusCode.OK);

        // Pasa más de 1 hora desde la creación -> 410 Gone
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(31));
        using var guest2 = fixture.CreateClientWithToken();
        var expired = await guest2.PostAsync($"/api/v1/invitations/{inv.Token}/join", null);
        expired.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task IT_API_06_07_08_Scd2PriceHistory_ShouldTrackChangesAndIsolateByStore()
    {
        using var anon = fixture.CreateClientWithToken();
        var created = await (await anon.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Precios")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();
        var houseId = created!.House.Id;
        using var client = fixture.CreateClientWithToken(created.DeviceToken);

        var stores = await client.GetFromJsonAsync<List<StoreDto>>($"/api/v1/houses/{houseId}/stores");
        var mercadona = stores!.First(s => s.Name == "Mercadona");
        var lidl = stores!.First(s => s.Name == "Lidl");

        // Activar Mercadona
        (await client.PutAsJsonAsync($"/api/v1/houses/{houseId}/active-store",
            new SetActiveStoreRequest(mercadona.Id))).EnsureSuccessStatusCode();

        // Añadir Leche y mover a Cesta
        var listItem = await (await client.PostAsJsonAsync($"/api/v1/houses/{houseId}/list/items",
            new AddListItemRequest(null, "Leche", null, null))).Content.ReadFromJsonAsync<ShoppingListItemDto>();
        var basketItem = await (await client.PostAsync(
            $"/api/v1/houses/{houseId}/list/items/{listItem!.Id}/move-to-basket", null))
            .Content.ReadFromJsonAsync<BasketItemDto>();

        // Sin precio inicialmente en Mercadona
        var initialBasket = await client.GetFromJsonAsync<BasketResponse>($"/api/v1/houses/{houseId}/basket");
        initialBasket!.Items.Single().CurrentPrice.Should().BeNull();

        // Consignar 1.20 -> 1.35 -> 1.10 en Mercadona
        (await client.PutAsJsonAsync($"/api/v1/houses/{houseId}/basket/items/{basketItem!.Id}/price",
            new SetPriceRequest(1.20m))).EnsureSuccessStatusCode();
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        (await client.PutAsJsonAsync($"/api/v1/houses/{houseId}/basket/items/{basketItem.Id}/price",
            new SetPriceRequest(1.35m))).EnsureSuccessStatusCode();
        fixture.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        (await client.PutAsJsonAsync($"/api/v1/houses/{houseId}/basket/items/{basketItem.Id}/price",
            new SetPriceRequest(1.10m))).EnsureSuccessStatusCode();

        // Verificar histórico SCD-2 en PostgreSQL real
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BabasketetDbContext>();
            var history = await db.ProductPriceHistories
                .Where(h => h.ProductId == basketItem.ProductId && h.StoreId == mercadona.Id)
                .OrderBy(h => h.ValidFrom)
                .ToListAsync();

            history.Should().HaveCount(3);
            history.Count(h => h.ValidTo == null).Should().Be(1);
            history.Last().Price.Should().Be(1.10m);
            history.Last().ValidTo.Should().BeNull();

            // IT-API-07: Intentar insertar segundo registro activo debe fallar por ux_product_price_active (23505)
            var act = async () => await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO product_price_history (product_id, store_id, valid_from, valid_to, price)
                VALUES ({basketItem.ProductId}, {mercadona.Id}, {DateTimeOffset.UtcNow.AddHours(2)}, NULL, 9.99)
                """);
            var ex = await act.Should().ThrowAsync<PostgresException>();
            ex.Which.SqlState.Should().Be("23505");
        }

        // IT-API-08: Cambiar tienda activa a Lidl -> precio debe ser null; volver a Mercadona -> 1.10
        await client.PutAsJsonAsync($"/api/v1/houses/{houseId}/active-store", new SetActiveStoreRequest(lidl.Id));
        var lidlBasket = await client.GetFromJsonAsync<BasketResponse>($"/api/v1/houses/{houseId}/basket");
        lidlBasket!.Items.Single().CurrentPrice.Should().BeNull();

        await client.PutAsJsonAsync($"/api/v1/houses/{houseId}/active-store", new SetActiveStoreRequest(mercadona.Id));
        var mercadonaBasket = await client.GetFromJsonAsync<BasketResponse>($"/api/v1/houses/{houseId}/basket");
        mercadonaBasket!.Items.Single().CurrentPrice.Should().Be(1.10m);
    }

    [Fact]
    public async Task IT_API_09_10_CheckoutAndPurchases_ShouldArchiveAndEnforcePremiumReceiptLock()
    {
        using var anon = fixture.CreateClientWithToken();
        var created = await (await anon.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Compras")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();
        var houseId = created!.House.Id;
        using var client = fixture.CreateClientWithToken(created.DeviceToken);

        var listItem = await (await client.PostAsJsonAsync($"/api/v1/houses/{houseId}/list/items",
            new AddListItemRequest(null, "Pan", null, "Ud"))).Content.ReadFromJsonAsync<ShoppingListItemDto>();
        var basketItem = await (await client.PostAsync(
            $"/api/v1/houses/{houseId}/list/items/{listItem!.Id}/move-to-basket", null))
            .Content.ReadFromJsonAsync<BasketItemDto>();

        await client.PatchAsJsonAsync($"/api/v1/houses/{houseId}/basket/items/{basketItem!.Id}",
            new UpdateBasketItemRequest(2m, null, false, null));
        await client.PutAsJsonAsync($"/api/v1/houses/{houseId}/basket/items/{basketItem.Id}/price",
            new SetPriceRequest(1.50m));

        var checkoutResp = await client.PostAsync($"/api/v1/houses/{houseId}/basket/checkout", null);
        checkoutResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var purchase = await checkoutResp.Content.ReadFromJsonAsync<PurchaseDetailDto>();
        purchase!.CalculatedTotal.Should().Be(3.00m);

        var basketAfter = await client.GetFromJsonAsync<BasketResponse>($"/api/v1/houses/{houseId}/basket");
        basketAfter!.Items.Should().BeEmpty();

        // Establecer total manual
        var updated = await (await client.PatchAsJsonAsync(
            $"/api/v1/houses/{houseId}/purchases/{purchase.Id}/total",
            new UpdateManualTotalRequest(2.85m))).Content.ReadFromJsonAsync<PurchaseSummaryDto>();
        updated!.EffectiveTotal.Should().Be(2.85m);

        // Ticket bloqueado para usuario Free -> 403 Forbidden
        var receiptFree = await client.PostAsync($"/api/v1/houses/{houseId}/purchases/{purchase.Id}/receipt", null);
        receiptFree.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IT_API_11_SignalR_ShouldBroadcastChangesAcross3Devices()
    {
        var userJwt1 = fixture.IssueZitadelJwt("user-signalr-1");
        var userJwt2 = fixture.IssueZitadelJwt("user-signalr-2");
        var userJwt3 = fixture.IssueZitadelJwt("user-signalr-3");

        using var c1 = fixture.CreateClientWithToken(userJwt1);
        var created = await (await c1.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Sync")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();
        var houseId = created!.House.Id;

        var inv = await (await c1.PostAsync($"/api/v1/houses/{houseId}/invitations", null))
            .Content.ReadFromJsonAsync<InvitationResponse>();

        using var c2 = fixture.CreateClientWithToken(userJwt2);
        using var c3 = fixture.CreateClientWithToken(userJwt3);
        (await c2.PostAsync($"/api/v1/invitations/{inv!.Token}/join", null)).EnsureSuccessStatusCode();
        (await c3.PostAsync($"/api/v1/invitations/{inv.Token}/join", null)).EnsureSuccessStatusCode();

        await using var hub2 = CreateHubConnection(userJwt2);
        await using var hub3 = CreateHubConnection(userJwt3);

        var tcs2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tcs3 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub2.On<object>("ListUpdated", _ => tcs2.TrySetResult());
        hub3.On<object>("ListUpdated", _ => tcs3.TrySetResult());

        await hub2.StartAsync();
        await hub3.StartAsync();
        await hub2.InvokeAsync("JoinHouseGroup", houseId);
        await hub3.InvokeAsync("JoinHouseGroup", houseId);

        (await c1.PostAsJsonAsync($"/api/v1/houses/{houseId}/list/items",
            new AddListItemRequest(null, "Café", null, null))).EnsureSuccessStatusCode();

        await Task.WhenAll(
            tcs2.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            tcs3.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task IT_API_12_RenameItem_InListAndBasket_ShouldUpdateAndAvoidDuplicateProductCrash()
    {
        using var anon = fixture.CreateClientWithToken();
        var created = await (await anon.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Renombrar")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();
        var houseId = created!.House.Id;
        using var client = fixture.CreateClientWithToken(created.DeviceToken);

        // Creamos previamente un producto "Leche Desnatada" para probar colisión de nombre normalizado
        var existingItem = await (await client.PostAsJsonAsync($"/api/v1/houses/{houseId}/list/items",
            new AddListItemRequest(null, "Leche Desnatada", null, null))).Content.ReadFromJsonAsync<ShoppingListItemDto>();
        (await client.DeleteAsync($"/api/v1/houses/{houseId}/list/items/{existingItem!.Id}")).EnsureSuccessStatusCode();

        var item = await (await client.PostAsJsonAsync($"/api/v1/houses/{houseId}/list/items",
            new AddListItemRequest(null, "Leche", null, null))).Content.ReadFromJsonAsync<ShoppingListItemDto>();

        // Renombrar en Lista a un nombre nuevo
        var renamedInList = await (await client.PatchAsJsonAsync(
            $"/api/v1/houses/{houseId}/list/items/{item!.Id}",
            new UpdateListItemRequest("Leche Entera", null, false, null))).Content.ReadFromJsonAsync<ShoppingListItemDto>();
        renamedInList!.Name.Should().Be("Leche Entera");

        // Mover a Cesta y renombrar al producto ya existente ("Leche Desnatada") sin violar unique constraint
        var basketItem = await (await client.PostAsync(
            $"/api/v1/houses/{houseId}/list/items/{item.Id}/move-to-basket", null))
            .Content.ReadFromJsonAsync<BasketItemDto>();

        var renamedInBasket = await (await client.PatchAsJsonAsync(
            $"/api/v1/houses/{houseId}/basket/items/{basketItem!.Id}",
            new UpdateBasketItemRequest(null, null, false, null, "Leche Desnatada"))).Content.ReadFromJsonAsync<BasketItemDto>();
        renamedInBasket!.Name.Should().Be("Leche Desnatada");

        var basket = await client.GetFromJsonAsync<BasketResponse>($"/api/v1/houses/{houseId}/basket");
        basket!.Items.Should().ContainSingle(i => i.Name == "Leche Desnatada");
    }

    private HubConnection CreateHubConnection(string token)
    {
        var handler = fixture.Factory.Server.CreateHandler();
        return new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, "/hubs/house"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => handler;
            })
            .Build();
    }
}
