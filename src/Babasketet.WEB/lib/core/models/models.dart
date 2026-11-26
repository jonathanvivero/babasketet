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

class MeHouseRoleDto {
  final String houseId;
  final String houseName;
  final String houseRole;

  const MeHouseRoleDto({
    required this.houseId,
    required this.houseName,
    required this.houseRole,
  });

  factory MeHouseRoleDto.fromJson(Map<String, dynamic> json) => MeHouseRoleDto(
        houseId: json['houseId'] as String,
        houseName: json['houseName'] as String,
        houseRole: json['houseRole'] as String,
      );
}

class MeResponse {
  final String? appUserId;
  final String? zitadelUserId;
  final String? deviceId;
  final String globalRole;
  final bool isAuthenticated;
  final bool isAdmin;
  final String status;
  final String? email;
  final String? displayName;
  final List<String> effectiveRoles;
  final List<MeHouseRoleDto> houses;

  const MeResponse({
    this.appUserId,
    this.zitadelUserId,
    this.deviceId,
    required this.globalRole,
    required this.isAuthenticated,
    required this.isAdmin,
    required this.status,
    this.email,
    this.displayName,
    required this.effectiveRoles,
    required this.houses,
  });

  factory MeResponse.fromJson(Map<String, dynamic> json) => MeResponse(
        appUserId: json['appUserId'] as String?,
        zitadelUserId: json['zitadelUserId'] as String?,
        deviceId: json['deviceId'] as String?,
        globalRole: (json['globalRole'] as String?) ?? 'Guest',
        isAuthenticated: (json['isAuthenticated'] as bool?) ?? false,
        isAdmin: (json['isAdmin'] as bool?) ?? false,
        status: (json['status'] as String?) ?? 'Active',
        email: json['email'] as String?,
        displayName: json['displayName'] as String?,
        effectiveRoles: ((json['effectiveRoles'] as List<dynamic>?) ?? [])
            .map((e) => e.toString())
            .toList(),
        houses: ((json['houses'] as List<dynamic>?) ?? [])
            .map((e) => MeHouseRoleDto.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class AdminHouseSummaryDto {
  final String id;
  final String name;
  final String activeStoreName;
  final int memberCount;
  final int totalProductsCount;
  final int listItemsCount;
  final int basketItemsCount;
  final int purchasesCount;
  final DateTime createdAt;

  const AdminHouseSummaryDto({
    required this.id,
    required this.name,
    required this.activeStoreName,
    required this.memberCount,
    required this.totalProductsCount,
    required this.listItemsCount,
    required this.basketItemsCount,
    required this.purchasesCount,
    required this.createdAt,
  });

  factory AdminHouseSummaryDto.fromJson(Map<String, dynamic> json) =>
      AdminHouseSummaryDto(
        id: json['id'] as String,
        name: json['name'] as String,
        activeStoreName: (json['activeStoreName'] as String?) ?? 'Default',
        memberCount: json['memberCount'] as int,
        totalProductsCount: json['totalProductsCount'] as int,
        listItemsCount: json['listItemsCount'] as int,
        basketItemsCount: json['basketItemsCount'] as int,
        purchasesCount: json['purchasesCount'] as int,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

class AdminHouseMemberDto {
  final String membershipId;
  final String? appUserId;
  final String? zitadelUserId;
  final String? deviceId;
  final String displayName;
  final String? email;
  final String globalRole;
  final bool isAuthenticated;
  final bool isAdmin;
  final String userStatus;
  final String houseRole;
  final DateTime joinedAt;

  const AdminHouseMemberDto({
    required this.membershipId,
    this.appUserId,
    this.zitadelUserId,
    this.deviceId,
    required this.displayName,
    this.email,
    required this.globalRole,
    required this.isAuthenticated,
    required this.isAdmin,
    required this.userStatus,
    required this.houseRole,
    required this.joinedAt,
  });

  factory AdminHouseMemberDto.fromJson(Map<String, dynamic> json) =>
      AdminHouseMemberDto(
        membershipId: json['membershipId'] as String,
        appUserId: json['appUserId'] as String?,
        zitadelUserId: json['zitadelUserId'] as String?,
        deviceId: json['deviceId'] as String?,
        displayName: json['displayName'] as String,
        email: json['email'] as String?,
        globalRole: json['globalRole'] as String,
        isAuthenticated: json['isAuthenticated'] as bool,
        isAdmin: json['isAdmin'] as bool,
        userStatus: json['userStatus'] as String,
        houseRole: json['houseRole'] as String,
        joinedAt: DateTime.parse(json['joinedAt'] as String),
      );
}

class AdminSimpleItemDto {
  final String id;
  final String productId;
  final String name;
  final String? productTypeName;
  final String? productTypeFillHex;
  final String format;
  final double? quantity;

  const AdminSimpleItemDto({
    required this.id,
    required this.productId,
    required this.name,
    this.productTypeName,
    this.productTypeFillHex,
    required this.format,
    this.quantity,
  });

  factory AdminSimpleItemDto.fromJson(Map<String, dynamic> json) =>
      AdminSimpleItemDto(
        id: json['id'] as String,
        productId: json['productId'] as String,
        name: json['name'] as String,
        productTypeName: json['productTypeName'] as String?,
        productTypeFillHex: json['productTypeFillHex'] as String?,
        format: (json['format'] as String?) ?? 'Ud',
        quantity: (json['quantity'] as num?)?.toDouble(),
      );
}

class AdminHouseDetailDto {
  final String id;
  final String name;
  final String activeStoreName;
  final DateTime createdAt;
  final int totalProductsCount;
  final List<AdminHouseMemberDto> members;
  final List<AdminSimpleItemDto> listItems;
  final List<AdminSimpleItemDto> basketItems;
  final List<PurchaseDetailDto> purchases;

  const AdminHouseDetailDto({
    required this.id,
    required this.name,
    required this.activeStoreName,
    required this.createdAt,
    required this.totalProductsCount,
    required this.members,
    required this.listItems,
    required this.basketItems,
    required this.purchases,
  });

  factory AdminHouseDetailDto.fromJson(Map<String, dynamic> json) =>
      AdminHouseDetailDto(
        id: json['id'] as String,
        name: json['name'] as String,
        activeStoreName: (json['activeStoreName'] as String?) ?? 'Default',
        createdAt: DateTime.parse(json['createdAt'] as String),
        totalProductsCount: json['totalProductsCount'] as int,
        members: (json['members'] as List<dynamic>)
            .map((e) => AdminHouseMemberDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        listItems: (json['listItems'] as List<dynamic>)
            .map((e) => AdminSimpleItemDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        basketItems: (json['basketItems'] as List<dynamic>)
            .map((e) => AdminSimpleItemDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        purchases: (json['purchases'] as List<dynamic>)
            .map((e) => PurchaseDetailDto.fromJson(e as Map<String, dynamic>))
            .toList(),
      );
}

class AdminUserHouseMembershipDto {
  final String houseId;
  final String houseName;
  final String houseRole;
  final DateTime joinedAt;

  const AdminUserHouseMembershipDto({
    required this.houseId,
    required this.houseName,
    required this.houseRole,
    required this.joinedAt,
  });

  factory AdminUserHouseMembershipDto.fromJson(Map<String, dynamic> json) =>
      AdminUserHouseMembershipDto(
        houseId: json['houseId'] as String,
        houseName: json['houseName'] as String,
        houseRole: json['houseRole'] as String,
        joinedAt: DateTime.parse(json['joinedAt'] as String),
      );
}

class AdminUserSummaryDto {
  final String id;
  final String? zitadelUserId;
  final String? deviceId;
  final String displayName;
  final String? email;
  final String status;
  final String globalRole;
  final bool isAdmin;
  final bool isAuthenticated;
  final List<String> effectiveRoles;
  final int housesCount;
  final int ownedHousesCount;
  final DateTime createdAt;
  final DateTime lastSeenAt;

  const AdminUserSummaryDto({
    required this.id,
    this.zitadelUserId,
    this.deviceId,
    required this.displayName,
    this.email,
    required this.status,
    required this.globalRole,
    required this.isAdmin,
    required this.isAuthenticated,
    required this.effectiveRoles,
    required this.housesCount,
    required this.ownedHousesCount,
    required this.createdAt,
    required this.lastSeenAt,
  });

  factory AdminUserSummaryDto.fromJson(Map<String, dynamic> json) =>
      AdminUserSummaryDto(
        id: json['id'] as String,
        zitadelUserId: json['zitadelUserId'] as String?,
        deviceId: json['deviceId'] as String?,
        displayName: json['displayName'] as String,
        email: json['email'] as String?,
        status: json['status'] as String,
        globalRole: json['globalRole'] as String,
        isAdmin: json['isAdmin'] as bool,
        isAuthenticated: json['isAuthenticated'] as bool,
        effectiveRoles: ((json['effectiveRoles'] as List<dynamic>?) ?? [])
            .map((e) => e.toString())
            .toList(),
        housesCount: json['housesCount'] as int,
        ownedHousesCount: json['ownedHousesCount'] as int,
        createdAt: DateTime.parse(json['createdAt'] as String),
        lastSeenAt: DateTime.parse(json['lastSeenAt'] as String),
      );
}

class AdminUserDetailDto {
  final String id;
  final String? zitadelUserId;
  final String? deviceId;
  final String displayName;
  final String? email;
  final String status;
  final String globalRole;
  final bool isAdmin;
  final bool isAuthenticated;
  final String authMethod;
  final List<String> effectiveRoles;
  final Map<String, String> customProperties;
  final List<AdminUserHouseMembershipDto> houses;
  final DateTime createdAt;
  final DateTime lastSeenAt;

  const AdminUserDetailDto({
    required this.id,
    this.zitadelUserId,
    this.deviceId,
    required this.displayName,
    this.email,
    required this.status,
    required this.globalRole,
    required this.isAdmin,
    required this.isAuthenticated,
    required this.authMethod,
    required this.effectiveRoles,
    required this.customProperties,
    required this.houses,
    required this.createdAt,
    required this.lastSeenAt,
  });

  factory AdminUserDetailDto.fromJson(Map<String, dynamic> json) =>
      AdminUserDetailDto(
        id: json['id'] as String,
        zitadelUserId: json['zitadelUserId'] as String?,
        deviceId: json['deviceId'] as String?,
        displayName: json['displayName'] as String,
        email: json['email'] as String?,
        status: json['status'] as String,
        globalRole: json['globalRole'] as String,
        isAdmin: json['isAdmin'] as bool,
        isAuthenticated: json['isAuthenticated'] as bool,
        authMethod: (json['authMethod'] as String?) ?? 'anonymous',
        effectiveRoles: ((json['effectiveRoles'] as List<dynamic>?) ?? [])
            .map((e) => e.toString())
            .toList(),
        customProperties:
            ((json['customProperties'] as Map<String, dynamic>?) ?? {})
                .map((k, v) => MapEntry(k, v.toString())),
        houses: (json['houses'] as List<dynamic>)
            .map((e) =>
                AdminUserHouseMembershipDto.fromJson(e as Map<String, dynamic>))
            .toList(),
        createdAt: DateTime.parse(json['createdAt'] as String),
        lastSeenAt: DateTime.parse(json['lastSeenAt'] as String),
      );
}

class AdminCatalogItemDto {
  final String normalizedName;
  final String name;
  final String? productTypeId;
  final String productTypeName;
  final String? productTypeFillHex;
  final int houseCount;
  final String? singleHouseId;
  final String locationLabel;

  const AdminCatalogItemDto({
    required this.normalizedName,
    required this.name,
    this.productTypeId,
    required this.productTypeName,
    this.productTypeFillHex,
    required this.houseCount,
    this.singleHouseId,
    required this.locationLabel,
  });

  factory AdminCatalogItemDto.fromJson(Map<String, dynamic> json) =>
      AdminCatalogItemDto(
        normalizedName: json['normalizedName'] as String,
        name: json['name'] as String,
        productTypeId: json['productTypeId'] as String?,
        productTypeName: (json['productTypeName'] as String?) ?? 'Sin tipo',
        productTypeFillHex: json['productTypeFillHex'] as String?,
        houseCount: json['houseCount'] as int,
        singleHouseId: json['singleHouseId'] as String?,
        locationLabel: json['locationLabel'] as String,
      );
}

class EmailConfigDto {
  final bool isConfigured;
  final String host;
  final int port;
  final String? username;
  final bool hasPassword;
  final String securityMode;
  final String fromEmail;
  final String fromName;
  final bool enabled;
  final DateTime? updatedAt;

  const EmailConfigDto({
    required this.isConfigured,
    required this.host,
    required this.port,
    this.username,
    required this.hasPassword,
    required this.securityMode,
    required this.fromEmail,
    required this.fromName,
    required this.enabled,
    this.updatedAt,
  });

  factory EmailConfigDto.fromJson(Map<String, dynamic> json) => EmailConfigDto(
        isConfigured: (json['isConfigured'] as bool?) ?? false,
        host: (json['host'] as String?) ?? '',
        port: (json['port'] as int?) ?? 587,
        username: json['username'] as String?,
        hasPassword: (json['hasPassword'] as bool?) ?? false,
        securityMode: (json['securityMode'] as String?) ?? 'StartTls',
        fromEmail: (json['fromEmail'] as String?) ?? '',
        fromName: (json['fromName'] as String?) ?? 'Babasketet',
        enabled: (json['enabled'] as bool?) ?? true,
        updatedAt: json['updatedAt'] != null
            ? DateTime.parse(json['updatedAt'] as String)
            : null,
      );
}

class TestEmailResultDto {
  final bool success;
  final String recipient;
  final String message;
  final DateTime sentAt;

  const TestEmailResultDto({
    required this.success,
    required this.recipient,
    required this.message,
    required this.sentAt,
  });

  factory TestEmailResultDto.fromJson(Map<String, dynamic> json) =>
      TestEmailResultDto(
        success: (json['success'] as bool?) ?? false,
        recipient: (json['recipient'] as String?) ?? '',
        message: (json['message'] as String?) ?? '',
        sentAt: DateTime.parse(json['sentAt'] as String),
      );
}
