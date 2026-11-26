import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/models/models.dart';
import '../houses/house_providers.dart';

class AdminInvitationsSection extends ConsumerStatefulWidget {
  final ValueChanged<String> onOpenUser;

  const AdminInvitationsSection({super.key, required this.onOpenUser});

  @override
  ConsumerState<AdminInvitationsSection> createState() =>
      _AdminInvitationsSectionState();
}

class _AdminInvitationsSectionState
    extends ConsumerState<AdminInvitationsSection> {
  static const List<({String value, String label})> _dateRanges = [
    (value: 'all', label: 'Todas las fechas'),
    (value: 'year', label: 'Último año'),
    (value: '6months', label: 'Últimos 6 meses'),
    (value: '3months', label: 'Últimos 3 meses'),
    (value: 'month', label: 'Último mes'),
    (value: 'week', label: 'Última semana'),
    (value: '24h', label: 'Últimas 24 horas'),
  ];

  bool _loading = true;
  bool _creating = false;
  bool _sendingEmails = false;
  String? _error;
  String? _feedback;

  String _dateRange = 'all';
  bool _showAllStatuses = false;
  Set<String> _selectedStatuses = {'Active'};

  bool _isIndefinite = false;
  late final TextEditingController _expiresCtrl;
  final TextEditingController _emailsCtrl = TextEditingController();

  List<PlatformInvitationDto> _invitations = const [];
  PlatformInvitationDto? _activeShare;

  @override
  void initState() {
    super.initState();
    final defaultExpiry = DateTime.now().add(const Duration(days: 7));
    _expiresCtrl = TextEditingController(text: _formatInputDate(defaultExpiry));
    Future.microtask(_load);
  }

  @override
  void dispose() {
    _expiresCtrl.dispose();
    _emailsCtrl.dispose();
    super.dispose();
  }

  static String _formatInputDate(DateTime dt) {
    String two(int v) => v.toString().padLeft(2, '0');
    return '${dt.year}-${two(dt.month)}-${two(dt.day)} ${two(dt.hour)}:${two(dt.minute)}';
  }

  static String _formatDisplayDate(DateTime dt) {
    final l = dt.toLocal();
    return _formatInputDate(l);
  }

  String get _baseOrigin {
    final uri = Uri.base;
    if (uri.scheme == 'http' || uri.scheme == 'https') {
      return uri.origin;
    }
    return 'http://localhost:8081';
  }

  String _inviteUrl(String code) => '$_baseOrigin/invitation-redeem/$code';

  String get _statusQuery =>
      _showAllStatuses ? 'All' : _selectedStatuses.join(',');

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final list = await ref
          .read(apiClientProvider)
          .adminGetPlatformInvitations(
            dateRange: _dateRange,
            statuses: _statusQuery,
          );
      if (!mounted) return;
      setState(() {
        _invitations = list;
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

  DateTime? _parseExpiryInput() {
    final raw = _expiresCtrl.text.trim();
    if (raw.isEmpty) return null;
    final normalized = raw.contains('T') ? raw : raw.replaceFirst(' ', 'T');
    return DateTime.tryParse(normalized);
  }

  Future<void> _pickExpiryDate() async {
    final initial = _parseExpiryInput() ?? DateTime.now().add(const Duration(days: 7));
    final date = await showDatePicker(
      context: context,
      initialDate: initial.isAfter(DateTime.now()) ? initial : DateTime.now().add(const Duration(days: 1)),
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 3650)),
    );
    if (date == null || !mounted) return;
    final updated = DateTime(date.year, date.month, date.day, initial.hour, initial.minute);
    setState(() {
      _isIndefinite = false;
      _expiresCtrl.text = _formatInputDate(updated);
    });
  }

  Future<void> _createInvitation() async {
    setState(() {
      _creating = true;
      _error = null;
      _feedback = null;
    });
    try {
      DateTime? expiresAt;
      if (!_isIndefinite) {
        expiresAt = _parseExpiryInput();
        if (expiresAt == null) {
          throw StateError('Formato de fecha inválido (usa YYYY-MM-DD HH:mm).');
        }
      }
      final created = await ref
          .read(apiClientProvider)
          .adminCreatePlatformInvitation(
            expiresAt: expiresAt,
            isIndefinite: _isIndefinite,
          );
      if (!mounted) return;
      setState(() {
        _creating = false;
        _activeShare = created;
        _feedback = 'Invitación ${created.code} generada correctamente.';
      });
      await _load();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _creating = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _cancelInvitation(PlatformInvitationDto inv) async {
    setState(() {
      _error = null;
      _feedback = null;
    });
    try {
      await ref.read(apiClientProvider).adminCancelPlatformInvitation(inv.id);
      if (!mounted) return;
      setState(() {
        if (_activeShare?.id == inv.id) _activeShare = null;
        _feedback = 'Invitación ${inv.code} cancelada.';
      });
      await _load();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    }
  }

  Future<void> _renewInvitation(
    PlatformInvitationDto inv, {
    int days = 7,
    bool isIndefinite = false,
  }) async {
    setState(() {
      _error = null;
      _feedback = null;
    });
    try {
      final updated = await ref
          .read(apiClientProvider)
          .adminRenewPlatformInvitation(
            inv.id,
            days: days,
            isIndefinite: isIndefinite,
          );
      if (!mounted) return;
      setState(() {
        if (_activeShare?.id == updated.id) _activeShare = updated;
        _feedback = isIndefinite
            ? 'Invitación ${updated.code} renovada indefinidamente.'
            : 'Invitación ${updated.code} renovada (+$days días).';
      });
      await _load();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    }
  }

  Future<void> _reactivateInvitation(PlatformInvitationDto inv, int days) async {
    setState(() {
      _error = null;
      _feedback = null;
    });
    try {
      final updated = await ref
          .read(apiClientProvider)
          .adminReactivatePlatformInvitation(inv.id, days: days);
      if (!mounted) return;
      setState(() {
        _activeShare = updated;
        _feedback =
            'Invitación ${updated.code} reactivada por $days día(s) desde ahora.';
      });
      await _load();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    }
  }

  Future<void> _copyUrl(PlatformInvitationDto inv) async {
    final url = _inviteUrl(inv.code);
    await Clipboard.setData(ClipboardData(text: url));
    if (!mounted) return;
    setState(() => _feedback = 'URL copiada: $url');
  }

  Future<void> _shareWhatsApp(PlatformInvitationDto inv) async {
    final url = _inviteUrl(inv.code);
    final waUri = Uri.parse(
      'https://wa.me/?text=${Uri.encodeComponent("Te invito a Babasketet con el código ${inv.code}: $url")}',
    );
    await launchUrl(waUri, mode: LaunchMode.externalApplication);
  }

  Future<void> _sendByEmail(PlatformInvitationDto inv) async {
    final emails = _emailsCtrl.text
        .split('\n')
        .map((line) => line.trim())
        .where((line) => line.isNotEmpty)
        .toList();
    if (emails.isEmpty) {
      setState(() => _error = 'Escribe al menos una dirección de correo (una por línea).');
      return;
    }

    setState(() {
      _sendingEmails = true;
      _error = null;
      _feedback = null;
    });
    try {
      final res = await ref
          .read(apiClientProvider)
          .adminSendPlatformInvitationEmails(
            inv.id,
            emails: emails,
            baseUrl: _baseOrigin,
          );
      if (!mounted) return;
      setState(() {
        _sendingEmails = false;
        _emailsCtrl.clear();
        _feedback = res.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _sendingEmails = false;
        _error = e.toString();
      });
    }
  }

  void _toggleStatus(String status) {
    setState(() {
      if (_showAllStatuses) {
        _showAllStatuses = false;
        _selectedStatuses = {status};
      } else if (_selectedStatuses.contains(status)) {
        final next = Set<String>.from(_selectedStatuses)..remove(status);
        if (next.isEmpty) {
          _showAllStatuses = true;
        } else {
          _selectedStatuses = next;
        }
      } else {
        _selectedStatuses = {..._selectedStatuses, status};
      }
    });
    _load();
  }

  @override
  Widget build(BuildContext context) {
    final shareInv = _activeShare;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        const Text(
          'Sistema de Invitaciones',
          style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 12),
        if (_error != null)
          Container(
            margin: const EdgeInsets.only(bottom: 12),
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: const Color(0xFFFEE2E2),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Text(_error!, style: const TextStyle(color: Color(0xFF991B1B))),
          ),
        if (_feedback != null)
          Semantics(
            container: true,
            identifier: 'admin_inv_feedback',
            child: Container(
              margin: const EdgeInsets.only(bottom: 12),
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFFDCFCE7),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(_feedback!,
                  style: const TextStyle(color: Color(0xFF166534))),
            ),
          ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Crear una invitación',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 12,
                  runSpacing: 10,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    SizedBox(
                      width: 240,
                      child: Semantics(
                        container: true,
                        identifier: 'admin_inv_expires_input',
                        textField: true,
                        child: TextFormField(
                          controller: _expiresCtrl,
                          enabled: !_isIndefinite,
                          decoration: InputDecoration(
                            labelText: 'Fecha de expiración (defecto +7d)',
                            border: const OutlineInputBorder(),
                            suffixIcon: IconButton(
                              tooltip: 'Elegir fecha',
                              icon: const Icon(Icons.calendar_today_rounded),
                              onPressed: _pickExpiryDate,
                            ),
                          ),
                        ),
                      ),
                    ),
                    Semantics(
                      container: true,
                      identifier: 'admin_inv_indefinite_switch',
                      toggled: _isIndefinite,
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Switch(
                            value: _isIndefinite,
                            onChanged: (v) => setState(() => _isIndefinite = v),
                          ),
                          const Text('Indefinida'),
                        ],
                      ),
                    ),
                    Semantics(
                      container: true,
                      identifier: 'admin_inv_create_btn',
                      button: true,
                      child: FilledButton.icon(
                        onPressed: _creating ? null : _createInvitation,
                        icon: const Icon(Icons.add_link_rounded),
                        label: const Text('Generar invitación'),
                      ),
                    ),
                  ],
                ),
                if (shareInv != null) ...[
                  const Divider(height: 28),
                  Semantics(
                    container: true,
                    identifier: 'admin_inv_share_box',
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Invitación lista · Código: ${shareInv.code}',
                          style: const TextStyle(
                              fontWeight: FontWeight.bold, fontSize: 15),
                        ),
                        const SizedBox(height: 4),
                        SelectableText(
                          _inviteUrl(shareInv.code),
                          style: const TextStyle(
                              fontFamily: 'monospace', fontSize: 13),
                        ),
                        const SizedBox(height: 10),
                        Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: [
                            Semantics(
                              container: true,
                              identifier: 'admin_inv_copy_url_btn',
                              button: true,
                              child: OutlinedButton.icon(
                                onPressed: () => _copyUrl(shareInv),
                                icon: const Icon(Icons.copy_rounded, size: 18),
                                label: const Text('Copiar URL'),
                              ),
                            ),
                            Semantics(
                              container: true,
                              identifier: 'admin_inv_whatsapp_btn',
                              button: true,
                              child: OutlinedButton.icon(
                                onPressed: () => _shareWhatsApp(shareInv),
                                icon: const Icon(Icons.chat_rounded, size: 18),
                                label: const Text('Enviar por WhatsApp'),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 12),
                        Semantics(
                          container: true,
                          identifier: 'admin_inv_emails_input',
                          textField: true,
                          child: TextField(
                            controller: _emailsCtrl,
                            maxLines: 4,
                            decoration: const InputDecoration(
                              labelText:
                                  'Enviar por email (una dirección por línea)',
                              hintText: 'usuario1@ejemplo.com\nusuario2@ejemplo.com',
                              border: OutlineInputBorder(),
                            ),
                          ),
                        ),
                        const SizedBox(height: 8),
                        Semantics(
                          container: true,
                          identifier: 'admin_inv_send_email_btn',
                          button: true,
                          child: FilledButton.icon(
                            onPressed: _sendingEmails
                                ? null
                                : () => _sendByEmail(shareInv),
                            icon: const Icon(Icons.send_rounded, size: 18),
                            label: const Text('Enviar por email'),
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  spacing: 12,
                  runSpacing: 10,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    const Text(
                      'Lista de invitaciones',
                      style:
                          TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                    ),
                    Semantics(
                      container: true,
                      identifier: 'admin_inv_date_filter',
                      child: DropdownButton<String>(
                        value: _dateRange,
                        items: _dateRanges
                            .map((d) => DropdownMenuItem(
                                  value: d.value,
                                  child: Text(d.label),
                                ))
                            .toList(),
                        onChanged: (val) {
                          if (val == null) return;
                          setState(() => _dateRange = val);
                          _load();
                        },
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    Semantics(
                      container: true,
                      identifier: 'admin_inv_filter_all',
                      button: true,
                      selected: _showAllStatuses,
                      child: FilterChip(
                        label: const Text('Todas'),
                        selected: _showAllStatuses,
                        onSelected: (_) {
                          setState(() => _showAllStatuses = true);
                          _load();
                        },
                      ),
                    ),
                    _statusFilterChip('Activas', 'Active', 'admin_inv_filter_active'),
                    _statusFilterChip('Canjeadas', 'Redeemed', 'admin_inv_filter_redeemed'),
                    _statusFilterChip('Canceladas', 'Cancelled', 'admin_inv_filter_cancelled'),
                    _statusFilterChip('Expiradas', 'Expired', 'admin_inv_filter_expired'),
                  ],
                ),
                const SizedBox(height: 12),
                if (_loading) const LinearProgressIndicator(),
                if (!_loading && _invitations.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 16),
                    child: Text('No hay invitaciones para el filtro actual.'),
                  ),
                for (var i = 0; i < _invitations.length; i++)
                  _buildInvitationTile(i, _invitations[i]),
              ],
            ),
          ),
        ),
      ],
    );
  }

  Widget _statusFilterChip(String label, String value, String semanticId) {
    final active = !_showAllStatuses && _selectedStatuses.contains(value);
    return Semantics(
      container: true,
      identifier: semanticId,
      button: true,
      selected: active,
      child: FilterChip(
        label: Text(label),
        selected: active,
        onSelected: (_) => _toggleStatus(value),
      ),
    );
  }

  Widget _buildInvitationTile(int idx, PlatformInvitationDto inv) {
    final isActive = inv.status == 'Active';
    final canReactivate =
        inv.status == 'Expired' || inv.status == 'Cancelled';

    return Semantics(
      container: true,
      identifier: 'admin_inv_item_$idx',
      label: '${inv.code} · ${_statusLabel(inv.status)}',
      child: Container(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          border: Border.all(color: const Color(0xFFE2E8F0)),
          borderRadius: BorderRadius.circular(10),
          color: isActive ? const Color(0xFFFFFBF5) : Colors.white,
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 6,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                _statusBadge(inv.status),
                SelectableText(
                  inv.code,
                  style: const TextStyle(
                      fontWeight: FontWeight.bold, fontFamily: 'monospace'),
                ),
                Text(
                  'Creada: ${_formatDisplayDate(inv.createdAt)}',
                  style: const TextStyle(fontSize: 12, color: Colors.black54),
                ),
                Text(
                  inv.isIndefinite
                      ? 'Expira: Indefinida'
                      : 'Expira: ${inv.expiresAt != null ? _formatDisplayDate(inv.expiresAt!) : "—"}',
                  style: const TextStyle(fontSize: 12, color: Colors.black87),
                ),
              ],
            ),
            if (inv.redeemedBy != null) ...[
              const SizedBox(height: 6),
              Wrap(
                spacing: 8,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    'Canjeada por: ${inv.redeemedBy!.displayName}${inv.redeemedBy!.email != null ? " (${inv.redeemedBy!.email})" : ""}',
                    style: const TextStyle(fontSize: 13),
                  ),
                  Semantics(
                    container: true,
                    identifier: 'admin_inv_user_link_$idx',
                    button: true,
                    child: TextButton.icon(
                      onPressed: () => widget.onOpenUser(inv.redeemedBy!.userId),
                      icon: const Icon(Icons.person_search_rounded, size: 16),
                      label: const Text('Ver usuario'),
                    ),
                  ),
                ],
              ),
            ],
            const SizedBox(height: 8),
            Wrap(
              spacing: 8,
              runSpacing: 6,
              children: [
                if (isActive) ...[
                  Semantics(
                    container: true,
                    identifier: 'admin_inv_select_$idx',
                    button: true,
                    child: OutlinedButton(
                      onPressed: () => setState(() => _activeShare = inv),
                      child: const Text('Compartir / Email'),
                    ),
                  ),
                  Semantics(
                    container: true,
                    identifier: 'admin_inv_cancel_$idx',
                    button: true,
                    child: OutlinedButton(
                      onPressed: () => _cancelInvitation(inv),
                      child: const Text('Cancelar'),
                    ),
                  ),
                  Semantics(
                    container: true,
                    identifier: 'admin_inv_renew_$idx',
                    button: true,
                    child: PopupMenuButton<String>(
                      onSelected: (choice) {
                        if (choice == 'indefinite') {
                          _renewInvitation(inv, isIndefinite: true);
                        } else {
                          _renewInvitation(inv, days: int.parse(choice));
                        }
                      },
                      itemBuilder: (_) => const [
                        PopupMenuItem(value: '1', child: Text('Renovar +1 día')),
                        PopupMenuItem(value: '7', child: Text('Renovar +7 días')),
                        PopupMenuItem(value: '15', child: Text('Renovar +15 días')),
                        PopupMenuItem(value: '30', child: Text('Renovar +30 días')),
                        PopupMenuItem(
                            value: 'indefinite',
                            child: Text('Renovar indefinidamente')),
                      ],
                      child: const Chip(
                        avatar: Icon(Icons.update_rounded, size: 16),
                        label: Text('Renovar'),
                      ),
                    ),
                  ),
                ],
                if (canReactivate)
                  Semantics(
                    container: true,
                    identifier: 'admin_inv_reactivate_$idx',
                    button: true,
                    child: PopupMenuButton<int>(
                      onSelected: (days) => _reactivateInvitation(inv, days),
                      itemBuilder: (_) => const [
                        PopupMenuItem(value: 1, child: Text('Reactivar 1 día')),
                        PopupMenuItem(value: 7, child: Text('Reactivar 7 días')),
                        PopupMenuItem(value: 15, child: Text('Reactivar 15 días')),
                        PopupMenuItem(value: 30, child: Text('Reactivar 30 días')),
                      ],
                      child: const Chip(
                        avatar: Icon(Icons.restart_alt_rounded, size: 16),
                        label: Text('Reactivar'),
                      ),
                    ),
                  ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  static String _statusLabel(String status) => switch (status) {
        'Active' => 'Activa',
        'Redeemed' => 'Canjeada',
        'Cancelled' => 'Cancelada',
        _ => 'Expirada',
      };

  static Widget _statusBadge(String status) {
    final (label, bg, fg) = switch (status) {
      'Active' => ('Activa', const Color(0xFFDCFCE7), const Color(0xFF166534)),
      'Redeemed' => ('Canjeada', const Color(0xFFDBEAFE), const Color(0xFF1E40AF)),
      'Cancelled' => ('Cancelada', const Color(0xFFFEE2E2), const Color(0xFF991B1B)),
      _ => ('Expirada', const Color(0xFFFEF3C7), const Color(0xFF92400E)),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(
        label,
        style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: fg),
      ),
    );
  }
}
