import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import 'core/pwa/pwa_install.dart';
import 'core/router/url_strategy.dart';
import 'features/admin/admin_page.dart';
import 'features/dashboard/dashboard_page.dart';
import 'features/houses/house_providers.dart';
import 'features/houses/onboarding_page.dart';
import 'features/invitations/invitation_redeem_page.dart';
import 'features/pwa/pwa_install_banner.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  configureUrlStrategy();
  SemanticsBinding.instance.ensureSemantics();
  runApp(const ProviderScope(child: BabasketetApp()));
}

class BabasketetApp extends ConsumerStatefulWidget {
  const BabasketetApp({super.key});

  @override
  ConsumerState<BabasketetApp> createState() => _BabasketetAppState();
}

class _BabasketetAppState extends ConsumerState<BabasketetApp> {
  late final GoRouter _router;

  @override
  void initState() {
    super.initState();
    Future.microtask(() async {
      await ref.read(houseControllerProvider.notifier).bootstrap();
      await evaluateFirstVisitInstallPrompt(ref);
    });

    _router = GoRouter(
      routes: [
        GoRoute(
          path: '/',
          builder: (context, state) => const _HomeGate(),
        ),
        GoRoute(
          path: '/join/:token',
          builder: (context, state) => _JoinRouteHandler(
            token: state.pathParameters['token'] ?? '',
          ),
        ),
        GoRoute(
          path: '/auth/callback',
          builder: (context, state) => _AuthCallbackHandler(
            code: state.uri.queryParameters['code'],
          ),
        ),
        GoRoute(
          path: '/onboarding',
          builder: (context, state) => const OnboardingPage(),
        ),
        GoRoute(
          path: '/invitation-redeem',
          builder: (context, state) =>
              const InvitationRedeemPage(initialCode: ''),
        ),
        GoRoute(
          path: '/invitation-redeem/:code',
          builder: (context, state) => InvitationRedeemPage(
            initialCode: state.pathParameters['code'] ?? '',
          ),
        ),
        GoRoute(
          path: '/admin-bo',
          builder: (context, state) => const AdminBackofficePage(),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'Babasketet',
      debugShowCheckedModeBanner: false,
      locale: const Locale('es', 'ES'),
      supportedLocales: const [Locale('es', 'ES'), Locale('pt', 'PT'), Locale('en')],
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      theme: ThemeData(
        useMaterial3: true,
        colorSchemeSeed: const Color(0xFFD97706),
      ),
      routerConfig: _router,
    );
  }
}

class _HomeGate extends ConsumerWidget {
  const _HomeGate();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final appState = ref.watch(houseControllerProvider);
    if (appState.loading && appState.activeHouse == null) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }
    if (appState.activeHouse == null) {
      return const OnboardingPage();
    }
    return const DashboardPage();
  }
}

class _JoinRouteHandler extends ConsumerStatefulWidget {
  final String token;

  const _JoinRouteHandler({required this.token});

  @override
  ConsumerState<_JoinRouteHandler> createState() => _JoinRouteHandlerState();
}

class _JoinRouteHandlerState extends ConsumerState<_JoinRouteHandler> {
  String? _error;

  @override
  void initState() {
    super.initState();
    _join();
  }

  Future<void> _join() async {
    try {
      await ref.read(houseControllerProvider.notifier).joinInvitation(widget.token);
      if (mounted) context.go('/');
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: _error != null
            ? Padding(
                padding: const EdgeInsets.all(24),
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.link_off_rounded, size: 48, color: Colors.red),
                    const SizedBox(height: 12),
                    Text(_error!, textAlign: TextAlign.center),
                    const SizedBox(height: 16),
                    FilledButton(
                      onPressed: () => context.go('/'),
                      child: const Text('Volver al inicio'),
                    ),
                  ],
                ),
              )
            : const CircularProgressIndicator(),
      ),
    );
  }
}

class _AuthCallbackHandler extends ConsumerStatefulWidget {
  final String? code;

  const _AuthCallbackHandler({required this.code});

  @override
  ConsumerState<_AuthCallbackHandler> createState() => _AuthCallbackHandlerState();
}

class _AuthCallbackHandlerState extends ConsumerState<_AuthCallbackHandler> {
  String? _error;

  @override
  void initState() {
    super.initState();
    _complete();
  }

  Future<void> _complete() async {
    final code = widget.code;
    if (code == null || code.isEmpty) {
      setState(() => _error = 'Falta el código de autorización.');
      return;
    }
    if (PwaInstallAdapter.tryNotifyOpenerAndClose(code)) {
      return;
    }
    try {
      await ref.read(oidcServiceProvider).completeCallback(code);
      final pendingInvite =
          await ref.read(localStorageProvider).takePendingInviteCode();
      if (pendingInvite != null && pendingInvite.isNotEmpty) {
        await ref.read(apiClientProvider).redeemPlatformInvitation(pendingInvite);
        await ref.read(apiClientProvider).claimAnonymousHouses();
        await ref.read(houseControllerProvider.notifier).bootstrap();
        if (mounted) context.go('/onboarding');
        return;
      }
      await ref.read(apiClientProvider).claimAnonymousHouses();
      await ref.read(houseControllerProvider.notifier).bootstrap();
      if (mounted) context.go('/');
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: _error != null
            ? Text(_error!)
            : const CircularProgressIndicator(),
      ),
    );
  }
}
