import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/models.dart';
import '../houses/house_providers.dart';

class StoreSelectorSheet extends ConsumerStatefulWidget {
  const StoreSelectorSheet({super.key});

  @override
  ConsumerState<StoreSelectorSheet> createState() => _StoreSelectorSheetState();
}

class _StoreSelectorSheetState extends ConsumerState<StoreSelectorSheet> {
  final _newStoreCtrl = TextEditingController();
  bool _adding = false;

  @override
  void dispose() {
    _newStoreCtrl.dispose();
    super.dispose();
  }

  Future<void> _selectStore(StoreDto store) async {
    final house = ref.read(houseControllerProvider).activeHouse;
    if (house == null) return;
    await ref.read(apiClientProvider).setActiveStore(house.id, store.id);
    await ref.read(houseControllerProvider.notifier).refreshActiveHouse();
    if (mounted) Navigator.of(context).pop();
  }

  Future<void> _addStore() async {
    final name = _newStoreCtrl.text.trim();
    final house = ref.read(houseControllerProvider).activeHouse;
    if (name.isEmpty || house == null) return;
    setState(() => _adding = true);
    try {
      final created =
          await ref.read(apiClientProvider).createStore(house.id, name);
      await ref.read(apiClientProvider).setActiveStore(house.id, created.id);
      await ref.read(houseControllerProvider.notifier).refreshActiveHouse();
      if (mounted) Navigator.of(context).pop();
    } finally {
      if (mounted) setState(() => _adding = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(houseControllerProvider);
    final activeId = state.activeHouse?.activeStoreId;

    return SafeArea(
      child: DraggableScrollableSheet(
        expand: false,
        initialChildSize: 0.75,
        maxChildSize: 0.92,
        builder: (context, scrollCtrl) => Column(
          children: [
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 16, 16, 8),
              child: Text(
                'Seleccionar Supermercado / Tienda',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
            ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Row(
                children: [
                  Expanded(
                    child: Semantics(
                      container: true,
                      identifier: 'store_new_name_input',
                      textField: true,
                      child: TextField(
                        controller: _newStoreCtrl,
                        decoration: const InputDecoration(
                          hintText: 'Agregar nuevo supermercado...',
                          border: OutlineInputBorder(),
                          isDense: true,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Semantics(
                    container: true,
                    identifier: 'store_add_btn',
                    button: true,
                    child: FilledButton(
                      onPressed: _adding ? null : _addStore,
                      child: const Text('Agregar'),
                    ),
                  ),
                ],
              ),
            ),
            const Divider(),
            Expanded(
              child: ListView.builder(
                controller: scrollCtrl,
                itemCount: state.stores.length,
                itemBuilder: (context, index) {
                  final store = state.stores[index];
                  final isSelected = store.id == activeId;
                  return Semantics(
                    container: true,
                    identifier: 'store_option_${store.name}',
                    button: true,
                    child: ListTile(
                      leading: Icon(
                        isSelected
                            ? Icons.check_circle_rounded
                            : Icons.storefront_outlined,
                        color: isSelected ? Colors.green : null,
                      ),
                      title: Text(store.name),
                      trailing: store.isDefault
                          ? const Chip(label: Text('Por defecto'))
                          : null,
                      onTap: () => _selectStore(store),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}
