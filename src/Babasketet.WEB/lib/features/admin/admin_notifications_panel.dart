import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/models.dart';
import '../houses/house_providers.dart';

class AdminNotificationsPanel extends ConsumerStatefulWidget {
  final int refreshTrigger;

  const AdminNotificationsPanel({super.key, this.refreshTrigger = 0});

  @override
  ConsumerState<AdminNotificationsPanel> createState() =>
      _AdminNotificationsPanelState();
}

class _AdminNotificationsPanelState
    extends ConsumerState<AdminNotificationsPanel> {
  // Por defecto solo Nuevas; se puede filtrar por Todas, Nuevas, Leidas y/o Archivadas.
  Set<String> _selectedStatuses = {'New'};
  bool _showAll = false;
  int _page = 1;
  bool _loading = true;
  String? _error;
  AdminNotificationsPageDto? _data;

  @override
  void initState() {
    super.initState();
    Future.microtask(_load);
  }

  @override
  void didUpdateWidget(covariant AdminNotificationsPanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.refreshTrigger != widget.refreshTrigger) {
      _load();
    }
  }

  String get _statusQuery =>
      _showAll ? 'All' : _selectedStatuses.join(',');

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final res = await ref.read(apiClientProvider).adminGetNotifications(
            status: _statusQuery,
            page: _page,
            pageSize: 10,
          );
      if (!mounted) return;
      setState(() {
        _data = res;
        _page = res.page;
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

  Future<void> _setStatus(String id, String nextStatus) async {
    try {
      await ref
          .read(apiClientProvider)
          .adminUpdateNotificationStatus(id, nextStatus);
      await _load();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    }
  }

  void _toggleStatusFilter(String status) {
    setState(() {
      if (_showAll) {
        _showAll = false;
        _selectedStatuses = {status};
      } else if (_selectedStatuses.contains(status)) {
        final next = Set<String>.from(_selectedStatuses)..remove(status);
        if (next.isEmpty) {
          _showAll = true;
        } else {
          _selectedStatuses = next;
        }
      } else {
        _selectedStatuses = {..._selectedStatuses, status};
      }
      _page = 1;
    });
    _load();
  }

  Future<void> _openPopup(int initialIndex) async {
    final items = List<AdminNotificationDto>.from(_data?.items ?? const []);
    if (items.isEmpty || initialIndex < 0 || initialIndex >= items.length) {
      return;
    }

    await showDialog<void>(
      context: context,
      builder: (_) => _NotificationPopupDialog(
        initialItems: items,
        initialIndex: initialIndex,
        onMarkStatus: (id, status) =>
            ref.read(apiClientProvider).adminUpdateNotificationStatus(id, status),
      ),
    );
    if (mounted) {
      await _load();
    }
  }

  @override
  Widget build(BuildContext context) {
    final pageData = _data;
    final items = pageData?.items ?? const <AdminNotificationDto>[];

    return Semantics(
      container: true,
      identifier: 'admin_notifications_panel',
      child: Container(
        color: const Color(0xFFFFFBF5),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 12, 12, 6),
              child: Row(
                children: [
                  const Icon(Icons.notifications_active_rounded,
                      color: Color(0xFFD97706)),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Notificaciones (${pageData?.newCount ?? 0} nuevas)',
                      style: const TextStyle(
                          fontSize: 16, fontWeight: FontWeight.bold),
                    ),
                  ),
                  Semantics(
                    container: true,
                    identifier: 'admin_notif_refresh_btn',
                    button: true,
                    child: IconButton(
                      tooltip: 'Actualizar notificaciones',
                      icon: const Icon(Icons.refresh_rounded, size: 20),
                      onPressed: _load,
                    ),
                  ),
                ],
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Wrap(
                spacing: 6,
                runSpacing: 4,
                children: [
                  Semantics(
                    container: true,
                    identifier: 'admin_notif_filter_all',
                    button: true,
                    selected: _showAll,
                    child: FilterChip(
                      label: const Text('Todas'),
                      selected: _showAll,
                      onSelected: (_) {
                        setState(() {
                          _showAll = true;
                          _page = 1;
                        });
                        _load();
                      },
                    ),
                  ),
                  _statusChip('Nuevas', 'New', 'admin_notif_filter_new'),
                  _statusChip('Leídas', 'Read', 'admin_notif_filter_read'),
                  _statusChip(
                      'Archivadas', 'Archived', 'admin_notif_filter_archived'),
                ],
              ),
            ),
            if (_error != null)
              Padding(
                padding: const EdgeInsets.all(8),
                child: Text(_error!,
                    style: const TextStyle(color: Colors.red, fontSize: 12)),
              ),
            if (_loading) const LinearProgressIndicator(),
            Expanded(
              child: items.isEmpty
                  ? const Center(
                      child: Padding(
                        padding: EdgeInsets.all(16),
                        child: Text(
                          'No hay notificaciones en este filtro.',
                          textAlign: TextAlign.center,
                        ),
                      ),
                    )
                  : ListView.builder(
                      padding: const EdgeInsets.all(10),
                      itemCount: items.length,
                      itemBuilder: (context, idx) {
                        final n = items[idx];
                        return Semantics(
                          container: true,
                          identifier: 'admin_notif_item_$idx',
                          button: true,
                          label:
                              '${n.title} · ${_statusLabel(n.status)} · ${n.createdAt.toLocal().toString().substring(0, 16)}',
                          child: Card(
                            margin: const EdgeInsets.only(bottom: 8),
                            color: n.status == 'New'
                                ? const Color(0xFFFFF7ED)
                                : Colors.white,
                            child: InkWell(
                              borderRadius: BorderRadius.circular(12),
                              onTap: () => _openPopup(idx),
                              child: Padding(
                                padding: const EdgeInsets.all(10),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        _statusBadge(n.status),
                                        const SizedBox(width: 6),
                                        Expanded(
                                          child: Text(
                                            n.title,
                                            maxLines: 1,
                                            overflow: TextOverflow.ellipsis,
                                            style: const TextStyle(
                                                fontWeight: FontWeight.bold),
                                          ),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      n.body,
                                      maxLines: 2,
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(
                                          fontSize: 12, color: Colors.black87),
                                    ),
                                    const SizedBox(height: 4),
                                    Text(
                                      'Creada: ${_fmt(n.createdAt)}${n.readAt != null ? " · Leída: ${_fmt(n.readAt!)}" : ""}${n.archivedAt != null ? " · Archivada: ${_fmt(n.archivedAt!)}" : ""}',
                                      style: const TextStyle(
                                          fontSize: 11, color: Colors.black54),
                                    ),
                                    const SizedBox(height: 6),
                                    Wrap(
                                      spacing: 4,
                                      children: [
                                        if (n.status != 'Read')
                                          Semantics(
                                            container: true,
                                            identifier:
                                                'admin_notif_mark_read_$idx',
                                            button: true,
                                            child: TextButton(
                                              onPressed: () =>
                                                  _setStatus(n.id, 'Read'),
                                              child: const Text('Leída'),
                                            ),
                                          ),
                                        if (n.status != 'New')
                                          Semantics(
                                            container: true,
                                            identifier:
                                                'admin_notif_mark_new_$idx',
                                            button: true,
                                            child: TextButton(
                                              onPressed: () =>
                                                  _setStatus(n.id, 'New'),
                                              child: const Text('Nueva'),
                                            ),
                                          ),
                                        if (n.status != 'Archived')
                                          Semantics(
                                            container: true,
                                            identifier:
                                                'admin_notif_archive_$idx',
                                            button: true,
                                            child: TextButton(
                                              onPressed: () =>
                                                  _setStatus(n.id, 'Archived'),
                                              child: const Text('Archivar'),
                                            ),
                                          ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        );
                      },
                    ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: const BoxDecoration(
                border: Border(top: BorderSide(color: Color(0xFFE2E8F0))),
              ),
              child: Row(
                children: [
                  Semantics(
                    container: true,
                    identifier: 'admin_notif_prev_page',
                    button: true,
                    child: TextButton(
                      style: TextButton.styleFrom(
                        visualDensity: VisualDensity.compact,
                        padding: const EdgeInsets.symmetric(horizontal: 8),
                      ),
                      onPressed: (pageData?.page ?? 1) > 1
                          ? () {
                              setState(() => _page -= 1);
                              _load();
                            }
                          : null,
                      child: const Text('Anterior'),
                    ),
                  ),
                  Expanded(
                    child: Text(
                      'Pág. ${pageData?.page ?? 1} / ${pageData?.totalPages ?? 1} (${pageData?.totalCount ?? 0})',
                      textAlign: TextAlign.center,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(fontSize: 12),
                    ),
                  ),
                  Semantics(
                    container: true,
                    identifier: 'admin_notif_next_page',
                    button: true,
                    child: TextButton(
                      style: TextButton.styleFrom(
                        visualDensity: VisualDensity.compact,
                        padding: const EdgeInsets.symmetric(horizontal: 8),
                      ),
                      onPressed:
                          (pageData?.page ?? 1) < (pageData?.totalPages ?? 1)
                              ? () {
                                  setState(() => _page += 1);
                                  _load();
                                }
                              : null,
                      child: const Text('Siguiente'),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _statusChip(String label, String value, String semanticId) {
    final active = !_showAll && _selectedStatuses.contains(value);
    return Semantics(
      container: true,
      identifier: semanticId,
      button: true,
      selected: active,
      child: FilterChip(
        label: Text(label),
        selected: active,
        onSelected: (_) => _toggleStatusFilter(value),
      ),
    );
  }

  static Widget _statusBadge(String status) {
    final (label, bg, fg) = switch (status) {
      'New' => ('Nueva', const Color(0xFFFFEDD5), const Color(0xFF9A3412)),
      'Read' => ('Leída', const Color(0xFFDBEAFE), const Color(0xFF1E40AF)),
      _ => ('Archivada', const Color(0xFFE2E8F0), const Color(0xFF334155)),
    };
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Text(label,
          style: TextStyle(fontSize: 11, fontWeight: FontWeight.bold, color: fg)),
    );
  }

  static String _statusLabel(String status) => switch (status) {
        'New' => 'Nueva',
        'Read' => 'Leída',
        _ => 'Archivada',
      };

  static String _fmt(DateTime dt) =>
      dt.toLocal().toString().substring(0, 16);
}

class _NotificationPopupDialog extends StatefulWidget {
  final List<AdminNotificationDto> initialItems;
  final int initialIndex;
  final Future<AdminNotificationDto> Function(String id, String status)
      onMarkStatus;

  const _NotificationPopupDialog({
    required this.initialItems,
    required this.initialIndex,
    required this.onMarkStatus,
  });

  @override
  State<_NotificationPopupDialog> createState() =>
      _NotificationPopupDialogState();
}

class _NotificationPopupDialogState extends State<_NotificationPopupDialog> {
  late List<AdminNotificationDto> _items;
  late int _index;

  @override
  void initState() {
    super.initState();
    _items = List<AdminNotificationDto>.from(widget.initialItems);
    _index = widget.initialIndex;
    _ensureCurrentMarkedRead();
  }

  Future<void> _ensureCurrentMarkedRead() async {
    final current = _items[_index];
    if (current.status == 'New') {
      try {
        final updated = await widget.onMarkStatus(current.id, 'Read');
        if (!mounted) return;
        setState(() {
          _items[_index] = updated;
        });
      } catch (_) {
        // Ignorado si falla la sincronización puntual
      }
    }
  }

  Future<void> _changeStatus(String status) async {
    final current = _items[_index];
    final updated = await widget.onMarkStatus(current.id, status);
    if (!mounted) return;
    setState(() {
      _items[_index] = updated;
    });
  }

  void _goTo(int nextIndex) {
    setState(() => _index = nextIndex);
    _ensureCurrentMarkedRead();
  }

  @override
  Widget build(BuildContext context) {
    final n = _items[_index];
    return Semantics(
      container: true,
      identifier: 'admin_notif_detail_dialog',
      child: AlertDialog(
        title: Text(n.title),
        content: SizedBox(
          width: 460,
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Wrap(
                  spacing: 8,
                  runSpacing: 6,
                  children: [
                    _AdminNotificationsPanelState._statusBadge(n.status),
                    Chip(label: Text('Categoría: ${n.category}')),
                  ],
                ),
                const SizedBox(height: 12),
                Text(n.body, style: const TextStyle(fontSize: 15)),
                const Divider(height: 24),
                Text('Creación: ${n.createdAt.toLocal().toString().substring(0, 19)}'),
                Text('Lectura: ${n.readAt != null ? n.readAt!.toLocal().toString().substring(0, 19) : "—"}'),
                Text('Archivo: ${n.archivedAt != null ? n.archivedAt!.toLocal().toString().substring(0, 19) : "—"}'),
              ],
            ),
          ),
        ),
        actions: [
          Semantics(
            container: true,
            identifier: 'admin_notif_dialog_prev',
            button: true,
            child: TextButton(
              onPressed: _index > 0 ? () => _goTo(_index - 1) : null,
              child: const Text('← Anterior'),
            ),
          ),
          Semantics(
            container: true,
            identifier: 'admin_notif_dialog_next',
            button: true,
            child: TextButton(
              onPressed:
                  _index < _items.length - 1 ? () => _goTo(_index + 1) : null,
              child: const Text('Siguiente →'),
            ),
          ),
          if (n.status != 'New')
            TextButton(
              onPressed: () => _changeStatus('New'),
              child: const Text('Marcar nueva'),
            ),
          if (n.status != 'Archived')
            TextButton(
              onPressed: () => _changeStatus('Archived'),
              child: const Text('Archivar'),
            ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Cerrar'),
          ),
        ],
      ),
    );
  }
}
