import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../houses/account_dialog.dart';
import '../houses/house_providers.dart';
import '../pwa/pwa_install_banner.dart';

class OnboardingPage extends ConsumerStatefulWidget {
  final bool allowBack;

  const OnboardingPage({super.key, this.allowBack = false});

  @override
  ConsumerState<OnboardingPage> createState() => _OnboardingPageState();
}

class _OnboardingPageState extends ConsumerState<OnboardingPage> {
  final _houseNameCtrl = TextEditingController();
  final _inviteCtrl = TextEditingController();
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _houseNameCtrl.dispose();
    _inviteCtrl.dispose();
    super.dispose();
  }

  Future<void> _createHouse() async {
    final name = _houseNameCtrl.text.trim();
    if (name.isEmpty) {
      setState(() => _error = 'Escribe un nombre para la casa.');
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(houseControllerProvider.notifier).createHouse(name);
      if (mounted) {
        if (widget.allowBack && Navigator.of(context).canPop()) {
          Navigator.of(context).pop();
        } else {
          context.go('/');
        }
      }
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _joinHouse() async {
    var raw = _inviteCtrl.text.trim();
    if (raw.isEmpty) {
      setState(() => _error = 'Pega el enlace o código de invitación.');
      return;
    }
    if (raw.contains('/join/')) {
      raw = raw.split('/join/').last.split('?').first.trim();
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await ref.read(houseControllerProvider.notifier).joinInvitation(raw);
      if (mounted) {
        if (widget.allowBack && Navigator.of(context).canPop()) {
          Navigator.of(context).pop();
        } else {
          context.go('/');
        }
      }
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(houseControllerProvider);
    final reachedAnonLimit = !state.isRegistered && state.houses.isNotEmpty;

    return Scaffold(
      appBar: widget.allowBack ? AppBar(title: const Text('Añadir Casa')) : null,
      body: SafeArea(
        child: Column(
          children: [
            const PwaInstallBanner(),
            Expanded(
              child: Center(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(24),
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 420),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        const Icon(Icons.shopping_basket_rounded, size: 56, color: Color(0xFFD97706)),
                        const SizedBox(height: 12),
                        Text(
                          'Babasketet',
                          textAlign: TextAlign.center,
                          style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                                fontWeight: FontWeight.w800,
                              ),
                        ),
                        const SizedBox(height: 6),
                        const Text(
                          'Crea una casa o únete a una existente mediante enlace.',
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 24),
                        if (_error != null)
                          Container(
                            padding: const EdgeInsets.all(12),
                            margin: const EdgeInsets.only(bottom: 16),
                            decoration: BoxDecoration(
                              color: Colors.red.shade50,
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Text(_error!, style: TextStyle(color: Colors.red.shade900)),
                          ),
                        if (reachedAnonLimit) ...[
                          Card(
                            color: Colors.amber.shade50,
                            child: Padding(
                              padding: const EdgeInsets.all(16),
                              child: Column(
                                children: [
                                  const Text(
                                    'Sin registro solo puedes guardar 1 casa. Regístrate con WillIAM / Zitadel (Passkey, Email, Teléfono o SSO) para gestionar hasta 4 casas.',
                                    textAlign: TextAlign.center,
                                  ),
                                  const SizedBox(height: 12),
                                  Semantics(
                                    container: true,
                                    identifier: 'onboarding_login_btn',
                                    button: true,
                                    child: FilledButton.icon(
                                      onPressed: () =>
                                          triggerOidcAccountFlow(context, ref),
                                      icon: const Icon(Icons.key_rounded),
                                      label: const Text('Iniciar sesión / Crear cuenta'),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ] else ...[
                          Card(
                            child: Padding(
                              padding: const EdgeInsets.all(16),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                children: [
                                  const Text('Crear una casa nueva',
                                      style: TextStyle(fontWeight: FontWeight.bold)),
                                  const SizedBox(height: 8),
                                  Semantics(
                                    container: true,
                                    identifier: 'onboarding_house_name_input',
                                    textField: true,
                                    child: TextField(
                                      controller: _houseNameCtrl,
                                      decoration: const InputDecoration(
                                        labelText: 'Nombre de la casa',
                                        hintText: 'Ej. Casa Centro',
                                        border: OutlineInputBorder(),
                                      ),
                                    ),
                                  ),
                                  const SizedBox(height: 12),
                                  Semantics(
                                    container: true,
                                    identifier: 'onboarding_create_house_btn',
                                    button: true,
                                    child: FilledButton(
                                      onPressed: _busy ? null : _createHouse,
                                      child: const Text('Crear Casa'),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                          const SizedBox(height: 16),
                          Card(
                            child: Padding(
                              padding: const EdgeInsets.all(16),
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.stretch,
                                children: [
                                  const Text('Unirse con enlace o código',
                                      style: TextStyle(fontWeight: FontWeight.bold)),
                                  const SizedBox(height: 8),
                                  Semantics(
                                    container: true,
                                    identifier: 'onboarding_join_token_input',
                                    textField: true,
                                    child: TextField(
                                      controller: _inviteCtrl,
                                      decoration: const InputDecoration(
                                        labelText: 'URL o código de invitación',
                                        hintText: 'https://.../join/...',
                                        border: OutlineInputBorder(),
                                      ),
                                    ),
                                  ),
                                  const SizedBox(height: 12),
                                  Semantics(
                                    container: true,
                                    identifier: 'onboarding_join_house_btn',
                                    button: true,
                                    child: OutlinedButton(
                                      onPressed: _busy ? null : _joinHouse,
                                      child: const Text('Unirme a la Casa'),
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ],
                        if (!state.isRegistered && !reachedAnonLimit) ...[
                          const SizedBox(height: 16),
                          Semantics(
                            container: true,
                            identifier: 'onboarding_login_btn',
                            button: true,
                            child: TextButton.icon(
                              onPressed: () =>
                                  triggerOidcAccountFlow(context, ref),
                              icon: const Icon(Icons.login_rounded),
                              label: const Text('Ya tengo cuenta (WillIAM · Passkey / SSO)'),
                            ),
                          ),
                        ],
                        if (state.isAdmin) ...[
                          const SizedBox(height: 8),
                          Semantics(
                            container: true,
                            identifier: 'onboarding_admin_bo_btn',
                            button: true,
                            child: OutlinedButton.icon(
                              onPressed: () => context.go('/admin-bo'),
                              icon: const Icon(Icons.admin_panel_settings_rounded),
                              label: const Text('Panel de Administración (/admin-bo)'),
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
