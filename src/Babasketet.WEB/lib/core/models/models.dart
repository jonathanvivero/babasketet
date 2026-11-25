class HouseDto {
  final String id;
  final String name;
  final String activeStoreId;
  final String activeStoreName;

  const HouseDto({
    required this.id,
    required this.name,
    required this.activeStoreId,
    required this.activeStoreName,
  });

  factory HouseDto.fromJson(Map<String, dynamic> json) => HouseDto(
        id: json['id'] as String,
        name: json['name'] as String,
        activeStoreId: json['activeStoreId'] as String,
        activeStoreName: (json['activeStoreName'] as String?) ?? 'Default',
      );
}

class HousesResponse {
  final List<HouseDto> houses;
  final int maxHouses;
  final bool isRegistered;

  const HousesResponse({
    required this.houses,
    required this.maxHouses,
    required this.isRegistered,
  });

  factory HousesResponse.fromJson(Map<String, dynamic> json) => HousesResponse(
        houses: (json['houses'] as List<dynamic>)
            .map((e) => HouseDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        maxHouses: json['maxHouses'] as int,
        isRegistered: json['isRegistered'] as bool,
      );
}

class StoreDto {
  final String id;
  final String name;
  final String iconKey;
  final bool isDefault;
  final bool isSystem;

  const StoreDto({
    required this.id,
    required this.name,
    required this.iconKey,
    required this.isDefault,
    required this.isSystem,
  });

  factory StoreDto.fromJson(Map<String, dynamic> json) => StoreDto(
        id: json['id'] as String,
        name: json['name'] as String,
        iconKey: json['iconKey'] as String,
        isDefault: json['isDefault'] as bool,
        isSystem: json['isSystem'] as bool,
      );
}

class ProductTypeDto {
  final String id;
  final String name;
  final String fillHex;
  final String? borderHex;
  final bool isSystem;

  const ProductTypeDto({
    required this.id,
    required this.name,
    required this.fillHex,
    this.borderHex,
    required this.isSystem,
  });

  factory ProductTypeDto.fromJson(Map<String, dynamic> json) => ProductTypeDto(
        id: json['id'] as String,
        name: json['name'] as String,
        fillHex: json['fillHex'] as String,
        borderHex: json['borderHex'] as String?,
        isSystem: json['isSystem'] as bool,
      );
}

class ProductSuggestionDto {
  final String id;
  final String name;
  final String? productTypeId;
  final ProductTypeDto? productType;
  final String defaultFormat;
  final double? currentPrice;

  const ProductSuggestionDto({
    required this.id,
    required this.name,
    this.productTypeId,
    this.productType,
    required this.defaultFormat,
    this.currentPrice,
  });

  factory ProductSuggestionDto.fromJson(Map<String, dynamic> json) =>
      ProductSuggestionDto(
        id: json['id'] as String,
        name: json['name'] as String,
        productTypeId: json['productTypeId'] as String?,
        productType: json['productType'] != null
            ? ProductTypeDto.fromJson(
                json['productType'] as Map<String, dynamic>)
            : null,
        defaultFormat: (json['defaultFormat'] as String?) ?? 'Ud',
        currentPrice: (json['currentPrice'] as num?)?.toDouble(),
      );
}

class ShoppingListItemDto {
  final String id;
  final String productId;
  final String name;
  final String? productTypeId;
  final ProductTypeDto? productType;
  final String format;
  final double? currentPrice;
  final int sortOrder;

  const ShoppingListItemDto({
    required this.id,
    required this.productId,
    required this.name,
    this.productTypeId,
    this.productType,
    required this.format,
    this.currentPrice,
    required this.sortOrder,
  });

  factory ShoppingListItemDto.fromJson(Map<String, dynamic> json) =>
      ShoppingListItemDto(
        id: json['id'] as String,
        productId: json['productId'] as String,
        name: json['name'] as String,
        productTypeId: json['productTypeId'] as String?,
        productType: json['productType'] != null
            ? ProductTypeDto.fromJson(
                json['productType'] as Map<String, dynamic>)
            : null,
        format: (json['format'] as String?) ?? 'Ud',
        currentPrice: (json['currentPrice'] as num?)?.toDouble(),
        sortOrder: json['sortOrder'] as int,
      );
}

class BasketItemDto {
  final String id;
  final String productId;
  final String name;
  final String? productTypeId;
  final ProductTypeDto? productType;
  final String format;
  final double quantity;
  final double? currentPrice;
  final double lineTotal;

  const BasketItemDto({
    required this.id,
    required this.productId,
    required this.name,
    this.productTypeId,
    this.productType,
    required this.format,
    required this.quantity,
    this.currentPrice,
    required this.lineTotal,
  });

