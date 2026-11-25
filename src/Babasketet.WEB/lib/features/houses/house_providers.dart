import 'dart:async';

import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/models.dart';
import '../../core/network/api_client.dart';
import '../../core/network/oidc_service.dart';
import '../../core/network/signalr_client.dart';
import '../../core/storage/local_storage.dart';

final localStorageProvider = Provider<LocalStorageService>(
  (ref) => LocalStorageService(),
);

final apiClientProvider = Provider<BabasketetApiClient>(
  (ref) => BabasketetApiClient(storage: ref.watch(localStorageProvider)),
);

final oidcServiceProvider = Provider<OidcService>(
  (ref) => OidcService(ref.watch(localStorageProvider)),
);

final signalRClientProvider = Provider<SignalRHouseClient>((ref) {
  final client = SignalRHouseClient();
  ref.onDispose(client.dispose);
  return client;
});

class AppState {
  final bool loading;
  final List<HouseDto> houses;
  final int maxHouses;
  final bool isRegistered;
  final bool isAdmin;
  final HouseDto? activeHouse;
  final List<StoreDto> stores;
  final List<ProductTypeDto> productTypes;
  final List<ShoppingListItemDto> listItems;
  final BasketResponse? basket;
  final List<PurchaseSummaryDto> purchases;
  final String? error;

  const AppState({
    this.loading = false,
    this.houses = const [],
    this.maxHouses = 1,
    this.isRegistered = false,
    this.isAdmin = false,
    this.activeHouse,
    this.stores = const [],
    this.productTypes = const [],
    this.listItems = const [],
    this.basket,
    this.purchases = const [],
    this.error,
  });

  AppState copyWith({
    bool? loading,
    List<HouseDto>? houses,
    int? maxHouses,
    bool? isRegistered,
    bool? isAdmin,
    HouseDto? activeHouse,
    bool clearActiveHouse = false,
    List<StoreDto>? stores,
    List<ProductTypeDto>? productTypes,
    List<ShoppingListItemDto>? listItems,
    BasketResponse? basket,
    List<PurchaseSummaryDto>? purchases,
    String? error,
    bool clearError = false,
  }) =>
      AppState(
        loading: loading ?? this.loading,
        houses: houses ?? this.houses,
        maxHouses: maxHouses ?? this.maxHouses,
        isRegistered: isRegistered ?? this.isRegistered,
        isAdmin: isAdmin ?? this.isAdmin,
        activeHouse:
            clearActiveHouse ? null : (activeHouse ?? this.activeHouse),
        stores: stores ?? this.stores,
        productTypes: productTypes ?? this.productTypes,
        listItems: listItems ?? this.listItems,
        basket: basket ?? this.basket,
        purchases: purchases ?? this.purchases,
        error: clearError ? null : (error ?? this.error),
      );
}

class HouseController extends Notifier<AppState> {
  StreamSubscription<String>? _signalRSub;

  @override
  AppState build() {
    ref.onDispose(() => _signalRSub?.cancel());
    return const AppState();
  }

  BabasketetApiClient get _api => ref.read(apiClientProvider);
  LocalStorageService get _storage => ref.read(localStorageProvider);
  SignalRHouseClient get _signalR => ref.read(signalRClientProvider);

  Future<void> bootstrap({String? preferredHouseId}) async {
    state = state.copyWith(loading: true, clearError: true);
    try {
      final resp = await _api.getHouses();
      var isAdmin = false;
      try {
        final me = await _api.getMe();
        isAdmin = me.isAdmin;
      } catch (_) {
        // Ignorado si /me falla durante arranque parcial
      }

      if (resp.houses.isEmpty) {
        state = AppState(
          loading: false,
          houses: const [],
          maxHouses: resp.maxHouses,
          isRegistered: resp.isRegistered,
          isAdmin: isAdmin,
        );
        return;
      }

      final storedActiveId = preferredHouseId ?? await _storage.getActiveHouseId();
      final selected = resp.houses.firstWhere(
        (h) => h.id == storedActiveId,
        orElse: () => resp.houses.first,
      );

      await _storage.setActiveHouseId(selected.id);
      state = state.copyWith(
        houses: resp.houses,
        maxHouses: resp.maxHouses,
        isRegistered: resp.isRegistered,
        isAdmin: isAdmin,
        activeHouse: selected,
      );

      await _loadHouseDetails(selected.id);
      await _connectRealtime(selected.id);
    } catch (e) {
      state = state.copyWith(loading: false, error: e.toString());
    }
  }

  Future<void> selectHouse(HouseDto house) async {
    await _storage.setActiveHouseId(house.id);
    state = state.copyWith(activeHouse: house, loading: true, clearError: true);
    await _loadHouseDetails(house.id);
    await _connectRealtime(house.id);
  }

  Future<void> createHouse(String name) async {
    state = state.copyWith(loading: true, clearError: true);
    try {
      final house = await _api.createHouse(name);
      await bootstrap(preferredHouseId: house.id);
    } catch (e) {
      state = state.copyWith(loading: false, error: e.toString());
      rethrow;
    }
  }

  Future<void> joinInvitation(String token) async {
    state = state.copyWith(loading: true, clearError: true);
    try {
      final house = await _api.joinInvitation(token);
      await bootstrap(preferredHouseId: house.id);
    } catch (e) {
      state = state.copyWith(loading: false, error: e.toString());
      rethrow;
    }
  }

  Future<void> _loadHouseDetails(String houseId) async {
    try {
      final results = await Future.wait([
        _api.getStores(houseId),
        _api.getProductTypes(houseId),
        _api.getList(houseId),
        _api.getBasket(houseId),
        _api.getPurchases(houseId),
      ]);

      final housesResp = await _api.getHouses();
      final updatedHouse = housesResp.houses.firstWhere(
        (h) => h.id == houseId,
        orElse: () => state.activeHouse!,
      );

      state = state.copyWith(
        loading: false,
        houses: housesResp.houses,
        maxHouses: housesResp.maxHouses,
        isRegistered: housesResp.isRegistered,
        activeHouse: updatedHouse,
        stores: results[0] as List<StoreDto>,
        productTypes: results[1] as List<ProductTypeDto>,
        listItems: results[2] as List<ShoppingListItemDto>,
        basket: results[3] as BasketResponse,
        purchases: results[4] as List<PurchaseSummaryDto>,
      );
    } catch (e) {
      state = state.copyWith(loading: false, error: e.toString());
    }
  }

  Future<void> refreshActiveHouse() async {
    final house = state.activeHouse;
    if (house != null) {
      await _loadHouseDetails(house.id);
    }
  }

  Future<void> _connectRealtime(String houseId) async {
    final token = await _storage.getEffectiveBearerToken();
    await _signalR.connect(houseId, token);
    await _signalRSub?.cancel();
    _signalRSub = _signalR.events.listen((event) {
      if (state.activeHouse?.id == houseId) {
        _loadHouseDetails(houseId);
      }
    });
  }
}

final houseControllerProvider =
    NotifierProvider<HouseController, AppState>(HouseController.new);
