import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/pwa/pwa_install.dart';
import '../basket/basket_tab.dart';
import '../houses/account_dialog.dart';
import '../houses/house_providers.dart';
import '../houses/onboarding_page.dart';
import '../houses/share_house_dialog.dart';
import '../purchases/purchases_tab.dart';
import '../pwa/pwa_install_banner.dart';
import '../shopping_list/list_tab.dart';
import '../stores/store_selector_sheet.dart';

class DashboardPage extends ConsumerWidget {
  const DashboardPage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(houseControllerProvider);
    final house = state.activeHouse;
    if (house == null) {
      return const OnboardingPage();
    }

    return DefaultTabController(
      length: 3,
      child: Scaffold(
        drawer: Drawer(
          child: SafeArea(
            child: ListView(
              padding: EdgeInsets.zero,
              children: [
                DrawerHeader(
                  decoration: const BoxDecoration(color: Color(0xFF0F172A)),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisAlignment: MainAxisAlignment.end,
                    children: [
                      const Text(
                        'Babasketet',
                        style: TextStyle(
                            color: Colors.white,
                            fontSize: 22,
                            fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        state.isRegistered
                            ? 'Cuenta WillIAM (${state.houses.length}/4 casas)'
                            : 'Modo Anónimo (${state.houses.length}/1 casa)',
                        style: const TextStyle(color: Colors.white70),
                      ),
                    ],
                  ),
                ),
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 16, vertical: 6),
                  child: Text('Tus Casas',
                      style: TextStyle(fontWeight: FontWeight.bold)),
                ),
                ...state.houses.map(
                  (h) => ListTile(
                    leading: Icon(
                      h.id == house.id
                          ? Icons.home_rounded
                          : Icons.home_outlined,
                      color: h.id == house.id ? const Color(0xFFD97706) : null,
                    ),
                    title: Text(h.name),
                    onTap: () {
                      Navigator.of(context).pop();
                      ref.read(houseControllerProvider.notifier).selectHouse(h);
                    },
                  ),
                ),
                ListTile(
                  leading: const Icon(Icons.share_rounded),
                  title: const Text('Compartir esta casa (1h / QR)'),
                  onTap: () {
                    Navigator.of(context).pop();
                    showDialog<void>(
                      context: context,
                      builder: (_) => ShareHouseDialog(house: house),
                    );
                  },
                ),
                ListTile(
                  leading: const Icon(Icons.add_home_outlined),
                  title: const Text('Crear o unirse a otra casa'),
                  onTap: () {
                    Navigator.of(context).pop();
                    Navigator.of(context).push(
                      MaterialPageRoute<void>(
                        builder: (_) => const OnboardingPage(allowBack: true),
                      ),
                    );
                  },
                ),
                if (kIsWeb && !PwaInstallAdapter.isRunningStandalone)
                  Semantics(
                    container: true,
                    identifier: 'drawer_install_pwa_btn',
                    button: true,
                    child: ListTile(
                      leading: const Icon(Icons.install_mobile_rounded),
                      title: const Text('Instalar como App (PWA)'),
                      onTap: () {
                        Navigator.of(context).pop();
                        showPwaInstallGuideDialog(context);
                      },
                    ),
                  ),
                const Divider(),
                Semantics(
                  container: true,
                  identifier: 'drawer_account_btn',
                  button: true,
                  child: ListTile(
                    leading: Icon(
                      state.isRegistered
                          ? Icons.verified_user_rounded
                          : Icons.key_rounded,
                    ),
                    title: Text(
                      state.isRegistered
                          ? 'Cuenta WillIAM conectada'
                          : 'Crear cuenta / Guardar sesión (WillIAM)',
                    ),
                    subtitle: Text(
                      state.isRegistered
                          ? 'Hasta 4 casas persistidas'
                          : 'Passkey, Email, Teléfono o SSO · Amplía a 4 casas',
                    ),
                    onTap: () {
                      Navigator.of(context).pop();
                      showAccountDialog(context, ref);
                    },
                  ),
                ),
                if (state.isAdmin)
                  Semantics(
                    container: true,
                    identifier: 'drawer_admin_bo_btn',
                    button: true,
                    child: ListTile(
                      leading: const Icon(Icons.admin_panel_settings_rounded,
                          color: Color(0xFFD97706)),
                      title: const Text('Panel de Administración'),
                      subtitle: const Text('/admin-bo · Casas, usuarios e ítems'),
                      onTap: () {
                        Navigator.of(context).pop();
                        context.go('/admin-bo');
                      },
                    ),
                  ),
              ],
            ),
          ),
        ),
        appBar: AppBar(
          leading: Semantics(
            container: true,
            identifier: 'top_menu_btn',
            button: true,
            child: Builder(
              builder: (ctx) => IconButton(
                icon: const Icon(Icons.menu),
                onPressed: () => Scaffold.of(ctx).openDrawer(),
              ),
            ),
          ),
          title: GestureDetector(
            onTap: () => showDialog<void>(
              context: context,
              builder: (_) => ShareHouseDialog(house: house),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(house.name,
                    style: const TextStyle(
                        fontSize: 17, fontWeight: FontWeight.w700)),
                Text(
                  house.activeStoreName,
                  style: const TextStyle(fontSize: 12, color: Colors.black54),
                ),
              ],
            ),
          ),
          actions: [
            Semantics(
              container: true,
              identifier: 'account_btn',
              button: true,
              child: IconButton(
                tooltip: state.isRegistered
                    ? 'Cuenta WillIAM conectada'
                    : 'Crear cuenta / Guardar sesión',
                icon: Icon(
                  state.isRegistered
                      ? Icons.verified_user_rounded
                      : Icons.person_add_alt_1_rounded,
                ),
                onPressed: () => showAccountDialog(context, ref),
              ),
            ),
            Semantics(
              container: true,
              identifier: 'share_house_btn',
              button: true,
              child: IconButton(
                tooltip: 'Compartir casa',
                icon: const Icon(Icons.qr_code_2_rounded),
                onPressed: () => showDialog<void>(
                  context: context,
                  builder: (_) => ShareHouseDialog(house: house),
                ),
              ),
            ),
            Semantics(
              container: true,
              identifier: 'store_selector_btn',
              button: true,
              child: IconButton(
                tooltip: 'Supermercado: ${house.activeStoreName}',
                icon: const Icon(Icons.storefront_rounded),
                onPressed: () => showModalBottomSheet<void>(
                  context: context,
                  isScrollControlled: true,
                  builder: (_) => const StoreSelectorSheet(),
                ),
              ),
            ),
          ],
          bottom: const TabBar(
            tabs: [
              Tab(key: Key('tab_lista'), text: 'Lista'),
              Tab(key: Key('tab_cesta'), text: 'Cesta'),
              Tab(key: Key('tab_compras'), text: 'Compras'),
            ],
          ),
        ),
        body: const Column(
          children: [
            PwaInstallBanner(),
            Expanded(
              child: TabBarView(
                children: [
                  ListTab(),
                  BasketTab(),
                  PurchasesTab(),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}
