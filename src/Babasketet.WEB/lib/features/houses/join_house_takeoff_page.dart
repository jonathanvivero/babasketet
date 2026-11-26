import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/models.dart';
import 'house_providers.dart';

class JoinHouseTakeoffPage extends ConsumerStatefulWidget {
  final String token;

  const JoinHouseTakeoffPage({super.key, required this.token});

  @override
  ConsumerState<JoinHouseTakeoffPage> createState() =>
      _JoinHouseTakeoffPageState();
}

class _JoinHouseTakeoffPageState extends ConsumerState<JoinHouseTakeoffPage> {
  bool _loading = true;
  bool _busy = false;
  InspectHouseInvitationResponseDto? _invitation;
  String? _error;

  @override
  void initState() {
    super.initState();
    Future.microtask(_load);
  }

  Future<void> _load() async {
    try {
      final api = ref.read(apiClientProvider);
      final info = await api.inspectHouseInvitation(widget.token);
      if (!mounted) return;

      final oidcToken = await ref.read(localStorageProvider).getOidcToken();
      if (oidcToken != null && oidcToken.isNotEmpty) {
        try {
          final me = await api.getMe();
          if (me.isAuthenticated) {
            await ref
                .read(houseControllerProvider.notifier)
                .joinInvitation(widget.token);
            if (mounted) context.go('/');
            return;
          }
        } catch (_) {
          // Si falla la sesión previa, muestra las opciones al usuario
        }
      }

      if (!mounted) return;
      setState(() {
        _invitation = info;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _continueWithAccount() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(localStorageProvider).setPendingHouseJoinToken(widget.token);
      final completedInPopup = await ref.read(oidcServiceProvider).startLogin();
      if (completedInPopup && mounted) {
        final pending =
            await ref.read(localStorageProvider).takePendingHouseJoinToken();
        if (pending != null && pending.isNotEmpty) {
          await ref.read(apiClientProvider).claimAnonymousHouses();
          await ref
              .read(houseControllerProvider.notifier)
              .joinInvitation(pending);
          if (mounted) context.go('/');
        }
      } else if (mounted) {
        setState(() => _busy = false);
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _continueAsGuest() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref
          .read(houseControllerProvider.notifier)
          .joinInvitation(widget.token);
      if (mounted) context.go('/');
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = e.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
    }

    if (_error != null && _invitation == null) {
      return Scaffold(
        body: Center(
          child: Padding(
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
          ),
        ),
      );
    }

    final houseName = _invitation?.houseName ?? 'la casa';

    return Scaffold(
      appBar: AppBar(
        title: const Text('Unirse a una Casa · Babasketet'),
        backgroundColor: const Color(0xFFD97706),
        foregroundColor: Colors.white,
        leading: IconButton(
          tooltip: 'Ir al inicio',
          icon: const Icon(Icons.home_rounded),
          onPressed: () => context.go('/'),
        ),
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 460),
            child: Card(
              elevation: 2,
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.group_add_rounded,
                        size: 52, color: Color(0xFFD97706)),
                    const SizedBox(height: 12),
                    Text(
                      'Te han invitado a "$houseName"',
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                          fontSize: 20, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Elige cómo quieres acceder antes de entrar en la lista compartida:',
                      textAlign: TextAlign.center,
                    ),
                    if (_error != null) ...[
                      const SizedBox(height: 12),
                      Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: const Color(0xFFFEE2E2),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          _error!,
                          style: const TextStyle(color: Color(0xFF991B1B)),
                        ),
                      ),
                    ],
                    const SizedBox(height: 20),
                    Semantics(
                      container: true,
                      identifier: 'join_takeoff_register_btn',
                      button: true,
                      child: FilledButton.icon(
                        onPressed: _busy ? null : _continueWithAccount,
                        icon: const Icon(Icons.person_add_alt_1_rounded),
                        label: const Text('Registrarme ahora'),
                      ),
                    ),
                    const SizedBox(height: 10),
                    Semantics(
                      container: true,
                      identifier: 'join_takeoff_login_btn',
                      button: true,
                      child: OutlinedButton.icon(
                        onPressed: _busy ? null : _continueWithAccount,
                        icon: const Icon(Icons.login_rounded),
                        label: const Text('Ya tengo cuenta (Introducir credenciales)'),
                      ),
                    ),
                    const Divider(height: 28),
                    Semantics(
                      container: true,
                      identifier: 'join_takeoff_guest_btn',
                      button: true,
                      child: TextButton.icon(
                        onPressed: _busy ? null : _continueAsGuest,
                        icon: const Icon(Icons.explore_outlined),
                        label: const Text('Continuar como invitado'),
                      ),
                    ),
                    const SizedBox(height: 4),
                    const Text(
                      'Si entras como invitado podrás registrarte más tarde desde tu perfil sin perder acceso a la casa.',
                      textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 12, color: Colors.black54),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
