import 'package:babasketet_web/core/config/app_config.dart';
import 'package:babasketet_web/core/models/models.dart';
import 'package:babasketet_web/core/network/api_client.dart';
import 'package:babasketet_web/core/storage/local_storage.dart';
import 'package:babasketet_web/core/widgets/long_press_and_dot.dart';
import 'package:babasketet_web/features/admin/admin_page.dart';
import 'package:babasketet_web/features/houses/house_providers.dart';
import 'package:babasketet_web/features/houses/onboarding_page.dart';
import 'package:babasketet_web/features/pwa/pwa_install_banner.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

class _FakeAdminApiClient extends BabasketetApiClient {
  final MeResponse me;
  _FakeAdminApiClient(this.me) : super(storage: LocalStorageService());

  @override
  Future<MeResponse> getMe() async => me;

  @override
  Future<List<AdminHouseSummaryDto>> adminGetHouses() async => const [];

  @override
  Future<List<AdminUserSummaryDto>> adminGetUsers() async => const [];

  @override
  Future<List<AdminCatalogItemDto>> adminGetItems({String? query}) async =>
      const [];

  EmailConfigDto _emailConfig = const EmailConfigDto(
    isConfigured: true,
    host: 'mailpit',
    port: 1025,
    hasPassword: false,
    securityMode: 'None',
    fromEmail: 'noreply@babasketet.local',
    fromName: 'Babasketet',
    enabled: true,
  );

  @override
  Future<EmailConfigDto> adminGetEmailConfig() async => _emailConfig;

  @override
  Future<EmailConfigDto> adminSaveEmailConfig({
    required String host,
    required int port,
    String? username,
    String? password,
    bool clearPassword = false,
    required String securityMode,
    required String fromEmail,
    String? fromName,
    required bool enabled,
  }) async {
    _emailConfig = EmailConfigDto(
      isConfigured: true,
      host: host,
      port: port,
      username: username,
      hasPassword: !clearPassword && (password?.isNotEmpty == true || _emailConfig.hasPassword),
      securityMode: securityMode,
      fromEmail: fromEmail,
      fromName: fromName ?? 'Babasketet',
      enabled: enabled,
    );
    return _emailConfig;
  }

  @override
  Future<TestEmailResultDto> adminSendTestEmail({
    String? toEmail,
    Map<String, dynamic>? overrideConfig,
  }) async =>
      TestEmailResultDto(
        success: true,
        recipient: toEmail ?? 'admin@babasketet.test',
        message: 'Correo de prueba enviado correctamente.',
        sentAt: DateTime(2026, 11, 26),
      );

  final List<AdminNotificationDto> _notifications = [
    AdminNotificationDto(
      id: 'notif-1',
      title: 'Solicitud de renovación',
      body: 'Detalle completo notificación 1',
      category: 'system',
      status: 'New',
      createdAt: DateTime(2026, 11, 26, 10, 0),
    ),
    AdminNotificationDto(
      id: 'notif-2',
      title: 'Aviso secundario',
      body: 'Detalle completo notificación 2',
      category: 'system',
      status: 'New',
      createdAt: DateTime(2026, 11, 26, 9, 0),
    ),
  ];

  @override
  Future<AdminNotificationsPageDto> adminGetNotifications({
    String status = 'New',
    int page = 1,
    int pageSize = 10,
  }) async {
    final allowed = status == 'All'
        ? {'New', 'Read', 'Archived'}
        : status.split(',').toSet();
    final filtered =
        _notifications.where((n) => allowed.contains(n.status)).toList();
    return AdminNotificationsPageDto(
      items: filtered,
      page: 1,
      pageSize: pageSize,
      totalCount: filtered.length,
      totalPages: 1,
      newCount: _notifications.where((n) => n.status == 'New').length,
    );
  }

  @override
  Future<AdminNotificationDto> adminUpdateNotificationStatus(
    String notificationId,
    String status,
  ) async {
    final idx = _notifications.indexWhere((n) => n.id == notificationId);
    final old = _notifications[idx];
    final updated = AdminNotificationDto(
      id: old.id,
      title: old.title,
      body: old.body,
      category: old.category,
      status: status,
      createdAt: old.createdAt,
      readAt: status == 'New' ? null : (old.readAt ?? DateTime(2026, 11, 26, 10, 5)),
      archivedAt: status == 'Archived' ? DateTime(2026, 11, 26, 10, 10) : null,
    );
    _notifications[idx] = updated;
    return updated;
  }

  final List<PlatformInvitationDto> _invitations = [];

