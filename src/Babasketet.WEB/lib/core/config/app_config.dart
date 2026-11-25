class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:8080',
  );

  static const String oidcAuthority = String.fromEnvironment(
    'OIDC_AUTHORITY',
    defaultValue: 'http://localhost:8085',
  );

  static const String williamUiUrl = String.fromEnvironment(
    'WILLIAM_UI_URL',
    defaultValue: 'http://localhost:3000',
  );

  static const List<String> allowedFormats = [
    'Ud',
    'Docena',
    'Kg',
    'g',
    'L',
    'Cl',
    'Pack-2',
    'Pack-3',
    'Pack-4',
    'Pack-6',
    'Pack-8',
  ];

  static String formatEuro(double value) {
    final fixed = value.toStringAsFixed(2).replaceAll('.', ',');
    return '$fixed €';
  }

  static double? parseEuroInput(String raw) {
    final normalized = raw.trim().replaceAll('€', '').replaceAll(',', '.').trim();
    if (normalized.isEmpty) return null;
    return double.tryParse(normalized);
  }
}