  factory BasketItemDto.fromJson(Map<String, dynamic> json) => BasketItemDto(
        id: json['id'] as String,
        productId: json['productId'] as String,
        name: json['name'] as String,
        productTypeId: json['productTypeId'] as String?,
        productType: json['productType'] != null
            ? ProductTypeDto.fromJson(
                json['productType'] as Map<String, dynamic>)
            : null,
        format: (json['format'] as String?) ?? 'Ud',
        quantity: (json['quantity'] as num).toDouble(),
        currentPrice: (json['currentPrice'] as num?)?.toDouble(),
        lineTotal: (json['lineTotal'] as num).toDouble(),
      );
}

class BasketResponse {
  final String activeStoreId;
  final String activeStoreName;
  final List<BasketItemDto> items;
  final double calculatedTotal;

  const BasketResponse({
    required this.activeStoreId,
    required this.activeStoreName,
    required this.items,
    required this.calculatedTotal,
  });

  factory BasketResponse.fromJson(Map<String, dynamic> json) => BasketResponse(
        activeStoreId: json['activeStoreId'] as String,
        activeStoreName: (json['activeStoreName'] as String?) ?? 'Default',
        items: (json['items'] as List<dynamic>)
            .map((e) => BasketItemDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        calculatedTotal: (json['calculatedTotal'] as num).toDouble(),
      );
}

class InvitationResponse {
  final String token;
  final String path;
  final DateTime expiresAt;

  const InvitationResponse({
    required this.token,
    required this.path,
    required this.expiresAt,
  });

  factory InvitationResponse.fromJson(Map<String, dynamic> json) =>
      InvitationResponse(
        token: json['token'] as String,
        path: json['path'] as String,
        expiresAt: DateTime.parse(json['expiresAt'] as String),
      );
}

class PurchaseSummaryDto {
  final String id;
  final String storeId;
  final String storeName;
  final DateTime purchasedAt;
  final double calculatedTotal;
  final double? manualTotal;
  final double effectiveTotal;
  final int itemCount;
  final bool hasReceipt;

  const PurchaseSummaryDto({
    required this.id,
    required this.storeId,
    required this.storeName,
    required this.purchasedAt,
    required this.calculatedTotal,
    this.manualTotal,
    required this.effectiveTotal,
    required this.itemCount,
    required this.hasReceipt,
  });

  factory PurchaseSummaryDto.fromJson(Map<String, dynamic> json) =>
      PurchaseSummaryDto(
        id: json['id'] as String,
        storeId: json['storeId'] as String,
        storeName: json['storeName'] as String,
        purchasedAt: DateTime.parse(json['purchasedAt'] as String),
        calculatedTotal: (json['calculatedTotal'] as num).toDouble(),
        manualTotal: (json['manualTotal'] as num?)?.toDouble(),
        effectiveTotal: (json['effectiveTotal'] as num).toDouble(),
        itemCount: json['itemCount'] as int,
        hasReceipt: json['hasReceipt'] as bool,
      );
}

class PurchaseItemDto {
  final String id;
  final String productId;
  final String productName;
  final String? productTypeId;
  final String format;
  final double quantity;
  final double unitPrice;
  final double lineTotal;

  const PurchaseItemDto({
    required this.id,
    required this.productId,
    required this.productName,
    this.productTypeId,
    required this.format,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });

  factory PurchaseItemDto.fromJson(Map<String, dynamic> json) =>
      PurchaseItemDto(
        id: json['id'] as String,
        productId: json['productId'] as String,
        productName: json['productName'] as String,
        productTypeId: json['productTypeId'] as String?,
        format: (json['format'] as String?) ?? 'Ud',
        quantity: (json['quantity'] as num).toDouble(),
        unitPrice: (json['unitPrice'] as num).toDouble(),
        lineTotal: (json['lineTotal'] as num).toDouble(),
      );
}

class PurchaseDetailDto {
  final String id;
  final String storeId;
  final String storeName;
  final DateTime purchasedAt;
  final double calculatedTotal;
  final double? manualTotal;
  final double effectiveTotal;
  final bool hasReceipt;
  final List<PurchaseItemDto> items;

  const PurchaseDetailDto({
    required this.id,
    required this.storeId,
    required this.storeName,
    required this.purchasedAt,
    required this.calculatedTotal,
    this.manualTotal,
    required this.effectiveTotal,
    required this.hasReceipt,
    required this.items,
  });

  factory PurchaseDetailDto.fromJson(Map<String, dynamic> json) =>
      PurchaseDetailDto(
        id: json['id'] as String,
        storeId: json['storeId'] as String,
        storeName: json['storeName'] as String,
        purchasedAt: DateTime.parse(json['purchasedAt'] as String),
        calculatedTotal: (json['calculatedTotal'] as num).toDouble(),
        manualTotal: (json['manualTotal'] as num?)?.toDouble(),
        effectiveTotal: (json['effectiveTotal'] as num).toDouble(),
        hasReceipt: json['hasReceipt'] as bool,
        items: (json['items'] as List<dynamic>)
            .map((e) => PurchaseItemDto.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}