  @override
  Future<List<PlatformInvitationDto>> adminGetPlatformInvitations({
    String dateRange = 'all',
    String statuses = 'Active',
  }) async {
    final allowed = statuses == 'All'
        ? {'Active', 'Redeemed', 'Cancelled', 'Expired'}
        : statuses.split(',').toSet();
    return _invitations.where((i) => allowed.contains(i.status)).toList();
  }

  @override
  Future<PlatformInvitationDto> adminCreatePlatformInvitation({
    DateTime? expiresAt,
    bool isIndefinite = false,
  }) async {
    final now = DateTime(2026, 11, 26, 12, 0);
    final inv = PlatformInvitationDto(
      id: 'inv-${_invitations.length + 1}',
      code: 'BBK-TEST000${_invitations.length + 1}',
      status: 'Active',
      createdAt: now,
      expiresAt: isIndefinite ? null : (expiresAt ?? now.add(const Duration(days: 7))),
      isIndefinite: isIndefinite,
    );
    _invitations.insert(0, inv);
    return inv;
  }

  @override
  Future<PlatformInvitationDto> adminCancelPlatformInvitation(String id) async {
    final idx = _invitations.indexWhere((i) => i.id == id);
    final old = _invitations[idx];
    final updated = PlatformInvitationDto(
      id: old.id,
      code: old.code,
      status: 'Cancelled',
      createdAt: old.createdAt,
      expiresAt: old.expiresAt,
      isIndefinite: old.isIndefinite,
      cancelledAt: DateTime(2026, 11, 26, 12, 5),
    );
    _invitations[idx] = updated;
    return updated;
  }

  @override
  Future<PlatformInvitationDto> adminRenewPlatformInvitation(
    String id, {
    int days = 7,
    bool isIndefinite = false,
  }) async {
    final idx = _invitations.indexWhere((i) => i.id == id);
    final old = _invitations[idx];
    final updated = PlatformInvitationDto(
      id: old.id,
      code: old.code,
      status: 'Active',
      createdAt: old.createdAt,
      expiresAt: isIndefinite ? null : (old.expiresAt ?? DateTime(2026, 11, 26)).add(Duration(days: days)),
      isIndefinite: isIndefinite,
    );
    _invitations[idx] = updated;
    return updated;
  }

  @override
  Future<PlatformInvitationDto> adminReactivatePlatformInvitation(
    String id, {
    int days = 7,
  }) async {
    final idx = _invitations.indexWhere((i) => i.id == id);
    final old = _invitations[idx];
    final updated = PlatformInvitationDto(
      id: old.id,
      code: old.code,
      status: 'Active',
      createdAt: old.createdAt,
      expiresAt: DateTime(2026, 11, 26).add(Duration(days: days)),
      isIndefinite: false,
    );
    _invitations[idx] = updated;
    return updated;
  }

  @override
  Future<SendPlatformInvitationEmailsResultDto> adminSendPlatformInvitationEmails(
    String id, {
    required List<String> emails,
    String? baseUrl,
  }) async =>
      SendPlatformInvitationEmailsResultDto(
        sentCount: emails.length,
        recipients: emails,
        message: 'Invitación enviada a ${emails.length} destinatario(s).',
      );
}

