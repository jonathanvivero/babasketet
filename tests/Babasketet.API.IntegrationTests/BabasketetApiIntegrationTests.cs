using System.Net;
using System.Net.Http.Json;
using Babasketet.API.Application;
using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    [Fact]
    public async Task IT_API_13_Migrations_ShouldBeSyncAndReversibleUpDown()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BabasketetDbContext>();

        db.Database.HasPendingModelChanges().Should().BeFalse();

        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261125000001_InitialBaseline");
        var appliedAfterDown = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        appliedAfterDown.Should().Contain("20261125000001_InitialBaseline");
        appliedAfterDown.Should().NotContain("20261125000002_AddAppUsersAndOwnershipRules");
        appliedAfterDown.Should().NotContain("20261126000001_AddSystemEmailConfig");
        appliedAfterDown.Should().NotContain("20261126000002_AddAdminNotifications");

        await migrator.MigrateAsync("20261126000002_AddAdminNotifications");
        var appliedAfterUp = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        appliedAfterUp.Should().Contain("20261125000002_AddAppUsersAndOwnershipRules");
        appliedAfterUp.Should().Contain("20261126000001_AddSystemEmailConfig");
        appliedAfterUp.Should().Contain("20261126000002_AddAdminNotifications");

        await DatabaseInitializer.SeedCatalogsAsync(db);
    }

    [Fact]
    public async Task IT_API_14_RoleAndOwnershipRules_GuestVsUserVsOwnerVsAdmin()
    {
        // 1. Invitado anónimo (Guest) crea una casa -> NO es owner, solo 'member' y GlobalRole='Guest'
        using var anon = fixture.CreateClientWithToken();
        var anonCreated = await (await anon.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Guest")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();
        var houseAnonId = anonCreated!.House.Id;

        using var guest1 = fixture.CreateClientWithToken(anonCreated.DeviceToken);
        var guestMe = await guest1.GetFromJsonAsync<MeResponse>("/api/v1/me");
        guestMe!.GlobalRole.Should().Be("Guest");
        guestMe.IsAuthenticated.Should().BeFalse();
        guestMe.IsAdmin.Should().BeFalse();
        guestMe.EffectiveRoles.Should().Equal("Guest");
        guestMe.Houses.Should().ContainSingle(h => h.HouseId == houseAnonId && h.HouseRole == "member");

        // Invitación generada en casa Guest -> otro Guest se une como 'member'
        var inv1 = await (await guest1.PostAsync($"/api/v1/houses/{houseAnonId}/invitations", null))
            .Content.ReadFromJsonAsync<InvitationResponse>();
        using var guest2Anon = fixture.CreateClientWithToken();
        var guest2Joined = await (await guest2Anon.PostAsync($"/api/v1/invitations/{inv1!.Token}/join", null))
            .Content.ReadFromJsonAsync<JoinInvitationResponse>();
        using var guest2 = fixture.CreateClientWithToken(guest2Joined!.DeviceToken);

        // El primer Guest se registra (User) y reclama la casa -> deja de ser Guest y queda como Owner
        var user1Jwt = fixture.IssueZitadelJwt("registered-first", roles: ["User"], displayName: "Usuario Uno");
        using var user1 = fixture.CreateClientWithToken(user1Jwt);
        await user1.PostAsJsonAsync("/api/v1/houses/claim", new ClaimHouseRequest(anonCreated.DeviceToken));
        var user1Me = await user1.GetFromJsonAsync<MeResponse>("/api/v1/me");
        user1Me!.GlobalRole.Should().Be("User");
        user1Me.IsAuthenticated.Should().BeTrue();
        user1Me.EffectiveRoles.Should().Contain("User").And.Contain("Owner");
        user1Me.Houses.Should().ContainSingle(h => h.HouseId == houseAnonId && h.HouseRole == "owner");

        // El segundo Guest se registra después -> pasa a User pero queda como 'member' (ya existe Owner)
        var user2Jwt = fixture.IssueZitadelJwt("registered-second", roles: ["User"]);
        using var user2 = fixture.CreateClientWithToken(user2Jwt);
        await user2.PostAsJsonAsync("/api/v1/houses/claim", new ClaimHouseRequest(guest2Joined.DeviceToken));
        var user2Me = await user2.GetFromJsonAsync<MeResponse>("/api/v1/me");
        user2Me!.Houses.Should().ContainSingle(h => h.HouseId == houseAnonId && h.HouseRole == "member");

        // Casa Guest (sin usuarios registrados) invita a un User ya registrado -> al canjear queda como Owner
        using var anonOther = fixture.CreateClientWithToken();
        var anonHouse2 = await (await anonOther.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Sin User")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>();
        using var guestOwnerless = fixture.CreateClientWithToken(anonHouse2!.DeviceToken);
        var inv2 = await (await guestOwnerless.PostAsync($"/api/v1/houses/{anonHouse2.House.Id}/invitations", null))
            .Content.ReadFromJsonAsync<InvitationResponse>();

        var user3Jwt = fixture.IssueZitadelJwt("registered-invited", roles: ["User"]);
        using var user3 = fixture.CreateClientWithToken(user3Jwt);
        (await user3.PostAsync($"/api/v1/invitations/{inv2!.Token}/join", null)).EnsureSuccessStatusCode();
        var user3Me = await user3.GetFromJsonAsync<MeResponse>("/api/v1/me");
        user3Me!.Houses.Should().ContainSingle(h => h.HouseId == anonHouse2.House.Id && h.HouseRole == "owner");

        // Un User normal recibe 403 al intentar acceder al panel /api/v1/admin/*
        var forbiddenResp = await user1.GetAsync("/api/v1/admin/houses");
        forbiddenResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IT_API_15_AdminPanelEndpoints_ShouldManageHousesUsersBlockingAndGroupedItems()
    {
        var userJwt = fixture.IssueZitadelJwt("owner-user", roles: ["User"], displayName: "Dueño Casa");
        using var userClient = fixture.CreateClientWithToken(userJwt);

        var h1 = (await (await userClient.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Norte")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>())!.House;
        var h2 = (await (await userClient.PostAsJsonAsync("/api/v1/houses", new CreateHouseRequest("Casa Sur")))
            .Content.ReadFromJsonAsync<CreateHouseResponse>())!.House;

        var types = await userClient.GetFromJsonAsync<List<ProductTypeDto>>($"/api/v1/houses/{h1.Id}/product-types");
        var dairyTypeId = types!.First(t => t.Name == "Lacteos").Id;
        var fruitTypeId = types!.First(t => t.Name == "Fruta/Verdura").Id;

        // Añadir mismo término exacto (case-insensitive) y mismo tipo en 2 casas -> "2 casas"
        await userClient.PostAsJsonAsync($"/api/v1/houses/{h1.Id}/list/items",
            new AddListItemRequest(null, "Leche", dairyTypeId, "L"));
        await userClient.PostAsJsonAsync($"/api/v1/houses/{h2.Id}/list/items",
            new AddListItemRequest(null, "leche", dairyTypeId, "L"));
        // Término distinto por tipo
        await userClient.PostAsJsonAsync($"/api/v1/houses/{h1.Id}/list/items",
            new AddListItemRequest(null, "Piña", fruitTypeId, "Ud"));

        var listItem = await (await userClient.PostAsJsonAsync($"/api/v1/houses/{h1.Id}/list/items",
            new AddListItemRequest(null, "Arroz", null, "Kg"))).Content.ReadFromJsonAsync<ShoppingListItemDto>();
        await userClient.PostAsync($"/api/v1/houses/{h1.Id}/list/items/{listItem!.Id}/move-to-basket", null);

        // Login como Admin (seed email o rol Admin)
        var adminJwt = fixture.IssueZitadelJwt(
            "admin-jonathan",
            roles: ["User", "Admin"],
            email: "jonathanvivero@gmail.com",
            displayName: "Jonathan Vivero");
        using var adminClient = fixture.CreateClientWithToken(adminJwt);

        var houses = await adminClient.GetFromJsonAsync<List<AdminHouseSummaryDto>>("/api/v1/admin/houses");
        houses.Should().HaveCount(2);

        var detailH1 = await adminClient.GetFromJsonAsync<AdminHouseDetailDto>($"/api/v1/admin/houses/{h1.Id}");
        detailH1!.Members.Should().ContainSingle(m => m.HouseRole == "owner");
        detailH1.TotalProductsCount.Should().Be(3);
        detailH1.ListItems.Should().HaveCount(2);
        detailH1.BasketItems.Should().ContainSingle(b => b.Name == "Arroz");

        // Listado global de ítems agrupado + ordenado alfabéticamente + filtro texto
        var allItems = await adminClient.GetFromJsonAsync<List<AdminCatalogItemDto>>("/api/v1/admin/items");
        allItems!.Select(i => i.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
        allItems.Should().ContainSingle(i =>
            i.Name.Equals("leche", StringComparison.OrdinalIgnoreCase) &&
            i.HouseCount == 2 &&
            i.LocationLabel == "2 casas");
        allItems.Should().ContainSingle(i =>
            i.Name == "Piña" &&
            i.HouseCount == 1 &&
            i.LocationLabel == "Casa Norte");

        var filtered = await adminClient.GetFromJsonAsync<List<AdminCatalogItemDto>>("/api/v1/admin/items?q=piña");
        filtered.Should().ContainSingle(i => i.Name == "Piña");

        // Edición de usuario y bloqueo inmediato
        var users = await adminClient.GetFromJsonAsync<List<AdminUserSummaryDto>>("/api/v1/admin/users");
        var ownerUser = users!.Single(u => u.ZitadelUserId == "owner-user");

        var updateResp = await adminClient.PutAsJsonAsync($"/api/v1/admin/users/{ownerUser.Id}",
            new AdminUpdateUserRequest(
                "Blocked",
                false,
                "Dueño Editado",
                new Dictionary<string, string> { ["nota"] = "Prueba BD" },
                [new AdminUserHouseRoleUpdate(h1.Id, true)]));
        updateResp.EnsureSuccessStatusCode();
        var updatedUser = await updateResp.Content.ReadFromJsonAsync<AdminUserDetailDto>();
        updatedUser!.Status.Should().Be("Blocked");
        updatedUser.CustomProperties["nota"].Should().Be("Prueba BD");

        // El usuario bloqueado ya no puede acceder a sus endpoints -> 403 USER_BLOCKED
        var blockedCall = await userClient.GetAsync("/api/v1/houses");
        blockedCall.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task IT_API_16_SmtpEmailConfig_ShouldPersistProtectAndSendTestEmail()
    {
        var userJwt = fixture.IssueZitadelJwt("normal-user-smtp", roles: ["User"]);
        using var userClient = fixture.CreateClientWithToken(userJwt);
        (await userClient.GetAsync("/api/v1/admin/settings/email")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminJwt = fixture.IssueZitadelJwt("admin-smtp", roles: ["User", "Admin"], email: "jonathanvivero@gmail.com");
        using var adminClient = fixture.CreateClientWithToken(adminJwt);

        var emptyCfg = await adminClient.GetFromJsonAsync<EmailConfigDto>("/api/v1/admin/settings/email");
        emptyCfg!.IsConfigured.Should().BeFalse();

        // Guardar SMTP con contraseña
        var saveResp = await adminClient.PutAsJsonAsync("/api/v1/admin/settings/email",
            new SaveEmailConfigRequest(
                "smtp.example.org",
                587,
                "smtp-user",
                "SuperSecretSmtpPass!",
                false,
                "StartTls",
                "noreply@babasketet.test",
                "Babasketet Mailer",
                true));
        saveResp.EnsureSuccessStatusCode();
        var saved = await saveResp.Content.ReadFromJsonAsync<EmailConfigDto>();
        saved!.IsConfigured.Should().BeTrue();
        saved.Host.Should().Be("smtp.example.org");
        saved.Port.Should().Be(587);
        saved.HasPassword.Should().BeTrue();

        // Actualizar sin enviar contraseña conserva la contraseña cifrada en BD
        var updateResp = await adminClient.PutAsJsonAsync("/api/v1/admin/settings/email",
            new SaveEmailConfigRequest(
                "smtp2.example.org",
                2525,
                "smtp-user",
                null,
                false,
                "None",
                "noreply@babasketet.test",
                "Babasketet Mailer 2",
                true));
        updateResp.EnsureSuccessStatusCode();
        (await updateResp.Content.ReadFromJsonAsync<EmailConfigDto>())!.HasPassword.Should().BeTrue();

        // Enviar correo de prueba verifica contraseña desencriptada y entrega
        var testResp = await adminClient.PostAsJsonAsync("/api/v1/admin/settings/email/test",
            new TestEmailRequest("destino.prueba@babasketet.test"));
        testResp.EnsureSuccessStatusCode();
        var testResult = await testResp.Content.ReadFromJsonAsync<TestEmailResultDto>();
        testResult!.Success.Should().BeTrue();
        testResult.Recipient.Should().Be("destino.prueba@babasketet.test");

        fixture.FakeSmtp.SentMessages.Should().ContainSingle(m =>
            m.ToEmail == "destino.prueba@babasketet.test" &&
            m.Settings.Host == "smtp2.example.org" &&
            m.Settings.Password == "SuperSecretSmtpPass!");
    }

    [Fact]
    public async Task IT_API_17_AdminNotifications_ShouldEmailAllAdmins_Paginate10_AndTransitionStates()
    {
        var admin1Jwt = fixture.IssueZitadelJwt("admin-notif-1", roles: ["User", "Admin"], email: "admin1@babasketet.test");
        var admin2Jwt = fixture.IssueZitadelJwt("admin-notif-2", roles: ["User", "Admin"], email: "admin2@babasketet.test");

        using var admin1 = fixture.CreateClientWithToken(admin1Jwt);
        using var admin2 = fixture.CreateClientWithToken(admin2Jwt);

        await admin1.GetAsync("/api/v1/me");
        await admin2.GetAsync("/api/v1/me");

        // Configurar SMTP para verificar notificación por email
        (await admin1.PutAsJsonAsync("/api/v1/admin/settings/email",
            new SaveEmailConfigRequest("smtp.test.local", 1025, null, null, true, "None", "noreply@babasketet.test", "Babasketet", true)))
            .EnsureSuccessStatusCode();

        // Crear 12 notificaciones -> paginado de 10 en 10
        var createdIds = new List<Guid>();
        for (var i = 1; i <= 12; i++)
        {
            var resp = await admin1.PostAsJsonAsync("/api/v1/admin/notifications",
                new CreateAdminNotificationRequest($"Alerta #{i}", $"Detalle completo de alerta {i}", "system"));
            resp.StatusCode.Should().Be(HttpStatusCode.Created);
            var dto = await resp.Content.ReadFromJsonAsync<AdminNotificationDto>();
            dto!.Status.Should().Be("New");
            dto.ReadAt.Should().BeNull();
            dto.ArchivedAt.Should().BeNull();
            createdIds.Add(dto.Id);
            fixture.TimeProvider.Advance(TimeSpan.FromSeconds(1));
        }

        // Debe haber enviado email a ambos admins registrados (y seed admin)
        fixture.FakeSmtp.SentMessages.Should().Contain(m => m.ToEmail == "admin1@babasketet.test" && m.Subject.Contains("Alerta #1"));
        fixture.FakeSmtp.SentMessages.Should().Contain(m => m.ToEmail == "admin2@babasketet.test" && m.Subject.Contains("Alerta #1"));

        var page1 = await admin1.GetFromJsonAsync<AdminNotificationsPageDto>("/api/v1/admin/notifications?status=New&page=1&pageSize=10");
        page1!.Items.Should().HaveCount(10);
        page1.TotalCount.Should().Be(12);
        page1.TotalPages.Should().Be(2);
        page1.NewCount.Should().Be(12);

        var page2 = await admin1.GetFromJsonAsync<AdminNotificationsPageDto>("/api/v1/admin/notifications?status=New&page=2&pageSize=10");
        page2!.Items.Should().HaveCount(2);

        // Transiciones: New -> Read -> Archived -> New
        var targetId = createdIds[0];
        var readDto = await (await admin1.PatchAsJsonAsync(
            $"/api/v1/admin/notifications/{targetId}/status",
            new UpdateNotificationStatusRequest("Read"))).Content.ReadFromJsonAsync<AdminNotificationDto>();
        readDto!.Status.Should().Be("Read");
        readDto.ReadAt.Should().NotBeNull();
        readDto.ArchivedAt.Should().BeNull();

        var archivedDto = await (await admin1.PatchAsJsonAsync(
            $"/api/v1/admin/notifications/{targetId}/status",
            new UpdateNotificationStatusRequest("Archived"))).Content.ReadFromJsonAsync<AdminNotificationDto>();
        archivedDto!.Status.Should().Be("Archived");
        archivedDto.ReadAt.Should().NotBeNull();
        archivedDto.ArchivedAt.Should().NotBeNull();

        var multiFilter = await admin1.GetFromJsonAsync<AdminNotificationsPageDto>("/api/v1/admin/notifications?status=Read,Archived");
        multiFilter!.Items.Should().ContainSingle(n => n.Id == targetId);

        var backToNew = await (await admin1.PatchAsJsonAsync(
            $"/api/v1/admin/notifications/{targetId}/status",
            new UpdateNotificationStatusRequest("New"))).Content.ReadFromJsonAsync<AdminNotificationDto>();
        backToNew!.Status.Should().Be("New");
        backToNew.ReadAt.Should().BeNull();
        backToNew.ArchivedAt.Should().BeNull();
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
