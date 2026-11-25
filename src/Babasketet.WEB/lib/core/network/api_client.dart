import 'package:dio/dio.dart';

import '../config/app_config.dart';
import '../models/models.dart';
import '../storage/local_storage.dart';

class ApiException implements Exception {
  final int statusCode;
  final String code;
  final String message;

  const ApiException({
    required this.statusCode,
    required this.code,
    required this.message,
  });

  @override
  String toString() => message;
}

class BabasketetApiClient {
  final Dio _dio;
  final LocalStorageService _storage;

  BabasketetApiClient({
    required LocalStorageService storage,
    Dio? dio,
  })  : _storage = storage,
        _dio = dio ??
            Dio(BaseOptions(
              baseUrl: AppConfig.apiBaseUrl,
              connectTimeout: const Duration(seconds: 10),
              receiveTimeout: const Duration(seconds: 15),
            )) {
    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: (options, handler) async {
        final token = await _storage.getEffectiveBearerToken();
        if (token != null && token.isNotEmpty) {
          options.headers['Authorization'] = 'Bearer $token';
        }
        handler.next(options);
      },
    ));
  }

  Future<T> _wrap<T>(Future<T> Function() fn) async {
    try {
      return await fn();
    } on DioException catch (e) {
      final status = e.response?.statusCode ?? 500;
      final data = e.response?.data;
      if (data is Map<String, dynamic>) {
        throw ApiException(
          statusCode: status,
          code: (data['code'] as String?) ??
              (data['title'] as String?) ??
              'API_ERROR',
          message: (data['detail'] as String?) ??
              e.message ??
              'Error de comunicación',
        );
      }
      throw ApiException(
        statusCode: status,
        code: 'NETWORK_ERROR',
        message: e.message ?? 'Error de red',
      );
    }
  }

  Future<HousesResponse> getHouses() => _wrap(() async {
        final res = await _dio.get<Map<String, dynamic>>('/api/v1/houses');
        final parsed = HousesResponse.fromJson(res.data!);
        await _storage
            .setLocalHouseIds(parsed.houses.map((h) => h.id).toList());
        return parsed;
      });

  Future<HouseDto> createHouse(String name) => _wrap(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/v1/houses',
          data: {'name': name},
        );
        final data = res.data!;
        final deviceToken = data['deviceToken'] as String?;
        if (deviceToken != null && deviceToken.isNotEmpty) {
          await _storage.setDeviceToken(deviceToken);
        }
        final house = HouseDto.fromJson(data['house'] as Map<String, dynamic>);
        await _storage.setActiveHouseId(house.id);
        return house;
      });

  Future<InvitationResponse> createInvitation(String houseId) => _wrap(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/v1/houses/$houseId/invitations',
        );
        return InvitationResponse.fromJson(res.data!);
      });

  Future<HouseDto> joinInvitation(String token) => _wrap(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/v1/invitations/$token/join',
        );
        final data = res.data!;
        final deviceToken = data['deviceToken'] as String?;
        if (deviceToken != null && deviceToken.isNotEmpty) {
          await _storage.setDeviceToken(deviceToken);
        }
        final house = HouseDto.fromJson(data['house'] as Map<String, dynamic>);
        await _storage.setActiveHouseId(house.id);
        return house;
      });

  Future<HousesResponse> claimAnonymousHouses() => _wrap(() async {
        final deviceToken = await _storage.getDeviceToken();
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/v1/houses/claim',
          data: {'deviceToken': deviceToken},
        );
        return HousesResponse.fromJson(res.data!);
      });

  Future<List<StoreDto>> getStores(String houseId) => _wrap(() async {
        final res = await _dio.get<List<dynamic>>(
          '/api/v1/houses/$houseId/stores',
        );
        return res.data!
            .map((e) => StoreDto.fromJson(e as Map<String, dynamic>))
            .toList();
      });

  Future<StoreDto> createStore(String houseId, String name) => _wrap(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/v1/houses/$houseId/stores',
          data: {'name': name},
        );
        return StoreDto.fromJson(res.data!);
      });

  Future<void> setActiveStore(String houseId, String storeId) =>
      _wrap(() async {
        await _dio.put<void>(
          '/api/v1/houses/$houseId/active-store',
          data: {'storeId': storeId},
        );
      });

  Future<List<ProductTypeDto>> getProductTypes(String houseId) =>
      _wrap(() async {
        final res = await _dio.get<List<dynamic>>(
          '/api/v1/houses/$houseId/product-types',
        );
        return res.data!
            .map((e) => ProductTypeDto.fromJson(e as Map<String, dynamic>))
            .toList();
      });

  Future<ProductTypeDto> createProductType(
    String houseId, {
    required String name,
    required String fillHex,
    String? borderHex,
  }) =>
      _wrap(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/v1/houses/$houseId/product-types',
          data: {
            'name': name,
            'fillHex': fillHex,
            'borderHex': borderHex,
          },
        );
        return ProductTypeDto.fromJson(res.data!);
      });

  Future<List<ProductSuggestionDto>> searchProducts(
    String houseId, {
    String? query,
  }) =>
      _wrap(() async {
        final res = await _dio.get<List<dynamic>>(
          '/api/v1/houses/$houseId/products',
          queryParameters: query != null ? {'q': query} : null,
        );
        return res.data!
            .map((e) => ProductSuggestionDto.fromJson(e as Map<String, dynamic>))
            .toList();
      });

  Future<List<ShoppingListItemDto>> getList(String houseId) => _wrap(() async {
        final res = await _dio.get<List<dynamic>>(
          '/api/v1/houses/$houseId/list',
        );
        return res.data!
            .map((e) => ShoppingListItemDto.fromJson(e as Map<String, dynamic>))
            .toList();
      });

  Future<ShoppingListItemDto> addListItem(
    String houseId, {
    String? productId,
    String? name,
    String? productTypeId,
    String? format,
  }) =>
      _wrap(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/v1/houses/$houseId/list/items',
          data: {
            'productId': productId,
            'name': name,
            'productTypeId': productTypeId,
            'format': format,
          },
        );
        return ShoppingListItemDto.fromJson(res.data!);
      });

  Future<ShoppingListItemDto> updateListItem(
    String houseId,
    String itemId, {
    String? name,
    String? productTypeId,
    bool clearProductType = false,
    String? format,
  }) =>
      _wrap(() async {
        final res = await _dio.patch<Map<String, dynamic>>(
          '/api/v1/houses/$houseId/list/items/$itemId',
          data: {
            'name': name,
            'productTypeId': productTypeId,
            'clearProductType': clearProductType,
            'format': format,
          },
        );
        return ShoppingListItemDto.fromJson(res.data!);
      });

  Future<void> reorderList(String houseId, List<String> orderedIds) =>
      _wrap(() async {
        await _dio.put<void>(
          '/api/v1/houses/$houseId/list/reorder',
          data: {'orderedItemIds': orderedIds},
        );
      });

  Future<void> moveListItemToBasket(String houseId, String itemId) =>
      _wrap(() async {
        await _dio.post<void>(
          '/api/v1/houses/$houseId/list/items/$itemId/move-to-basket',
        );
      });

  Future<void> deleteListItem(String houseId, String itemId) => _wrap(() async {
        await _dio.delete<void>(
          '/api/v1/houses/$houseId/list/items/$itemId',
        );
      });

  Future<BasketResponse> getBasket(String houseId) => _wrap(() async {
        final res = await _dio.get<Map<String, dynamic>>(
          '/api/v1/houses/$houseId/basket',
        );
        return BasketResponse.fromJson(res.data!);
      });

  Future<void> updateBasketItem(
    String houseId,
    String itemId, {
    String? name,
    double? quantity,
    String? productTypeId,
    bool clearProductType = false,
    String? format,
  }) =>
      _wrap(() async {
        await _dio.patch<void>(
          '/api/v1/houses/$houseId/basket/items/$itemId',
          data: {
            'name': name,
            'quantity': quantity,
            'productTypeId': productTypeId,
            'clearProductType': clearProductType,
            'format': format,
          },
        );
      });

  Future<void> setBasketItemPrice(
    String houseId,
    String itemId,
    double price,
  ) =>
      _wrap(() async {
        await _dio.put<void>(
          '/api/v1/houses/$houseId/basket/items/$itemId/price',
          data: {'price': price},
        );
      });

  Future<void> moveBasketItemToList(String houseId, String itemId) =>
      _wrap(() async {
        await _dio.post<void>(
          '/api/v1/houses/$houseId/basket/items/$itemId/move-to-list',
        );
      });

  Future<void> deleteBasketItem(String houseId, String itemId) =>
      _wrap(() async {
        await _dio.delete<void>(
          '/api/v1/houses/$houseId/basket/items/$itemId',
        );
      });

  Future<PurchaseDetailDto> checkoutBasket(String houseId) => _wrap(() async {
        final res = await _dio.post<Map<String, dynamic>>(
          '/api/v1/houses/$houseId/basket/checkout',
        );
        return PurchaseDetailDto.fromJson(res.data!);
      });

  Future<List<PurchaseSummaryDto>> getPurchases(String houseId) =>
      _wrap(() async {
        final res = await _dio.get<List<dynamic>>(
          '/api/v1/houses/$houseId/purchases',
        );
        return res.data!
            .map((e) => PurchaseSummaryDto.fromJson(e as Map<String, dynamic>))
            .toList();
      });

  Future<PurchaseDetailDto> getPurchaseDetail(
    String houseId,
    String purchaseId,
  ) =>
      _wrap(() async {
        final res = await _dio.get<Map<String, dynamic>>(
          '/api/v1/houses/$houseId/purchases/$purchaseId',
        );
        return PurchaseDetailDto.fromJson(res.data!);
      });

  Future<void> updateManualTotal(
    String houseId,
    String purchaseId,
    double manualTotal,
  ) =>
      _wrap(() async {
        await _dio.patch<void>(
          '/api/v1/houses/$houseId/purchases/$purchaseId/total',
          data: {'manualTotal': manualTotal},
        );
      });
}