void main() {
  test('AppConfig formats and parses Euro amounts with es-ES comma', () {
    expect(AppConfig.formatEuro(0), '0,00 €');
    expect(AppConfig.formatEuro(12.5), '12,50 €');
    expect(AppConfig.parseEuroInput('1,95'), 1.95);
    expect(AppConfig.parseEuroInput('2.40 €'), 2.40);
    expect(AppConfig.allowedFormats.length, 11);
  });

  testWidgets(
      'ItemLongPressDetector does not trigger before 800ms and triggers after 800ms',
      (tester) async {
    var triggered = 0;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ItemLongPressDetector(
            onTriggered: () => triggered++,
            child: const SizedBox(
              width: 200,
              height: 60,
              child: Text('Leche'),
            ),
          ),
        ),
      ),
    );

    final gesture =
        await tester.startGesture(tester.getCenter(find.text('Leche')));
    await tester.pump(const Duration(milliseconds: 400));
    expect(triggered, 0);

    await tester.pump(const Duration(milliseconds: 450));
    expect(triggered, 1);

    await gesture.up();
  });

  testWidgets('ProductTypeDot renders fill and optional border',
      (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: Column(
            children: [
              ProductTypeDot(fillHex: '#FFFFFF', borderHex: '#FF00FF'),
              ProductTypeDot(fillHex: '#86EFAC'),
            ],
          ),
        ),
      ),
    );

    final containers =
        tester.widgetList<Container>(find.byType(Container)).toList();
    final withBorder = containers[0].decoration as BoxDecoration;
    final withoutBorder = containers[1].decoration as BoxDecoration;
    expect(withBorder.color, const Color(0xFFFFFFFF));
    expect(withBorder.border, isNotNull);
    expect(withoutBorder.color, const Color(0xFF86EFAC));
    expect(withoutBorder.border, isNull);
  });

  testWidgets('OnboardingPage shows Create and Join house sections',
      (tester) async {
    await tester.pumpWidget(
      const ProviderScope(
        child: MaterialApp(
          home: OnboardingPage(),
        ),
      ),
    );

    expect(find.text('Babasketet'), findsOneWidget);
    expect(find.text('Crear Casa'), findsOneWidget);
    expect(find.text('Unirme a la Casa'), findsOneWidget);
  });

  testWidgets('PwaInstallBanner renders when enabled and hides on dismiss',
      (tester) async {
    SharedPreferences.setMockInitialValues({});

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          pwaInstallPromptVisibleProvider.overrideWith((ref) => true),
        ],
        child: const MaterialApp(
          home: Scaffold(
            body: PwaInstallBanner(),
          ),
        ),
      ),
    );

    expect(find.text('Instalar'), findsOneWidget);
    await tester.tap(find.byIcon(Icons.close_rounded));
    await tester.pumpAndSettle();
    expect(find.text('Instalar'), findsNothing);
  });

  testWidgets('AdminBackofficePage enforces Admin role guard', (tester) async {
    const userMe = MeResponse(
      globalRole: 'User',
      isAuthenticated: true,
      isAdmin: false,
      status: 'Active',
      email: 'user@babasketet.test',
      effectiveRoles: ['User'],
      houses: [],
    );

    await tester.pumpWidget(
      ProviderScope(
        key: const ValueKey('scope_user'),
        overrides: [
          apiClientProvider.overrideWithValue(_FakeAdminApiClient(userMe)),
        ],
        child: const MaterialApp(home: AdminBackofficePage()),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('403 · Acceso denegado'), findsOneWidget);

    const adminMe = MeResponse(
      globalRole: 'User',
      isAuthenticated: true,
      isAdmin: true,
      status: 'Active',
      email: 'jonathanvivero@gmail.com',
      effectiveRoles: ['User', 'Admin'],
      houses: [],
    );

    tester.view.physicalSize = const Size(1280, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    await tester.pumpWidget(
      ProviderScope(
        key: const ValueKey('scope_admin'),
        overrides: [
          apiClientProvider.overrideWithValue(_FakeAdminApiClient(adminMe)),
        ],
        child: const MaterialApp(home: AdminBackofficePage()),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.text('Casas (0)'), findsOneWidget);
    expect(find.text('Usuarios (0)'), findsOneWidget);
    expect(find.text('Ítems (0)'), findsOneWidget);
    expect(find.text('Configuraciones'), findsOneWidget);
    expect(find.text('Invitaciones'), findsOneWidget);

    await tester.tap(find.text('Invitaciones'));
    await tester.pumpAndSettle();
    expect(find.text('Sistema de Invitaciones'), findsOneWidget);
    await tester.tap(find.text('Generar invitación'));
    await tester.pumpAndSettle();
    expect(find.textContaining('BBK-TEST0001'), findsWidgets);
    expect(find.text('Copiar URL'), findsOneWidget);
    expect(find.text('Enviar por WhatsApp'), findsOneWidget);
    expect(find.text('Enviar por email'), findsOneWidget);

    await tester.tap(find.text('Configuraciones'));
    await tester.pumpAndSettle();
    expect(find.text('Configuraciones del sistema'), findsOneWidget);
    expect(find.text('Correo Electrónico Saliente'), findsOneWidget);
    expect(find.text('Guardar configuración SMTP'), findsOneWidget);
    await tester.drag(find.byType(ListView).first, const Offset(0, -450));
    await tester.pumpAndSettle();
    expect(find.text('Enviar email de prueba'), findsOneWidget);

    // Verificar panel de notificaciones y popup con auto-lectura + navegación
    expect(find.textContaining('Notificaciones (2 nuevas)'), findsOneWidget);
    await tester.tap(find.text('Solicitud de renovación'));
    await tester.pumpAndSettle();
    expect(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.text('Detalle completo notificación 1'),
      ),
      findsOneWidget,
    );
    await tester.tap(find.text('Siguiente →'));
    await tester.pumpAndSettle();
    expect(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.text('Detalle completo notificación 2'),
      ),
      findsOneWidget,
    );
    await tester.tap(find.text('Cerrar'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Notificaciones (0 nuevas)'), findsOneWidget);
  });
}
