import 'dart:async';
import 'dart:js_interop';
import 'dart:js_interop_unsafe';

@JS('window')
external JSObject get _window;

@JS('navigator')
external JSObject get _navigator;

@JS('window.matchMedia')
external JSObject _matchMedia(String query);

@JS('window.open')
external JSObject? _windowOpen(String url, String target, String features);

class PwaInstallAdapter {
  static bool get isWeb => true;

  static bool get isRunningStandalone {
    try {
      final mq = _matchMedia('(display-mode: standalone)');
      final matches = (mq.getProperty('matches'.toJS) as JSBoolean?)?.toDart ?? false;
      if (matches) return true;
      final navStandalone = (_navigator.getProperty('standalone'.toJS) as JSBoolean?)?.toDart ?? false;
      return navStandalone;
    } catch (_) {
      return false;
    }
  }

  static bool get isIosBrowser {
    try {
      final ua = (_navigator.getProperty('userAgent'.toJS) as JSString?)?.toDart ?? '';
      return RegExp(r'iPhone|iPad|iPod', caseSensitive: false).hasMatch(ua);
    } catch (_) {
      return false;
    }
  }

  static bool get hasNativePrompt {
    try {
      final prompt = _window.getProperty('__babasketetInstallPrompt'.toJS);
      return prompt != null && !prompt.isUndefined && !prompt.isNull;
    } catch (_) {
      return false;
    }
  }

  static Future<bool> promptInstall() async {
    try {
      final promptObj = _window.getProperty('__babasketetInstallPrompt'.toJS);
      if (promptObj == null || promptObj.isUndefined || promptObj.isNull) {
        return false;
      }
      final deferred = promptObj as JSObject;
      await (deferred.callMethod('prompt'.toJS) as JSPromise).toDart;
      final choice = await (deferred.getProperty('userChoice'.toJS) as JSPromise).toDart;
      final outcome = ((choice as JSObject?)?.getProperty('outcome'.toJS) as JSString?)?.toDart;
      _window.setProperty('__babasketetInstallPrompt'.toJS, null);
      return outcome == 'accepted';
    } catch (_) {
      return false;
    }
  }

  static Future<String?> openOidcPopupAndWaitForCode(String authUrl) async {
    try {
      final popup = _windowOpen(
        authUrl,
        'babasketet_william_auth',
        'width=460,height=740,popup=yes',
      );
      if (popup == null || popup.isUndefined || popup.isNull) {
        return null;
      }

      final completer = Completer<String?>();
      final origin = (_window.getProperty('location'.toJS) as JSObject)
          .getProperty('origin'.toJS) as JSString;
      final expectedOrigin = origin.toDart;

      late JSFunction listener;
      Timer? pollClosed;

      void cleanup() {
        pollClosed?.cancel();
        _window.callMethod('removeEventListener'.toJS, 'message'.toJS, listener);
      }

      listener = ((JSObject event) {
        try {
          final evtOrigin = (event.getProperty('origin'.toJS) as JSString?)?.toDart;
          if (evtOrigin != expectedOrigin) return;
          final data = event.getProperty('data'.toJS);
          if (data == null || data.isUndefined || data.isNull) return;
          final dataObj = data as JSObject;
          final type = (dataObj.getProperty('type'.toJS) as JSString?)?.toDart;
          if (type != 'babasketet_oidc_code') return;
          final code = (dataObj.getProperty('code'.toJS) as JSString?)?.toDart;
          if (!completer.isCompleted) {
            cleanup();
            completer.complete(code);
          }
        } catch (_) {}
      }).toJS;

      _window.callMethod('addEventListener'.toJS, 'message'.toJS, listener);

      pollClosed = Timer.periodic(const Duration(milliseconds: 500), (_) {
        try {
          final closed = (popup.getProperty('closed'.toJS) as JSBoolean?)?.toDart ?? false;
          if (closed && !completer.isCompleted) {
            cleanup();
            completer.complete('');
          }
        } catch (_) {}
      });

      return completer.future;
    } catch (_) {
      return null;
    }
  }

  static bool tryNotifyOpenerAndClose(String code) {
    try {
      final opener = _window.getProperty('opener'.toJS);
      if (opener == null || opener.isUndefined || opener.isNull) {
        return false;
      }
      final origin = ((_window.getProperty('location'.toJS) as JSObject)
              .getProperty('origin'.toJS) as JSString)
          .toDart;
      final msg = JSObject();
      msg.setProperty('type'.toJS, 'babasketet_oidc_code'.toJS);
      msg.setProperty('code'.toJS, code.toJS);
      (opener as JSObject).callMethod('postMessage'.toJS, msg, origin.toJS);
      _window.callMethod('close'.toJS);
      return true;
    } catch (_) {
      return false;
    }
  }
}
