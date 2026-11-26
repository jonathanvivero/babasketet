import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/models.dart';
import '../houses/house_providers.dart';

class InvitationRedeemPage extends ConsumerStatefulWidget {
  final String initialCode;

  const InvitationRedeemPage({super.key, this.initialCode = ''});

  @override
  ConsumerState<InvitationRedeemPage> createState() =>
      _InvitationRedeemPageState();
}

class _InvitationRedeemPageState extends ConsumerState<InvitationRedeemPage> {
  late final TextEditingController _codeCtrl;
  final TextEditingController _emailCtrl = TextEditingController();

  bool _checking = false;
  bool _requestingRenewal = false;
  bool _redeeming = false;
  bool _isAuthenticated = false;
  CheckPlatformInvitationResponseDto? _result;
  String? _error;
  String? _feedback;

  @override
  void initState() {
    super.initState();
    _codeCtrl = TextEditingController(text: widget.initialCode.trim());
    Future.microtask(_bootstrap);
  }

  @override
  void dispose() {
    _codeCtrl.dispose();
    _emailCtrl.dispose();
    super.dispose();
  }

  Future<void> _bootstrap() async {
    try {
      final token = await ref.read(localStorageProvider).getOidcToken();
      if (token != null && token.isNotEmpty) {
        final me = await ref.read(apiClientProvider).getMe();
        if (mounted) {
          setState(() => _isAuthenticated = me.isAuthenticated);
        }
      }
    } catch (_) {
      // Ignorado: el usuario actúa como visitante anónimo
    }

    if (_codeCtrl.text.trim().isNotEmpty) {
      await _checkCode();
    }
  }

