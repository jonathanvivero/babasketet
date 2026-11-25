import 'package:shared_preferences/shared_preferences.dart';

class LocalStorageService {
  static const _kDeviceToken = 'babasketet.device_token';
  static const _kOidcToken = 'babasketet.oidc_token';
  static const _kActiveHouseId = 'babasketet.active_house_id';
  static const _kHouseIds = 'babasketet.house_ids';
  static const _kPkceVerifier = 'babasketet.pkce_verifier';

  Future<String?> getDeviceToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_kDeviceToken);
  }

  Future<void> setDeviceToken(String token) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_kDeviceToken, token);
  }

  Future<String?> getOidcToken() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_kOidcToken);
  }

  Future<void> setOidcToken(String? token) async {
    final prefs = await SharedPreferences.getInstance();
    if (token == null || token.isEmpty) {
      await prefs.remove(_kOidcToken);
    } else {
      await prefs.setString(_kOidcToken, token);
    }
  }

  Future<String?> getEffectiveBearerToken() async {
    final oidc = await getOidcToken();
    if (oidc != null && oidc.isNotEmpty) return oidc;
    return getDeviceToken();
  }

  Future<String?> getActiveHouseId() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getString(_kActiveHouseId);
  }

  Future<void> setActiveHouseId(String? houseId) async {
    final prefs = await SharedPreferences.getInstance();
    if (houseId == null) {
      await prefs.remove(_kActiveHouseId);
    } else {
      await prefs.setString(_kActiveHouseId, houseId);
    }
  }

  Future<List<String>> getLocalHouseIds() async {
    final prefs = await SharedPreferences.getInstance();
    return prefs.getStringList(_kHouseIds) ?? [];
  }

  Future<void> setLocalHouseIds(List<String> ids) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setStringList(_kHouseIds, ids);
  }

  Future<void> setPkceVerifier(String verifier) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_kPkceVerifier, verifier);
  }

  Future<String?> takePkceVerifier() async {
    final prefs = await SharedPreferences.getInstance();
    final v = prefs.getString(_kPkceVerifier);
    await prefs.remove(_kPkceVerifier);
    return v;
  }
}
