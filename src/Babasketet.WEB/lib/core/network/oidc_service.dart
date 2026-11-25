import 'dart:convert';
import 'dart:math';

import 'package:crypto/crypto.dart';
import 'package:dio/dio.dart';
import 'package:url_launcher/url_launcher.dart';

import '../config/app_config.dart';
import '../storage/local_storage.dart';

class OidcService {
  final LocalStorageService _storage;
  final Dio _dio = Dio();

  OidcService(this._storage);

  Future<({String issuer, String clientId})> fetchConfig() async {
    final res = await _dio.get<Map<String, dynamic>>(
      '${AppConfig.williamUiUrl}/api/oidc-config',
    );
    final data = res.data!;
    return (
      issuer: (data['issuer'] as String?) ?? AppConfig.oidcAuthority,
      clientId: (data['clientId'] as String?) ?? '',
    );
  }

  Future<void> startLogin({String? redirectOrigin}) async {
    final cfg = await fetchConfig();
    if (cfg.clientId.isEmpty) {
      throw StateError('OIDC ClientId no configurado en Zitadel.');
    }

    final verifier = _randomBase64Url(48);
    await _storage.setPkceVerifier(verifier);
    final challenge = base64UrlEncode(sha256.convert(utf8.encode(verifier)).bytes)
        .replaceAll('=', '');

    final origin = redirectOrigin ?? Uri.base.origin;
    final authUri = Uri.parse('${cfg.issuer}/oauth/v2/authorize').replace(
      queryParameters: {
        'client_id': cfg.clientId,
        'redirect_uri': '$origin/auth/callback',
        'response_type': 'code',
        'scope': 'openid profile email',
        'code_challenge': challenge,
        'code_challenge_method': 'S256',
      },
    );

    await launchUrl(authUri, webOnlyWindowName: '_self');
  }

  Future<void> completeCallback(String code, {String? redirectOrigin}) async {
    final verifier = await _storage.takePkceVerifier();
    if (verifier == null || verifier.isEmpty) {
      throw StateError('Falta code_verifier PKCE.');
    }
    final cfg = await fetchConfig();
    final origin = redirectOrigin ?? Uri.base.origin;

    final res = await _dio.post<Map<String, dynamic>>(
      '${cfg.issuer}/oauth/v2/token',
      data: {
        'grant_type': 'authorization_code',
        'client_id': cfg.clientId,
        'code': code,
        'redirect_uri': '$origin/auth/callback',
        'code_verifier': verifier,
      },
      options: Options(contentType: Headers.formUrlEncodedContentType),
    );

    final accessToken = res.data?['access_token'] as String?;
    if (accessToken == null || accessToken.isEmpty) {
      throw StateError('Zitadel no devolvió access_token.');
    }
    await _storage.setOidcToken(accessToken);
  }

  static String _randomBase64Url(int length) {
    final rnd = Random.secure();
    final bytes = List<int>.generate(length, (_) => rnd.nextInt(256));
    return base64UrlEncode(bytes).replaceAll('=', '');
  }
}
