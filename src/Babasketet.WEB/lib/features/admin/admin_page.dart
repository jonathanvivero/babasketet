import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/models.dart';
import '../../core/widgets/long_press_and_dot.dart';
import '../houses/house_providers.dart';
import 'admin_settings_section.dart';

class AdminBackofficePage extends ConsumerStatefulWidget {
  const AdminBackofficePage({super.key});

  @override
  ConsumerState<AdminBackofficePage> createState() =>
      _AdminBackofficePageState();
}

class _AdminBackofficePageState extends ConsumerState<AdminBackofficePage> {
  bool _checkingAccess = true;
  bool _loadingData = false;
  MeResponse? _me;
  String? _error;

  int _selectedTab = 0;

  List<AdminHouseSummaryDto> _houses = const [];
  AdminHouseDetailDto? _selectedHouse;

  List<AdminUserSummaryDto> _users = const [];
  AdminUserDetailDto? _selectedUser;

  List<AdminCatalogItemDto> _items = const [];
  final _itemSearchCtrl = TextEditingController();

  @override
  void initState() {
    super.initState();
    Future.microtask(_loadInitial);
  }

  @override
  void dispose() {
    _itemSearchCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadInitial() async {
    setState(() {
      _checkingAccess = true;
      _error = null;
    });

    try {
      final api = ref.read(apiClientProvider);
      final me = await api.getMe();
      if (!mounted) return;
      setState(() {
        _me = me;
        _checkingAccess = false;
      });
      if (me.isAdmin) {
        await _refreshAllData();
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _checkingAccess = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _refreshAllData() async {
    setState(() {
      _loadingData = true;
      _error = null;
    });
    try {
      final api = ref.read(apiClientProvider);
      final results = await Future.wait([
        api.adminGetHouses(),
        api.adminGetUsers(),
        api.adminGetItems(query: _itemSearchCtrl.text),
      ]);
      if (!mounted) return;
      setState(() {
        _houses = results[0] as List<AdminHouseSummaryDto>;
        _users = results[1] as List<AdminUserSummaryDto>;
        _items = results[2] as List<AdminCatalogItemDto>;
        _loadingData = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingData = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _openHouseDetail(String houseId) async {
    setState(() => _loadingData = true);
    try {
      final detail = await ref.read(apiClientProvider).adminGetHouseDetail(houseId);
      if (!mounted) return;
      setState(() {
        _selectedHouse = detail;
        _loadingData = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingData = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _openUserDetail(String userId) async {
    setState(() => _loadingData = true);
    try {
      final detail = await ref.read(apiClientProvider).adminGetUserDetail(userId);
      if (!mounted) return;
      setState(() {
        _selectedUser = detail;
        _loadingData = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loadingData = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _searchItems(String query) async {
    try {
      final items = await ref.read(apiClientProvider).adminGetItems(query: query);
      if (!mounted) return;
      setState(() => _items = items);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    final me = _me;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Babasketet · Administración (/admin-bo)'),
        backgroundColor: const Color(0xFF0F172A),
        foregroundColor: Colors.white,
        leading: Semantics(
          container: true,
          identifier: 'admin_back_app_btn',
          button: true,
          child: IconButton(
            tooltip: 'Volver a Babasketet',
            icon: const Icon(Icons.arrow_back),
            onPressed: () => context.go('/'),
          ),
        ),
        actions: [
          if (me?.isAdmin == true)
            Semantics(
              container: true,
              identifier: 'admin_refresh_btn',
              button: true,
              child: IconButton(
                tooltip: 'Recargar datos',
                icon: const Icon(Icons.refresh_rounded),
                onPressed: _refreshAllData,
              ),
            ),
        ],
      ),
      body: _buildBody(context, me),
    );
  }

  Widget _buildBody(BuildContext context, MeResponse? me) {
    if (_checkingAccess) {
      return const Center(child: CircularProgressIndicator());
    }

    if (me == null || !me.isAuthenticated) {
      return Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 440),
          child: Card(
            margin: const EdgeInsets.all(24),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.admin_panel_settings_rounded,
                      size: 56, color: Color(0xFFD97706)),
                  const SizedBox(height: 12),
                  const Text(
                    'Panel de Administración',
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Solo los usuarios autenticados con rol Admin pueden acceder a /admin-bo.',
                    textAlign: TextAlign.center,
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: 12),
                    Text(_error!, style: const TextStyle(color: Colors.red)),
                  ],
                  const SizedBox(height: 20),
                  Semantics(
                    container: true,
                    identifier: 'admin_login_btn',
                    button: true,
                    child: FilledButton.icon(
                      icon: const Icon(Icons.login_rounded),
                      label: const Text('Iniciar sesión con WillIAM'),
                      onPressed: () => ref.read(oidcServiceProvider).startLogin(),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    if (!me.isAdmin) {
      return Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 440),
          child: Card(
            margin: const EdgeInsets.all(24),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.gpp_bad_rounded, size: 56, color: Colors.red),
                  const SizedBox(height: 12),
                  const Text(
                    '403 · Acceso denegado',
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
                  ),
                  const SizedBox(height: 8),
                  Text(
                    'Tu cuenta (${me.email ?? me.displayName ?? me.zitadelUserId}) tiene roles: ${me.effectiveRoles.join(', ')}. Solo el rol Admin puede acceder a este panel.',
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 20),
                  FilledButton(
                    onPressed: () => context.go('/'),
                    child: const Text('Volver a Babasketet'),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    return Column(
      children: [
        Container(
          width: double.infinity,
          color: const Color(0xFFF8FAFC),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
          child: Wrap(
            spacing: 8,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              _navChip(0, Icons.home_work_rounded, 'Casas (${_houses.length})', 'admin_tab_houses'),
              _navChip(1, Icons.people_alt_rounded, 'Usuarios (${_users.length})', 'admin_tab_users'),
              _navChip(2, Icons.inventory_2_rounded, 'Ítems (${_items.length})', 'admin_tab_items'),
              _navChip(3, Icons.settings_rounded, 'Configuraciones', 'admin_tab_settings'),
              const SizedBox(width: 8),
              Chip(
                avatar: const Icon(Icons.verified_user_rounded, size: 16),
                label: Text('${me.email ?? me.displayName} (${me.effectiveRoles.join(" + ")})'),
              ),
            ],
          ),
        ),
        if (_error != null)
          MaterialBanner(
            backgroundColor: const Color(0xFFFEE2E2),
            content: Text(_error!, style: const TextStyle(color: Color(0xFF991B1B))),
            actions: [
              TextButton(
                onPressed: () => setState(() => _error = null),
                child: const Text('Cerrar'),
              ),
            ],
          ),
        if (_loadingData) const LinearProgressIndicator(),
        Expanded(
          child: switch (_selectedTab) {
            0 => _selectedHouse != null
                ? _HouseDetailView(
                    detail: _selectedHouse!,
                    onBack: () => setState(() => _selectedHouse = null),
                    onOpenUser: (appUserId) {
                      if (appUserId != null) {
                        setState(() {
                          _selectedTab = 1;
                          _selectedHouse = null;
                        });
                        _openUserDetail(appUserId);
                      }
                    },
                  )
                : _HousesListView(
                    houses: _houses,
                    onSelect: (h) => _openHouseDetail(h.id),
                  ),
            1 => _selectedUser != null
                ? _UserEditorView(
                    user: _selectedUser!,
                    onBack: () => setState(() => _selectedUser = null),
                    onSaved: (updated) async {
                      setState(() => _selectedUser = updated);
                      await _refreshAllData();
                    },
                  )
                : _UsersListView(
                    users: _users,
                    onSelect: (u) => _openUserDetail(u.id),
                  ),
            2 => _ItemsListView(
                items: _items,
                controller: _itemSearchCtrl,
                onSearchChanged: _searchItems,
              ),
            _ => AdminSettingsSection(adminEmail: me.email),
          },
        ),
      ],
    );
  }

  Widget _navChip(int index, IconData icon, String label, String semanticId) {
    final selected = _selectedTab == index;
    return Semantics(
      container: true,
      identifier: semanticId,
      button: true,
      selected: selected,
      child: ChoiceChip(
        selected: selected,
        avatar: Icon(icon, size: 18),
        label: Text(label),
        onSelected: (_) => setState(() {
          _selectedTab = index;
          _selectedHouse = null;
          _selectedUser = null;
        }),
      ),
    );
  }
}

class _HousesListView extends StatelessWidget {
  final List<AdminHouseSummaryDto> houses;
  final void Function(AdminHouseSummaryDto) onSelect;

  const _HousesListView({required this.houses, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    if (houses.isEmpty) {
      return const Center(child: Text('No hay casas registradas todavía.'));
    }

    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: houses.length,
      itemBuilder: (context, idx) {
        final h = houses[idx];
        return Semantics(
          container: true,
          identifier: 'admin_house_${h.id}',
          button: true,
          label: '${h.name} · ${h.memberCount} usuarios · ${h.totalProductsCount} ítems totales',
          child: Card(
            margin: const EdgeInsets.only(bottom: 10),
            child: ListTile(
              leading: const CircleAvatar(
                backgroundColor: Color(0xFFFFEDD5),
                child: Icon(Icons.home_rounded, color: Color(0xFFD97706)),
              ),
              title: Text(h.name, style: const TextStyle(fontWeight: FontWeight.bold)),
              subtitle: Text(
                'Supermercado: ${h.activeStoreName} · Usuarios: ${h.memberCount} · Ítems totales: ${h.totalProductsCount} · Lista: ${h.listItemsCount} · Cesta: ${h.basketItemsCount} · Compras: ${h.purchasesCount}',
              ),
              trailing: const Icon(Icons.chevron_right_rounded),
              onTap: () => onSelect(h),
            ),
          ),
        );
      },
    );
  }
}

class _HouseDetailView extends StatelessWidget {
  final AdminHouseDetailDto detail;
  final VoidCallback onBack;
  final void Function(String? appUserId) onOpenUser;

  const _HouseDetailView({
    required this.detail,
    required this.onBack,
    required this.onOpenUser,
  });

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Row(
          children: [
            Semantics(
              container: true,
              identifier: 'admin_house_detail_back_btn',
              button: true,
              child: OutlinedButton.icon(
                icon: const Icon(Icons.arrow_back),
                label: const Text('Todas las casas'),
                onPressed: onBack,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(
                detail.name,
                style: Theme.of(context).textTheme.headlineSmall,
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _metricChip('Usuarios', '${detail.members.length}'),
            _metricChip('Ítems totales', '${detail.totalProductsCount}'),
            _metricChip('En Lista', '${detail.listItems.length}'),
            _metricChip('En Cesta', '${detail.basketItems.length}'),
            _metricChip('Compras', '${detail.purchases.length}'),
            _metricChip('Supermercado activo', detail.activeStoreName),
          ],
        ),
        const SizedBox(height: 20),
        _sectionTitle('Usuarios de la casa (${detail.members.length})'),
        if (detail.members.isEmpty)
          const Text('Sin miembros')
        else
          ...detail.members.map(
            (m) => Card(
              child: ListTile(
                leading: Icon(
                  m.houseRole == 'owner'
                      ? Icons.workspace_premium_rounded
                      : (m.isAuthenticated
                          ? Icons.person_rounded
                          : Icons.person_outline_rounded),
                  color: m.houseRole == 'owner' ? const Color(0xFFD97706) : null,
                ),
                title: Text('${m.displayName} (${m.houseRole.toUpperCase()})'),
                subtitle: Text(
                  'Rol global: ${m.globalRole}${m.isAdmin ? "+Admin" : ""} · ${m.isAuthenticated ? "Autenticado" : "No autenticado"} · Estado: ${m.userStatus}${m.email != null ? " · ${m.email}" : ""}',
                ),
                trailing: m.appUserId != null
                    ? const Icon(Icons.edit_outlined)
                    : null,
                onTap: m.appUserId != null ? () => onOpenUser(m.appUserId) : null,
              ),
            ),
          ),
        const SizedBox(height: 16),
        _sectionTitle('Ítems actuales en Lista (${detail.listItems.length})'),
        if (detail.listItems.isEmpty)
          const Card(
            child: ListTile(title: Text('La lista está vacía.')),
          )
        else
          ...detail.listItems.map(
            (i) => Card(
              child: ListTile(
                leading: ProductTypeDot(fillHex: i.productTypeFillHex ?? '#CBD5E1'),
                title: Text(i.name),
                subtitle: Text(
                    'Tipo: ${i.productTypeName ?? "Sin tipo"} · Formato: ${i.format}'),
              ),
            ),
          ),
        const SizedBox(height: 16),
        _sectionTitle('Ítems actuales en Cesta (${detail.basketItems.length})'),
        if (detail.basketItems.isEmpty)
          const Card(
            child: ListTile(title: Text('La cesta está vacía.')),
          )
        else
          ...detail.basketItems.map(
            (i) => Card(
              child: ListTile(
                leading: ProductTypeDot(fillHex: i.productTypeFillHex ?? '#CBD5E1'),
                title: Text(i.name),
                subtitle: Text(
                    'Tipo: ${i.productTypeName ?? "Sin tipo"} · Formato: ${i.format} · Cantidad: ${i.quantity?.toStringAsFixed(0) ?? "1"}'),
              ),
            ),
          ),
        const SizedBox(height: 16),
        _sectionTitle('Compras y sus ítems (${detail.purchases.length})'),
        if (detail.purchases.isEmpty)
          const Card(
            child: ListTile(title: Text('Aún no hay compras cerradas en esta casa.')),
          )
        else
          ...detail.purchases.map(
            (p) => Card(
              child: ExpansionTile(
                leading: const Icon(Icons.receipt_long_rounded),
                title: Text(
                    '${p.storeName} · ${p.effectiveTotal.toStringAsFixed(2)} €'),
                subtitle: Text(
                    '${p.purchasedAt.toLocal().toString().substring(0, 16)} · ${p.items.length} ítems'),
                children: p.items
                    .map(
                      (pi) => ListTile(
                        dense: true,
                        title: Text(pi.productName),
                        subtitle: Text(
                            '${pi.quantity.toStringAsFixed(0)} ${pi.format} × ${pi.unitPrice.toStringAsFixed(2)} €'),
                        trailing: Text('${pi.lineTotal.toStringAsFixed(2)} €',
                            style: const TextStyle(fontWeight: FontWeight.w600)),
                      ),
                    )
                    .toList(),
              ),
            ),
          ),
      ],
    );
  }

  Widget _metricChip(String label, String value) => Chip(
        label: Text('$label: $value',
            style: const TextStyle(fontWeight: FontWeight.w600)),
      );

  Widget _sectionTitle(String title) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
        child: Text(title,
            style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
      );
}

class _UsersListView extends StatelessWidget {
  final List<AdminUserSummaryDto> users;
  final void Function(AdminUserSummaryDto) onSelect;

  const _UsersListView({required this.users, required this.onSelect});

  @override
  Widget build(BuildContext context) {
    if (users.isEmpty) {
      return const Center(child: Text('No hay usuarios registrados todavía.'));
    }

    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: users.length,
      itemBuilder: (context, idx) {
        final u = users[idx];
        final isBlocked = u.status == 'Blocked';
        return Semantics(
          container: true,
          identifier: 'admin_user_${u.id}',
          button: true,
          label:
              '${u.displayName} · ${u.effectiveRoles.join("+")} · ${u.status}',
          child: Card(
            margin: const EdgeInsets.only(bottom: 10),
            child: ListTile(
              leading: CircleAvatar(
                backgroundColor:
                    isBlocked ? const Color(0xFFFEE2E2) : const Color(0xFFDCFCE7),
                child: Icon(
                  u.isAdmin
                      ? Icons.security_rounded
                      : (u.isAuthenticated
                          ? Icons.person_rounded
                          : Icons.person_outline_rounded),
                  color: isBlocked
                      ? const Color(0xFF991B1B)
                      : const Color(0xFF166534),
                ),
              ),
              title: Text(u.displayName,
                  style: const TextStyle(fontWeight: FontWeight.bold)),
              subtitle: Text(
                'Roles: ${u.effectiveRoles.join(" + ")} · Estado: ${isBlocked ? "Bloqueado" : "Activo"} · ${u.isAuthenticated ? "Autenticado" : "No autenticado"} · Casas: ${u.housesCount}${u.email != null ? " · ${u.email}" : ""}',
              ),
              trailing: const Icon(Icons.edit_rounded),
              onTap: () => onSelect(u),
            ),
          ),
        );
      },
    );
  }
}

class _UserEditorView extends ConsumerStatefulWidget {
  final AdminUserDetailDto user;
  final VoidCallback onBack;
  final Future<void> Function(AdminUserDetailDto updated) onSaved;

  const _UserEditorView({
    required this.user,
    required this.onBack,
    required this.onSaved,
  });

  @override
  ConsumerState<_UserEditorView> createState() => _UserEditorViewState();
}

class _UserEditorViewState extends ConsumerState<_UserEditorView> {
  late String _status;
  late bool _isAdmin;
  late TextEditingController _nameCtrl;
  late Map<String, bool> _houseOwners;
  late List<({TextEditingController key, TextEditingController value})> _propRows;
  bool _saving = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _syncFromWidget();
  }

  @override
  void didUpdateWidget(covariant _UserEditorView oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.user.id != widget.user.id ||
        oldWidget.user.lastSeenAt != widget.user.lastSeenAt) {
      _disposeControllers();
      _syncFromWidget();
    }
  }

  void _syncFromWidget() {
    final u = widget.user;
    _status = u.status;
    _isAdmin = u.isAdmin;
    _nameCtrl = TextEditingController(text: u.displayName);
    _houseOwners = {
      for (final h in u.houses) h.houseId: h.houseRole == 'owner',
    };
    _propRows = u.customProperties.entries
        .map((e) => (
              key: TextEditingController(text: e.key),
              value: TextEditingController(text: e.value),
            ))
        .toList();
  }

  void _disposeControllers() {
    _nameCtrl.dispose();
    for (final row in _propRows) {
      row.key.dispose();
      row.value.dispose();
    }
  }

  @override
  void dispose() {
    _disposeControllers();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() {
      _saving = true;
      _error = null;
    });

    try {
      final props = <String, String>{};
      for (final row in _propRows) {
        final k = row.key.text.trim();
        if (k.isNotEmpty) {
          props[k] = row.value.text.trim();
        }
      }

      final houseRoles = widget.user.houses
          .map((h) => (houseId: h.houseId, isOwner: _houseOwners[h.houseId] ?? false))
          .toList();

      final updated = await ref.read(apiClientProvider).adminUpdateUser(
            widget.user.id,
            status: _status,
            isAdmin: _isAdmin,
            displayName: _nameCtrl.text.trim(),
            customProperties: props,
            houseRoles: houseRoles,
          );

      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Usuario actualizado correctamente.')),
      );
      await widget.onSaved(updated);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final u = widget.user;
    final isGuest = u.globalRole == 'Guest' || !u.isAuthenticated;

    return Semantics(
      container: true,
      identifier: 'admin_user_editor',
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          Row(
            children: [
              Semantics(
                container: true,
                identifier: 'admin_user_editor_back_btn',
                button: true,
                child: OutlinedButton.icon(
                  icon: const Icon(Icons.arrow_back),
                  label: const Text('Todos los usuarios'),
                  onPressed: widget.onBack,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  'Editar usuario · ${u.displayName}',
                  style: Theme.of(context).textTheme.titleLarge,
                ),
              ),
            ],
          ),
          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(_error!, style: const TextStyle(color: Colors.red)),
          ],
          const SizedBox(height: 16),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Estado y Autenticación',
                      style:
                          TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 10),
                  Wrap(
                    spacing: 10,
                    runSpacing: 8,
                    children: [
                      Chip(
                        avatar: Icon(
                          u.isAuthenticated
                              ? Icons.verified_user_rounded
                              : Icons.person_off_outlined,
                          size: 18,
                        ),
                        label: Text(u.isAuthenticated
                            ? 'Autenticado en WillIAM'
                            : 'No autenticado (Anónimo / Guest)'),
                      ),
                      Chip(
                        label: Text('Rol base: ${u.globalRole}'),
                      ),
                      Chip(
                        label: Text('Efectivos: ${u.effectiveRoles.join(" + ")}'),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  const Text('Estado en plataforma:',
                      style: TextStyle(fontWeight: FontWeight.w600)),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    children: [
                      Semantics(
                        container: true,
                        identifier: 'admin_user_status_active',
                        button: true,
                        selected: _status == 'Active',
                        child: ChoiceChip(
                          label: const Text('Activo'),
                          selected: _status == 'Active',
                          onSelected: (_) => setState(() => _status = 'Active'),
                        ),
                      ),
                      Semantics(
                        container: true,
                        identifier: 'admin_user_status_blocked',
                        button: true,
                        selected: _status == 'Blocked',
                        child: ChoiceChip(
                          label: const Text('Bloqueado'),
                          selected: _status == 'Blocked',
                          selectedColor: const Color(0xFFFEE2E2),
                          onSelected: (_) => setState(() => _status = 'Blocked'),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Roles (Guest | User | Owner | Admin)',
                      style:
                          TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 6),
                  Text(
                    isGuest
                        ? 'Este dispositivo es Guest (no registrado): solo puede tener el rol Guest y no puede ser Owner ni Admin hasta registrarse como User.'
                        : 'Usuario registrado (User). Puede acumular Owner por casa (máximo 1 Owner por casa) y/o Admin.',
                    style: const TextStyle(color: Colors.black54, fontSize: 13),
                  ),
                  const SizedBox(height: 8),
                  Semantics(
                    container: true,
                    identifier: 'admin_user_admin_switch',
                    child: SwitchListTile(
                      title: const Text('Rol global Admin'),
                      subtitle:
                          const Text('Permite acceso completo a /admin-bo'),
                      value: isGuest ? false : _isAdmin,
                      onChanged: isGuest
                          ? null
                          : (val) => setState(() => _isAdmin = val),
                    ),
                  ),
                  const Divider(),
                  const Text('Casas a las que pertenece:',
                      style: TextStyle(fontWeight: FontWeight.w600)),
                  if (u.houses.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8),
                      child: Text('No pertenece a ninguna casa.'),
                    )
                  else
                    ...u.houses.map(
                      (h) => Semantics(
                        container: true,
                        identifier: 'admin_user_house_owner_${h.houseId}',
                        child: SwitchListTile(
                          title: Text(h.houseName),
                          subtitle: Text(
                              (_houseOwners[h.houseId] ?? false)
                                  ? 'Rol en casa: Owner (dueño)'
                                  : 'Rol en casa: Miembro'),
                          value: isGuest ? false : (_houseOwners[h.houseId] ?? false),
                          onChanged: isGuest
                              ? null
                              : (val) => setState(
                                  () => _houseOwners[h.houseId] = val),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Propiedades guardadas en Bases de Datos',
                      style:
                          TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
                  const SizedBox(height: 12),
                  Semantics(
                    container: true,
                    identifier: 'admin_user_name_input',
                    child: TextField(
                      controller: _nameCtrl,
                      decoration: const InputDecoration(
                        labelText: 'Nombre visible (app_users.display_name)',
                        border: OutlineInputBorder(),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  _readonlyRow('ID interno BD', u.id),
                  _readonlyRow('Zitadel User ID (sub)', u.zitadelUserId ?? '—'),
                  _readonlyRow('Device ID (Guest)', u.deviceId ?? '—'),
                  _readonlyRow('Email', u.email ?? '—'),
                  _readonlyRow('Método Auth', u.authMethod),
                  _readonlyRow('Alta', u.createdAt.toLocal().toString()),
                  _readonlyRow('Último acceso', u.lastSeenAt.toLocal().toString()),
                  const Divider(height: 24),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text('Propiedades adicionales (clave / valor):',
                          style: TextStyle(fontWeight: FontWeight.w600)),
                      Semantics(
                        container: true,
                        identifier: 'admin_user_add_prop_btn',
                        button: true,
                        child: TextButton.icon(
                          icon: const Icon(Icons.add),
                          label: const Text('Añadir propiedad'),
                          onPressed: () {
                            setState(() {
                              _propRows.add((
                                key: TextEditingController(),
                                value: TextEditingController(),
                              ));
                            });
                          },
                        ),
                      ),
                    ],
                  ),
                  ..._propRows.asMap().entries.map((entry) {
                    final idx = entry.key;
                    final row = entry.value;
                    return Padding(
                      padding: const EdgeInsets.only(top: 8),
                      child: Row(
                        children: [
                          Expanded(
                            child: TextField(
                              controller: row.key,
                              decoration: const InputDecoration(
                                labelText: 'Clave',
                                border: OutlineInputBorder(),
                                isDense: true,
                              ),
                            ),
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: TextField(
                              controller: row.value,
                              decoration: const InputDecoration(
                                labelText: 'Valor',
                                border: OutlineInputBorder(),
                                isDense: true,
                              ),
                            ),
                          ),
                          IconButton(
                            icon: const Icon(Icons.delete_outline_rounded),
                            onPressed: () {
                              setState(() {
                                final removed = _propRows.removeAt(idx);
                                removed.key.dispose();
                                removed.value.dispose();
                              });
                            },
                          ),
                        ],
                      ),
                    );
                  }),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          Semantics(
            container: true,
            identifier: 'admin_user_save_btn',
            button: true,
            child: FilledButton.icon(
              icon: _saving
                  ? const SizedBox(
                      width: 16,
                      height: 16,
                      child: CircularProgressIndicator(
                          strokeWidth: 2, color: Colors.white),
                    )
                  : const Icon(Icons.save_rounded),
              label: Text(_saving ? 'Guardando...' : 'Guardar cambios'),
              onPressed: _saving ? null : _save,
            ),
          ),
        ],
      ),
    );
  }

  Widget _readonlyRow(String label, String value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 3),
        child: Row(
          children: [
            SizedBox(
              width: 170,
              child: Text(label,
                  style: const TextStyle(
                      fontWeight: FontWeight.w600, color: Colors.black54)),
            ),
            Expanded(
              child: SelectableText(value),
            ),
          ],
        ),
      );
}

class _ItemsListView extends StatelessWidget {
  final List<AdminCatalogItemDto> items;
  final TextEditingController controller;
  final ValueChanged<String> onSearchChanged;

  const _ItemsListView({
    required this.items,
    required this.controller,
    required this.onSearchChanged,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
          child: Semantics(
            container: true,
            identifier: 'admin_items_search_input',
            child: TextField(
              controller: controller,
              onChanged: onSearchChanged,
              decoration: InputDecoration(
                prefixIcon: const Icon(Icons.search_rounded),
                hintText: 'Filtrar ítems alfabéticamente por texto, tipo o casa...',
                border: const OutlineInputBorder(),
                suffixIcon: controller.text.isNotEmpty
                    ? IconButton(
                        icon: const Icon(Icons.clear),
                        onPressed: () {
                          controller.clear();
                          onSearchChanged('');
                        },
                      )
                    : null,
              ),
            ),
          ),
        ),
        Expanded(
          child: items.isEmpty
              ? const Center(child: Text('No hay ítems que coincidan con el filtro.'))
              : ListView.builder(
                  padding: const EdgeInsets.all(16),
                  itemCount: items.length,
                  itemBuilder: (context, idx) {
                    final item = items[idx];
                    return Semantics(
                      container: true,
                      identifier: 'admin_item_row_$idx',
                      label:
                          '${item.name} · ${item.productTypeName} · ${item.locationLabel}',
                      child: Card(
                        margin: const EdgeInsets.only(bottom: 8),
                        child: ListTile(
                          leading: ProductTypeDot(
                              fillHex: item.productTypeFillHex ?? '#CBD5E1'),
                          title: Text(item.name,
                              style:
                                  const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text('Tipo: ${item.productTypeName}'),
                          trailing: Chip(
                            avatar: Icon(
                              item.houseCount > 1
                                  ? Icons.workspaces_rounded
                                  : Icons.home_outlined,
                              size: 16,
                            ),
                            label: Text(item.locationLabel),
                          ),
                        ),
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }
}
