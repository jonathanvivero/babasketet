class PwaInstallAdapter {
  static bool get isWeb => false;
  static bool get isRunningStandalone => false;
  static bool get isIosBrowser => false;
  static bool get hasNativePrompt => false;

  static Future<bool> promptInstall() async => false;

  static Future<String?> openOidcPopupAndWaitForCode(String authUrl) async => null;

  static bool tryNotifyOpenerAndClose(String code) => false;
}
