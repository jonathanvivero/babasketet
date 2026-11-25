import 'dart:async';
import 'dart:convert';

import 'package:web_socket_channel/web_socket_channel.dart';

import '../config/app_config.dart';

class SignalRHouseClient {
  static const String _recordSeparator = '\x1e';
  WebSocketChannel? _channel;
  StreamSubscription<dynamic>? _sub;
  final _events = StreamController<String>.broadcast();
  String? _joinedHouseId;

  Stream<String> get events => _events.stream;

  Future<void> connect(String houseId, String? token) async {
    if (_joinedHouseId == houseId && _channel != null) return;
    await disconnect();

    if (token == null || token.isEmpty) return;
    _joinedHouseId = houseId;

    final base = Uri.parse(AppConfig.apiBaseUrl);
    final wsScheme = base.scheme == 'https' ? 'wss' : 'ws';
    final wsUri = base.replace(
      scheme: wsScheme,
      path: '/hubs/house',
      queryParameters: {'access_token': token},
    );

    try {
      final ch = WebSocketChannel.connect(wsUri);
      _channel = ch;
      await ch.ready;

      ch.sink.add('${jsonEncode({'protocol': 'json', 'version': 1})}$_recordSeparator');
      ch.sink.add('${jsonEncode({
        'type': 1,
        'target': 'JoinHouseGroup',
        'arguments': [houseId],
      })}$_recordSeparator');

      _sub = ch.stream.listen(
        (raw) {
          if (raw is! String) return;
          for (final frame in raw.split(_recordSeparator)) {
            if (frame.trim().isEmpty) continue;
            try {
              final decoded = jsonDecode(frame) as Map<String, dynamic>;
              if (decoded['type'] == 1 && decoded['target'] is String) {
                _events.add(decoded['target'] as String);
              }
            } catch (_) {}
          }
        },
        onError: (_) {},
        onDone: () {},
      );
    } catch (_) {
      // Modo degradado sin WebSocket en entornos offline
    }
  }

  Future<void> disconnect() async {
    _joinedHouseId = null;
    await _sub?.cancel();
    _sub = null;
    await _channel?.sink.close();
    _channel = null;
  }

  void dispose() {
    disconnect();
    _events.close();
  }
}
