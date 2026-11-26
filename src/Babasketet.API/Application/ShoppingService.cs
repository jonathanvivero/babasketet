using Babasketet.API.Domain;
using Babasketet.API.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Babasketet.API.Application;

public sealed class ShoppingService(
    BabasketetDbContext db,
    HouseService houseService,
    ICallerContextAccessor callerAccessor,
    IHouseNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ProductSuggestionDto>> SearchProductsAsync(Guid houseId, string? query, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);

        var productsQuery = db.Products
            .Include(p => p.ProductType)
            .Where(p => p.HouseId == houseId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = DomainCatalogs.NormalizeProductName(query);
            productsQuery = productsQuery.Where(p => p.NormalizedName.Contains(normalized));
        }

        var products = await productsQuery
            .OrderByDescending(p => p.LastUsedAt)
            .Take(50)
            .ToListAsync(ct);

        var productIds = products.Select(p => p.Id).ToList();
        var activePrices = await GetActivePricesMapAsync(productIds, house.ActiveStoreId, ct);

        return products.Select(p => new ProductSuggestionDto(
            p.Id,
            p.Name,
            p.ProductTypeId,
            ToTypeDto(p.ProductType),
            p.DefaultFormat,
            activePrices.TryGetValue(p.Id, out var price) ? price : null)).ToList();
    }

    public async Task<IReadOnlyList<ShoppingListItemDto>> GetListAsync(Guid houseId, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);

        var items = await db.ShoppingListItems
            .Include(i => i.Product)
            .Include(i => i.ProductType)
            .Where(i => i.HouseId == houseId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.CreatedAt)
            .ToListAsync(ct);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var activePrices = await GetActivePricesMapAsync(productIds, house.ActiveStoreId, ct);

        return items.Select(i => new ShoppingListItemDto(
            i.Id,
            i.ProductId,
            i.Product.Name,
            i.ProductTypeId,
            ToTypeDto(i.ProductType),
            i.Format,
            activePrices.TryGetValue(i.ProductId, out var p) ? p : null,
            i.SortOrder)).ToList();
    }

    public async Task<ShoppingListItemDto> AddListItemAsync(Guid houseId, AddListItemRequest request, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);
        var now = timeProvider.GetUtcNow();

        var format = string.IsNullOrWhiteSpace(request.Format) ? "Ud" : request.Format.Trim();
        EnsureValidFormat(format);

        if (request.ProductTypeId.HasValue)
        {
            await EnsureValidProductTypeAsync(houseId, request.ProductTypeId.Value, ct);
        }

        Product product;
        if (request.ProductId.HasValue)
        {
            product = await db.Products
                .Include(p => p.ProductType)
                .FirstOrDefaultAsync(p => p.Id == request.ProductId.Value && p.HouseId == houseId, ct)
                ?? throw new ResourceNotFoundException("producto", request.ProductId.Value);

            product.LastUsedAt = now;
        }
        else
        {
            var rawName = request.Name?.Trim();
            if (string.IsNullOrWhiteSpace(rawName) || rawName.Length > 120)
            {
                throw new DomainValidationException("INVALID_PRODUCT_NAME", "El nombre del producto es obligatorio (máximo 120 caracteres).");
            }

            var normalized = DomainCatalogs.NormalizeProductName(rawName);
            product = await db.Products
                .Include(p => p.ProductType)
                .FirstOrDefaultAsync(p => p.HouseId == houseId && p.NormalizedName == normalized, ct)
                ?? new Product
                {
                    Id = Guid.NewGuid(),
                    HouseId = houseId,
                    Name = rawName,
                    NormalizedName = normalized,
                    ProductTypeId = request.ProductTypeId,
                    DefaultFormat = format,
                    LastUsedAt = now
                };

            if (db.Entry(product).State == EntityState.Detached)
            {
                db.Products.Add(product);
            }
            else
            {
                product.LastUsedAt = now;
            }
        }

        var maxOrder = await db.ShoppingListItems
            .Where(i => i.HouseId == houseId)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(ct) ?? -1;

        var effectiveTypeId = request.ProductTypeId ?? product.ProductTypeId;
        var effectiveFormat = string.IsNullOrWhiteSpace(request.Format) ? product.DefaultFormat : format;

        var item = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            ProductId = product.Id,
            ProductTypeId = effectiveTypeId,
            Format = effectiveFormat,
            SortOrder = maxOrder + 1,
            CreatedAt = now
        };

        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync(ct);
        await notifier.NotifyListUpdatedAsync(houseId, ct);

        var typeEntity = effectiveTypeId.HasValue
            ? await db.ProductTypes.FirstOrDefaultAsync(t => t.Id == effectiveTypeId.Value, ct)
            : null;
        var price = await GetActivePriceAsync(product.Id, house.ActiveStoreId, ct);

        return new ShoppingListItemDto(
            item.Id,
            product.Id,
            product.Name,
            item.ProductTypeId,
            ToTypeDto(typeEntity),
            item.Format,
            price,
            item.SortOrder);
    }

    public async Task<ShoppingListItemDto> UpdateListItemAsync(Guid houseId, Guid itemId, UpdateListItemRequest request, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);

        var item = await db.ShoppingListItems
            .Include(i => i.Product)
            .Include(i => i.ProductType)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("elemento de lista", itemId);

        var nameChanged = false;
        if (request.Name is not null)
        {
            var resolved = await ResolveOrRenameProductAsync(houseId, item.Product, request.Name, ct);
            nameChanged = true;
            if (resolved.Id != item.ProductId)
            {
                item.ProductId = resolved.Id;
                item.Product = resolved;
            }
        }

        if (request.ClearProductType)
        {
            item.ProductTypeId = null;
            item.Product.ProductTypeId = null;
        }
        else if (request.ProductTypeId.HasValue)
        {
            await EnsureValidProductTypeAsync(houseId, request.ProductTypeId.Value, ct);
            item.ProductTypeId = request.ProductTypeId;
            item.Product.ProductTypeId = request.ProductTypeId;
        }

        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            var fmt = request.Format.Trim();
            EnsureValidFormat(fmt);
            item.Format = fmt;
            item.Product.DefaultFormat = fmt;
        }

        await db.SaveChangesAsync(ct);
        await notifier.NotifyListUpdatedAsync(houseId, ct);
        if (nameChanged)
        {
            await notifier.NotifyBasketUpdatedAsync(houseId, ct);
        }

        var typeEntity = item.ProductTypeId.HasValue
            ? await db.ProductTypes.FirstOrDefaultAsync(t => t.Id == item.ProductTypeId.Value, ct)
            : null;
        var price = await GetActivePriceAsync(item.ProductId, house.ActiveStoreId, ct);

        return new ShoppingListItemDto(
            item.Id,
            item.ProductId,
            item.Product.Name,
            item.ProductTypeId,
            ToTypeDto(typeEntity),
            item.Format,
            price,
            item.SortOrder);
    }

    public async Task ReorderListAsync(Guid houseId, ReorderListRequest request, CancellationToken ct)
    {
        await houseService.RequireHouseMembershipAsync(houseId, ct);

        var items = await db.ShoppingListItems
            .Where(i => i.HouseId == houseId)
            .ToListAsync(ct);

        var map = items.ToDictionary(i => i.Id);
        var order = 0;
        foreach (var id in request.OrderedItemIds)
        {
            if (map.TryGetValue(id, out var item))
            {
                item.SortOrder = order++;
            }
        }

        foreach (var item in items.Where(i => !request.OrderedItemIds.Contains(i.Id)).OrderBy(i => i.SortOrder))
        {
            item.SortOrder = order++;
        }

        await db.SaveChangesAsync(ct);
        await notifier.NotifyListUpdatedAsync(houseId, ct);
    }

    public async Task<BasketItemDto> MoveListItemToBasketAsync(Guid houseId, Guid itemId, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);
        var now = timeProvider.GetUtcNow();

        var listItem = await db.ShoppingListItems
            .Include(i => i.Product)
            .Include(i => i.ProductType)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("elemento de lista", itemId);

        var basketItem = new BasketItem
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            ProductId = listItem.ProductId,
            ProductTypeId = listItem.ProductTypeId,
            Format = listItem.Format,
            Quantity = 1m,
            AddedAt = now
        };

        db.ShoppingListItems.Remove(listItem);
        db.BasketItems.Add(basketItem);
        await db.SaveChangesAsync(ct);

        await notifier.NotifyListUpdatedAsync(houseId, ct);
        await notifier.NotifyBasketUpdatedAsync(houseId, ct);

        var price = await GetActivePriceAsync(basketItem.ProductId, house.ActiveStoreId, ct);
        var lineTotal = price.HasValue ? Math.Round(price.Value * basketItem.Quantity, 2) : 0m;

        return new BasketItemDto(
            basketItem.Id,
            basketItem.ProductId,
            listItem.Product.Name,
            basketItem.ProductTypeId,
            ToTypeDto(listItem.ProductType),
            basketItem.Format,
            basketItem.Quantity,
            price,
            lineTotal);
    }

    public async Task DeleteListItemAsync(Guid houseId, Guid itemId, CancellationToken ct)
    {
        await houseService.RequireHouseMembershipAsync(houseId, ct);

        var listItem = await db.ShoppingListItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("elemento de lista", itemId);

        db.ShoppingListItems.Remove(listItem);
        await db.SaveChangesAsync(ct);
        await notifier.NotifyListUpdatedAsync(houseId, ct);
    }

    public async Task<BasketResponse> GetBasketAsync(Guid houseId, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);

        var items = await db.BasketItems
            .Include(i => i.Product)
            .Include(i => i.ProductType)
            .Where(i => i.HouseId == houseId)
            .OrderBy(i => i.AddedAt)
            .ToListAsync(ct);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var activePrices = await GetActivePricesMapAsync(productIds, house.ActiveStoreId, ct);

        var dtos = items.Select(i =>
        {
            decimal? price = activePrices.TryGetValue(i.ProductId, out var found) ? found : null;
            var lineTotal = price.HasValue ? Math.Round(price.Value * i.Quantity, 2) : 0m;
            return new BasketItemDto(
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.ProductTypeId,
                ToTypeDto(i.ProductType),
                i.Format,
                i.Quantity,
                price,
                lineTotal);
        }).ToList();

        var total = dtos.Sum(d => d.LineTotal);
        return new BasketResponse(house.ActiveStoreId, house.ActiveStore?.Name ?? "Default", dtos, total);
    }

    public async Task<BasketItemDto> UpdateBasketItemAsync(Guid houseId, Guid itemId, UpdateBasketItemRequest request, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);

        var item = await db.BasketItems
            .Include(i => i.Product)
            .Include(i => i.ProductType)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("elemento de cesta", itemId);

        var nameChanged = false;
        if (request.Name is not null)
        {
            var resolved = await ResolveOrRenameProductAsync(houseId, item.Product, request.Name, ct);
            nameChanged = true;
            if (resolved.Id != item.ProductId)
            {
                item.ProductId = resolved.Id;
                item.Product = resolved;
            }
        }

        if (request.Quantity.HasValue)
        {
            if (request.Quantity.Value <= 0)
            {
                throw new DomainValidationException("INVALID_QUANTITY", "La cantidad debe ser mayor que cero.");
            }
            item.Quantity = request.Quantity.Value;
        }

        if (request.ClearProductType)
        {
            item.ProductTypeId = null;
            item.Product.ProductTypeId = null;
        }
        else if (request.ProductTypeId.HasValue)
        {
            await EnsureValidProductTypeAsync(houseId, request.ProductTypeId.Value, ct);
            item.ProductTypeId = request.ProductTypeId;
            item.Product.ProductTypeId = request.ProductTypeId;
        }

        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            var fmt = request.Format.Trim();
            EnsureValidFormat(fmt);
            item.Format = fmt;
            item.Product.DefaultFormat = fmt;
        }

        await db.SaveChangesAsync(ct);
        await notifier.NotifyBasketUpdatedAsync(houseId, ct);
        if (nameChanged)
        {
            await notifier.NotifyListUpdatedAsync(houseId, ct);
        }

        var typeEntity = item.ProductTypeId.HasValue
            ? await db.ProductTypes.FirstOrDefaultAsync(t => t.Id == item.ProductTypeId.Value, ct)
            : null;
        var price = await GetActivePriceAsync(item.ProductId, house.ActiveStoreId, ct);
        var lineTotal = price.HasValue ? Math.Round(price.Value * item.Quantity, 2) : 0m;

        return new BasketItemDto(
            item.Id,
            item.ProductId,
            item.Product.Name,
            item.ProductTypeId,
            ToTypeDto(typeEntity),
            item.Format,
            item.Quantity,
            price,
            lineTotal);
    }

    public async Task<PriceHistoryEntryDto> SetBasketItemPriceAsync(Guid houseId, Guid itemId, SetPriceRequest request, CancellationToken ct)
    {
        if (request.Price < 0)
        {
            throw new DomainValidationException("INVALID_PRICE", "El precio no puede ser negativo.");
        }

        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);
        var item = await db.BasketItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("elemento de cesta", itemId);

        var roundedPrice = Math.Round(request.Price, 2);
        var result = await UpsertScd2PriceAsync(item.ProductId, house.ActiveStoreId, roundedPrice, ct);
        await notifier.NotifyBasketUpdatedAsync(houseId, ct);

        return result;
    }

    public async Task<PriceHistoryEntryDto> UpsertScd2PriceAsync(Guid productId, Guid storeId, decimal newPrice, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var active = await db.ProductPriceHistories
            .Where(h => h.ProductId == productId && h.StoreId == storeId && h.ValidTo == null)
            .FirstOrDefaultAsync(ct);

        if (active is not null && active.Price == newPrice)
        {
            await tx.CommitAsync(ct);
            return new PriceHistoryEntryDto(active.ProductId, active.StoreId, active.Price, active.ValidFrom, active.ValidTo);
        }

        var now = timeProvider.GetUtcNow();
        if (active is not null)
        {
            if (now <= active.ValidFrom)
            {
                now = active.ValidFrom.AddTicks(10);
            }
            active.ValidTo = now;
            await db.SaveChangesAsync(ct);
        }

        var next = new ProductPriceHistory
        {
            ProductId = productId,
            StoreId = storeId,
            ValidFrom = now,
            ValidTo = null,
            Price = newPrice
        };

        db.ProductPriceHistories.Add(next);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new PriceHistoryEntryDto(next.ProductId, next.StoreId, next.Price, next.ValidFrom, next.ValidTo);
    }

    public async Task<ShoppingListItemDto> MoveBasketItemToListAsync(Guid houseId, Guid itemId, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);
        var now = timeProvider.GetUtcNow();

        var basketItem = await db.BasketItems
            .Include(i => i.Product)
            .Include(i => i.ProductType)
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("elemento de cesta", itemId);

        var maxOrder = await db.ShoppingListItems
            .Where(i => i.HouseId == houseId)
            .Select(i => (int?)i.SortOrder)
            .MaxAsync(ct) ?? -1;

        var listItem = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            ProductId = basketItem.ProductId,
            ProductTypeId = basketItem.ProductTypeId,
            Format = basketItem.Format,
            SortOrder = maxOrder + 1,
            CreatedAt = now
        };

        db.BasketItems.Remove(basketItem);
        db.ShoppingListItems.Add(listItem);
        await db.SaveChangesAsync(ct);

        await notifier.NotifyBasketUpdatedAsync(houseId, ct);
        await notifier.NotifyListUpdatedAsync(houseId, ct);

        var price = await GetActivePriceAsync(listItem.ProductId, house.ActiveStoreId, ct);

        return new ShoppingListItemDto(
            listItem.Id,
            listItem.ProductId,
            basketItem.Product.Name,
            listItem.ProductTypeId,
            ToTypeDto(basketItem.ProductType),
            listItem.Format,
            price,
            listItem.SortOrder);
    }

    public async Task DeleteBasketItemAsync(Guid houseId, Guid itemId, CancellationToken ct)
    {
        await houseService.RequireHouseMembershipAsync(houseId, ct);

        var item = await db.BasketItems
            .FirstOrDefaultAsync(i => i.Id == itemId && i.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("elemento de cesta", itemId);

        db.BasketItems.Remove(item);
        await db.SaveChangesAsync(ct);
        await notifier.NotifyBasketUpdatedAsync(houseId, ct);
    }

    public async Task<PurchaseDetailDto> CheckoutBasketAsync(Guid houseId, CancellationToken ct)
    {
        var house = await houseService.RequireHouseMembershipAsync(houseId, ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var basketItems = await db.BasketItems
            .Include(i => i.Product)
            .Where(i => i.HouseId == houseId)
            .ToListAsync(ct);

        if (basketItems.Count == 0)
        {
            throw new DomainValidationException("EMPTY_BASKET", "No puedes efectuar la compra con la cesta vacía.");
        }

        var productIds = basketItems.Select(i => i.ProductId).Distinct().ToList();
        var activePrices = await GetActivePricesMapAsync(productIds, house.ActiveStoreId, ct);

        var now = timeProvider.GetUtcNow();
        var purchase = new Purchase
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            StoreId = house.ActiveStoreId,
            PurchasedAt = now
        };

        decimal total = 0m;
        foreach (var b in basketItems)
        {
            var unitPrice = activePrices.TryGetValue(b.ProductId, out var p) ? p : 0m;
            var lineTotal = Math.Round(unitPrice * b.Quantity, 2);
            total += lineTotal;

            purchase.Items.Add(new PurchaseItem
            {
                Id = Guid.NewGuid(),
                PurchaseId = purchase.Id,
                ProductId = b.ProductId,
                ProductNameSnapshot = b.Product.Name,
                ProductTypeId = b.ProductTypeId,
                Format = b.Format,
                Quantity = b.Quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal
            });
        }

        purchase.CalculatedTotal = total;

        db.Purchases.Add(purchase);
        db.BasketItems.RemoveRange(basketItems);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await notifier.NotifyBasketUpdatedAsync(houseId, ct);
        await notifier.NotifyPurchaseCompletedAsync(houseId, purchase.Id, ct);

        return new PurchaseDetailDto(
            purchase.Id,
            purchase.StoreId,
            house.ActiveStore?.Name ?? "Default",
            purchase.PurchasedAt,
            purchase.CalculatedTotal,
            purchase.ManualTotal,
            purchase.ManualTotal ?? purchase.CalculatedTotal,
            false,
            purchase.Items.Select(pi => new PurchaseItemDto(
                pi.Id,
                pi.ProductId,
                pi.ProductNameSnapshot,
                pi.ProductTypeId,
                pi.Format,
                pi.Quantity,
                pi.UnitPrice,
                pi.LineTotal)).ToList());
    }

    public async Task<IReadOnlyList<PurchaseSummaryDto>> GetPurchasesAsync(Guid houseId, CancellationToken ct)
    {
        await houseService.RequireHouseMembershipAsync(houseId, ct);

        return await db.Purchases
            .Include(p => p.Store)
            .Include(p => p.Items)
            .Where(p => p.HouseId == houseId)
            .OrderByDescending(p => p.PurchasedAt)
            .Select(p => new PurchaseSummaryDto(
                p.Id,
                p.StoreId,
                p.Store.Name,
                p.PurchasedAt,
                p.CalculatedTotal,
                p.ManualTotal,
                p.ManualTotal ?? p.CalculatedTotal,
                p.Items.Count,
                p.ReceiptImageUrl != null))
            .ToListAsync(ct);
    }

    public async Task<PurchaseDetailDto> GetPurchaseDetailAsync(Guid houseId, Guid purchaseId, CancellationToken ct)
    {
        await houseService.RequireHouseMembershipAsync(houseId, ct);

        var purchase = await db.Purchases
            .Include(p => p.Store)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == purchaseId && p.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("compra", purchaseId);

        return new PurchaseDetailDto(
            purchase.Id,
            purchase.StoreId,
            purchase.Store.Name,
            purchase.PurchasedAt,
            purchase.CalculatedTotal,
            purchase.ManualTotal,
            purchase.ManualTotal ?? purchase.CalculatedTotal,
            purchase.ReceiptImageUrl != null,
            purchase.Items.Select(pi => new PurchaseItemDto(
                pi.Id,
                pi.ProductId,
                pi.ProductNameSnapshot,
                pi.ProductTypeId,
                pi.Format,
                pi.Quantity,
                pi.UnitPrice,
                pi.LineTotal)).ToList());
    }

    public async Task<PurchaseSummaryDto> UpdateManualTotalAsync(Guid houseId, Guid purchaseId, UpdateManualTotalRequest request, CancellationToken ct)
    {
        if (request.ManualTotal < 0)
        {
            throw new DomainValidationException("INVALID_TOTAL", "El total de la compra no puede ser negativo.");
        }

        await houseService.RequireHouseMembershipAsync(houseId, ct);

        var purchase = await db.Purchases
            .Include(p => p.Store)
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == purchaseId && p.HouseId == houseId, ct)
            ?? throw new ResourceNotFoundException("compra", purchaseId);

        purchase.ManualTotal = Math.Round(request.ManualTotal, 2);
        await db.SaveChangesAsync(ct);

        return new PurchaseSummaryDto(
            purchase.Id,
            purchase.StoreId,
            purchase.Store.Name,
            purchase.PurchasedAt,
            purchase.CalculatedTotal,
            purchase.ManualTotal,
            purchase.ManualTotal ?? purchase.CalculatedTotal,
            purchase.Items.Count,
            purchase.ReceiptImageUrl != null);
    }

    public async Task AttachReceiptAsync(Guid houseId, Guid purchaseId, CancellationToken ct)
    {
        await houseService.RequireHouseMembershipAsync(houseId, ct);

        if (!callerAccessor.Current.IsPremium)
        {
            throw new PremiumRequiredException();
        }

        var exists = await db.Purchases.AnyAsync(p => p.Id == purchaseId && p.HouseId == houseId, ct);
        if (!exists)
        {
            throw new ResourceNotFoundException("compra", purchaseId);
        }
    }

    private async Task<Dictionary<Guid, decimal>> GetActivePricesMapAsync(
        IReadOnlyCollection<Guid> productIds,
        Guid storeId,
        CancellationToken ct)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        return await db.ProductPriceHistories
            .Where(h => productIds.Contains(h.ProductId) && h.StoreId == storeId && h.ValidTo == null)
            .ToDictionaryAsync(h => h.ProductId, h => h.Price, ct);
    }

    private async Task<decimal?> GetActivePriceAsync(Guid productId, Guid storeId, CancellationToken ct) =>
        await db.ProductPriceHistories
            .Where(h => h.ProductId == productId && h.StoreId == storeId && h.ValidTo == null)
            .Select(h => (decimal?)h.Price)
            .FirstOrDefaultAsync(ct);

    private async Task<Product> ResolveOrRenameProductAsync(
        Guid houseId,
        Product currentProduct,
        string rawName,
        CancellationToken ct)
    {
        var trimmed = rawName.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > 120)
        {
            throw new DomainValidationException("INVALID_PRODUCT_NAME", "El nombre del producto debe tener entre 1 y 120 caracteres.");
        }

        var normalized = DomainCatalogs.NormalizeProductName(trimmed);
        if (normalized == currentProduct.NormalizedName)
        {
            currentProduct.Name = trimmed;
            currentProduct.LastUsedAt = timeProvider.GetUtcNow();
            return currentProduct;
        }

        var existing = await db.Products
            .FirstOrDefaultAsync(p => p.HouseId == houseId && p.NormalizedName == normalized && p.Id != currentProduct.Id, ct);

        if (existing is not null)
        {
            existing.Name = trimmed;
            existing.LastUsedAt = timeProvider.GetUtcNow();
            return existing;
        }

        currentProduct.Name = trimmed;
        currentProduct.NormalizedName = normalized;
        currentProduct.LastUsedAt = timeProvider.GetUtcNow();
        return currentProduct;
    }

    private async Task EnsureValidProductTypeAsync(Guid houseId, Guid productTypeId, CancellationToken ct)
    {
        var exists = await db.ProductTypes.AnyAsync(t =>
            t.Id == productTypeId && (t.HouseId == null || t.HouseId == houseId), ct);
        if (!exists)
        {
            throw new ResourceNotFoundException("tipo de producto", productTypeId);
        }
    }

    private static void EnsureValidFormat(string format)
    {
        if (!DomainCatalogs.AllowedFormatsSet.Contains(format))
        {
            throw new DomainValidationException(
                "INVALID_FORMAT",
                $"Formato '{format}' no permitido. Permitidos: {string.Join(", ", DomainCatalogs.AllowedFormats)}.");
        }
    }

    private static ProductTypeDto? ToTypeDto(ProductType? type) =>
        type is null ? null : new ProductTypeDto(type.Id, type.Name, type.FillHex, type.BorderHex, type.IsSystem);
}