  Future<void> _checkCode() async {
    final code = _codeCtrl.text.trim();
    if (code.isEmpty) {
      setState(() => _error = 'Introduce un código de invitación.');
      return;
    }

    setState(() {
      _checking = true;
      _error = null;
      _feedback = null;
    });

    try {
      final res =
          await ref.read(apiClientProvider).checkPlatformInvitation(code);
      if (!mounted) return;
      setState(() {
        _result = res;
        _checking = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _checking = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _registerOrRedeem() async {
    final res = _result;
    if (res == null || !res.canRedeem) return;

    setState(() {
      _redeeming = true;
      _error = null;
    });

    try {
      if (_isAuthenticated) {
        await ref.read(apiClientProvider).redeemPlatformInvitation(res.code);
        await ref.read(houseControllerProvider.notifier).bootstrap();
        if (!mounted) return;
        context.go('/onboarding');
        return;
      }

      await ref.read(localStorageProvider).setPendingInviteCode(res.code);
      final completedInPopup = await ref.read(oidcServiceProvider).startLogin();
      if (completedInPopup && mounted) {
        final pending =
            await ref.read(localStorageProvider).takePendingInviteCode();
        if (pending != null && pending.isNotEmpty) {
          await ref.read(apiClientProvider).redeemPlatformInvitation(pending);
        }
        await ref.read(apiClientProvider).claimAnonymousHouses();
        await ref.read(houseControllerProvider.notifier).bootstrap();
        if (!mounted) return;
        context.go('/onboarding');
      } else if (mounted) {
        setState(() => _redeeming = false);
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _redeeming = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _requestRenewal() async {
    final res = _result;
    if (res == null || !res.isExpired) return;

    setState(() {
      _requestingRenewal = true;
      _error = null;
    });

    try {
      final reply = await ref
          .read(apiClientProvider)
          .requestPlatformInvitationRenewal(
            res.code,
            requesterEmail: _emailCtrl.text.trim(),
          );
      if (!mounted) return;
      setState(() {
        _requestingRenewal = false;
        _result = null;
        _codeCtrl.clear();
        _emailCtrl.clear();
        _feedback =
            '${reply.message} Introduce un código renovado cuando lo recibas.';
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _requestingRenewal = false;
        _error = e.toString();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final res = _result;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Canjear Invitación · Babasketet'),
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
            constraints: const BoxConstraints(maxWidth: 480),
            child: Card(
              elevation: 2,
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Icon(Icons.card_giftcard_rounded,
                        size: 48, color: Color(0xFFD97706)),
                    const SizedBox(height: 10),
                    const Text(
                      'Introduce tu código de invitación',
                      textAlign: TextAlign.center,
                      style:
                          TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                    ),
                    const SizedBox(height: 16),
                    if (_feedback != null)
                      Semantics(
                        container: true,
                        identifier: 'invite_redeem_feedback',
                        child: Container(
                          margin: const EdgeInsets.only(bottom: 12),
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: const Color(0xFFDCFCE7),
                            borderRadius: BorderRadius.circular(10),
                          ),
                          child: Text(
                            _feedback!,
                            style: const TextStyle(color: Color(0xFF166534)),
                          ),
                        ),
                      ),
                    if (_error != null)
                      Container(
                        margin: const EdgeInsets.only(bottom: 12),
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: const Color(0xFFFEE2E2),
                          borderRadius: BorderRadius.circular(10),
                        ),
                        child: Text(
                          _error!,
                          style: const TextStyle(color: Color(0xFF991B1B)),
                        ),
                      ),
                    Semantics(
                      container: true,
                      identifier: 'invite_redeem_code_input',
                      textField: true,
                      child: TextField(
                        controller: _codeCtrl,
                        textCapitalization: TextCapitalization.characters,
                        decoration: const InputDecoration(
                          labelText: 'Código de invitación',
                          hintText: 'BBK-XXXXXXXX',
                          border: OutlineInputBorder(),
                        ),
                        onSubmitted: (_) => _checkCode(),
                      ),
                    ),
                    const SizedBox(height: 12),
                    Semantics(
                      container: true,
                      identifier: 'invite_redeem_check_btn',
                      button: true,
                      child: FilledButton.icon(
                        onPressed: _checking ? null : _checkCode,
                        icon: const Icon(Icons.verified_outlined),
                        label: const Text('Comprobar código'),
                      ),
                    ),
                    if (_checking) ...[
                      const SizedBox(height: 12),
                      const LinearProgressIndicator(),
                    ],
                    if (res != null) ...[
                      const Divider(height: 28),
                      Semantics(
                        container: true,
                        identifier: 'invite_redeem_status_banner',
                        child: Container(
                          padding: const EdgeInsets.all(14),
                          decoration: BoxDecoration(
                            color: res.canRedeem
                                ? const Color(0xFFF0FDF4)
                                : res.isExpired
                                    ? const Color(0xFFFFFBEB)
                                    : const Color(0xFFFEE2E2),
                            borderRadius: BorderRadius.circular(10),
                            border: Border.all(
                              color: res.canRedeem
                                  ? const Color(0xFF86EFAC)
                                  : res.isExpired
                                      ? const Color(0xFFFCD34D)
                                      : const Color(0xFFFCA5A5),
                            ),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Text(
                                res.message,
                                style: const TextStyle(
                                    fontWeight: FontWeight.w600, fontSize: 15),
                              ),
                              if (res.canRedeem) ...[
                                const SizedBox(height: 12),
                                Semantics(
                                  container: true,
                                  identifier: 'invite_redeem_register_btn',
                                  button: true,
                                  child: FilledButton.icon(
                                    onPressed: _redeeming
                                        ? null
                                        : _registerOrRedeem,
                                    icon: Icon(_isAuthenticated
                                        ? Icons.check_circle_rounded
                                        : Icons.person_add_alt_1_rounded),
                                    label: Text(_isAuthenticated
                                        ? 'Canjear invitación'
                                        : 'Registrarse para canjear'),
                                  ),
                                ),
                              ],
                              if (res.isExpired) ...[
                                const SizedBox(height: 12),
                                Semantics(
                                  container: true,
                                  identifier:
                                      'invite_redeem_renewal_email_input',
                                  textField: true,
                                  child: TextField(
                                    controller: _emailCtrl,
                                    keyboardType: TextInputType.emailAddress,
                                    decoration: const InputDecoration(
                                      labelText:
                                          'Tu correo electrónico (opcional)',
                                      hintText: 'tu@correo.com',
                                      border: OutlineInputBorder(),
                                    ),
                                  ),
                                ),
                                const SizedBox(height: 10),
                                Semantics(
                                  container: true,
                                  identifier:
                                      'invite_redeem_request_renewal_btn',
                                  button: true,
                                  child: FilledButton.icon(
                                    style: FilledButton.styleFrom(
                                      backgroundColor: const Color(0xFFD97706),
                                    ),
                                    onPressed: _requestingRenewal
                                        ? null
                                        : _requestRenewal,
                                    icon: const Icon(Icons.refresh_rounded),
                                    label: const Text(
                                        'Solicitar renovación de invitación'),
                                  ),
                                ),
                              ],
                            ],
                          ),
                        ),
                      ),
                    ],
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
