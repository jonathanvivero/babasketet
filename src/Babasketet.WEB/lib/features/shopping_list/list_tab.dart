import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/models.dart';
import '../../core/widgets/long_press_and_dot.dart';
import '../houses/house_providers.dart';

class ListTab extends ConsumerStatefulWidget {
  const ListTab({super.key});

  @override
  ConsumerState<ListTab> createState() => _ListTabState();
}

class _ListTabState extends ConsumerState<ListTab> {
  final _inputCtrl = TextEditingController();
  final _inputFocus = FocusNode();
  List<ProductSuggestionDto> _suggestions = [];
  bool _showSuggestions = false;

  @override
  void dispose() {
    _inputCtrl.dispose();
    _inputFocus.dispose();
    super.dispose();
  }

  Future<void> _onQueryChanged(String value) async {
    final house = ref.read(houseControllerProvider).activeHouse;
    if (house == null) return;
    if (value.trim().isEmpty) {
      setState(() {
        _suggestions = [];
        _showSuggestions = false;
      });
      return;
    }
    try {
      final matches = await ref
          .read(apiClientProvider)
          .searchProducts(house.id, query: value.trim());
      if (mounted) {
        setState(() {
          _suggestions = matches;
          _showSuggestions = matches.isNotEmpty;
        });
      }
    } catch (_) {}
  }

  Future<void> _addFromText() async {
    final name = _inputCtrl.text.trim();
    final house = ref.read(houseControllerProvider).activeHouse;
    if (name.isEmpty || house == null) return;
    await ref.read(apiClientProvider).addListItem(house.id, name: name);
    _inputCtrl.clear();
    setState(() {
      _suggestions = [];
      _showSuggestions = false;
    });
    await ref.read(houseControllerProvider.notifier).refreshActiveHouse();
  }

  Future<void> _addExistingProduct(ProductSuggestionDto product) async {
    final house = ref.read(houseControllerProvider).activeHouse;
    if (house == null) return;
    await ref
        .read(apiClientProvider)
        .addListItem(house.id, productId: product.id);
    _inputCtrl.clear();
    setState(() {
      _suggestions = [];
      _showSuggestions = false;
    });
    await ref.read(houseControllerProvider.notifier).refreshActiveHouse();
  }

  Future<void> _openExistingProductsPicker() async {
    final house = ref.read(houseControllerProvider).activeHouse;
    if (house == null) return;
    final items = await ref.read(apiClientProvider).searchProducts(house.id);
    if (!mounted) return;

    await showModalBottomSheet<void>(
      context: context,
      builder: (ctx) => SafeArea(
        child: Column(
          children: [
            const Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                'Elementos usados anteriormente',
                style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
              ),
            ),
            Expanded(
              child: items.isEmpty
                  ? const Center(child: Text('Aún no hay productos guardados.'))
                  : ListView.builder(
                      itemCount: items.length,
                      itemBuilder: (_, idx) {
                        final p = items[idx];
                        return ListTile(
                          leading: p.productType != null
                              ? ProductTypeDot(
                                  fillHex: p.productType!.fillHex,
                                  borderHex: p.productType!.borderHex,
                                )
                              : null,
                          title: Text(p.name),
                          subtitle: Text(p.defaultFormat),
                          onTap: () async {
                            Navigator.of(ctx).pop();
                            await _addExistingProduct(p);
                          },
                        );
                      },
                    ),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _openTypePopup(ShoppingListItemDto item) async {
    final state = ref.read(houseControllerProvider);
    final house = state.activeHouse;
    if (house == null) return;
    final nameCtrl = TextEditingController(text: item.name);
    String? selectedTypeId = item.productTypeId;

    await showDialog<void>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) {
          final types = ref.watch(houseControllerProvider).productTypes;
          return AlertDialog(
            title: const Text('Editar elemento'),
            content: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Semantics(
                    container: true,
                    identifier: 'list_edit_name_input',
                    textField: true,
                    child: TextField(
                      controller: nameCtrl,
                      decoration: const InputDecoration(
                        labelText: 'Nombre del producto',
                        border: OutlineInputBorder(),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Semantics(
                    container: true,
                    identifier: 'list_type_dropdown',
                    child: DropdownButtonFormField<String?>(
                      value: selectedTypeId,
                      isExpanded: true,
                      decoration: const InputDecoration(
                        labelText: 'Tipo de producto',
                        border: OutlineInputBorder(),
                      ),
                      items: [
                        const DropdownMenuItem<String?>(
                          value: null,
                          child: Text('Sin tipo (texto negro)'),
                        ),
                        ...types.map(
                          (t) => DropdownMenuItem<String?>(
                            value: t.id,
                            child: Row(
                              children: [
                                ProductTypeDot(
                                  fillHex: t.fillHex,
                                  borderHex: t.borderHex,
                                ),
                                const SizedBox(width: 10),
                                Flexible(child: Text(t.name)),
                              ],
                            ),
                          ),
                        ),
                      ],
                      onChanged: (val) =>
                          setDialogState(() => selectedTypeId = val),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Semantics(
                    container: true,
                    identifier: 'create_custom_type_btn',
                    button: true,
                    child: TextButton.icon(
                      onPressed: () async {
                        final created =
                            await _openCreateCustomTypeDialog(house.id);
                        if (created != null) {
                          await ref
                              .read(houseControllerProvider.notifier)
                              .refreshActiveHouse();
                          setDialogState(() => selectedTypeId = created.id);
                        }
                      },
                      icon: const Icon(Icons.add_circle_outline),
                      label: const Text('Crear tipo personalizado'),
                    ),
                  ),
                ],
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.of(ctx).pop(),
                child: const Text('Cancelar'),
              ),
              Semantics(
                container: true,
                identifier: 'save_list_type_btn',
                button: true,
                child: FilledButton(
                  onPressed: () async {
                    final trimmedName = nameCtrl.text.trim();
                    if (trimmedName.isEmpty) return;
                    await ref.read(apiClientProvider).updateListItem(
                          house.id,
                          item.id,
                          name: trimmedName,
                          productTypeId: selectedTypeId,
                          clearProductType: selectedTypeId == null,
                        );
                    await ref
                        .read(houseControllerProvider.notifier)
                        .refreshActiveHouse();
                    if (ctx.mounted) Navigator.of(ctx).pop();
                  },
                  child: const Text('Guardar'),
                ),
              ),
            ],
          );
        },
      ),
    );
  }

  Future<ProductTypeDto?> _openCreateCustomTypeDialog(String houseId) async {
    final nameCtrl = TextEditingController();
    String fillHex = '#22C55E';
    String? borderHex;

    const palette = [
      '#22C55E',
      '#EF4444',
      '#3B82F6',
      '#F59E0B',
      '#A855F7',
      '#EC4899',
      '#FFFFFF',
      '#F5F5DC',
      '#000000',
    ];

    return showDialog<ProductTypeDto>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setS) => AlertDialog(
          title: const Text('Nuevo tipo de producto'),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                TextField(
                  controller: nameCtrl,
                  decoration: const InputDecoration(
                    labelText: 'Nombre del tipo',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                const Text('Color de fondo:'),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 8,
                  children: palette.map((hex) {
                    final selected = fillHex == hex;
                    return GestureDetector(
                      onTap: () => setS(() => fillHex = hex),
                      child: Container(
                        padding: const EdgeInsets.all(2),
                        decoration: BoxDecoration(
                          shape: BoxShape.circle,
                          border: selected
                              ? Border.all(color: Colors.black, width: 2)
                              : null,
                        ),
                        child: ProductTypeDot(fillHex: hex, size: 24),
                      ),
                    );
                  }).toList(),
                ),
                const SizedBox(height: 12),
                const Text('Borde (opcional):'),
                const SizedBox(height: 6),
                Wrap(
                  spacing: 8,
                  children: [null, '#000000', '#2563EB', '#FF00FF', '#78350F']
                      .map((hex) {
                    final selected = borderHex == hex;
                    return ChoiceChip(
                      label: Text(hex == null ? 'Sin borde' : hex),
                      selected: selected,
                      onSelected: (_) => setS(() => borderHex = hex),
                    );
                  }).toList(),
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(ctx).pop(),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () async {
                if (nameCtrl.text.trim().isEmpty) return;
                final created = await ref
                    .read(apiClientProvider)
                    .createProductType(
                      houseId,
                      name: nameCtrl.text.trim(),
                      fillHex: fillHex,
                      borderHex: borderHex,
                    );
                if (ctx.mounted) Navigator.of(ctx).pop(created);
              },
              child: const Text('Crear'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(houseControllerProvider);
    final house = state.activeHouse;
    if (house == null) return const SizedBox.shrink();
    final items = state.listItems;

    return Stack(
      children: [
        Column(
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(12, 12, 12, 6),
              child: Row(
                children: [
                  Semantics(
                    container: true,
                    identifier: 'list_history_btn',
                    button: true,
                    child: IconButton.outlined(
                      tooltip: 'Seleccionar items existentes',
                      onPressed: _openExistingProductsPicker,
                      icon: const Icon(Icons.history_rounded),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Semantics(
                      container: true,
                      identifier: 'list_input_field',
                      textField: true,
                      child: TextField(
                        controller: _inputCtrl,
                        focusNode: _inputFocus,
                        onChanged: _onQueryChanged,
                        onSubmitted: (_) => _addFromText(),
                        decoration: const InputDecoration(
                          hintText: 'Añadir a la lista...',
                          border: OutlineInputBorder(),
                          isDense: true,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Semantics(
                    container: true,
                    identifier: 'list_add_btn',
                    button: true,
                    child: IconButton.filled(
                      tooltip: 'Añadir texto',
                      onPressed: _addFromText,
                      icon: const Icon(Icons.check_rounded),
                    ),
                  ),
                ],
              ),
            ),
            if (_showSuggestions)
              Container(
                margin: const EdgeInsets.symmetric(horizontal: 12),
                constraints: const BoxConstraints(maxHeight: 180),
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: ListView.builder(
                  shrinkWrap: true,
                  itemCount: _suggestions.length,
                  itemBuilder: (ctx, i) {
                    final s = _suggestions[i];
                    return ListTile(
                      dense: true,
                      leading: s.productType != null
                          ? ProductTypeDot(
                              fillHex: s.productType!.fillHex,
                              borderHex: s.productType!.borderHex,
                            )
                          : null,
                      title: Text(s.name),
                      onTap: () => _addExistingProduct(s),
                    );
                  },
                ),
              ),
            Expanded(
              child: items.isEmpty
                  ? const Center(
                      child: Text('Tu lista está vacía. ¡Añade productos arriba o con [+]!'),
                    )
                  : ReorderableListView.builder(
                      padding: const EdgeInsets.only(bottom: 88, top: 4),
                      itemCount: items.length,
                      onReorder: (oldIndex, newIndex) async {
                        final mutable = [...items];
                        if (newIndex > oldIndex) newIndex -= 1;
                        final moved = mutable.removeAt(oldIndex);
                        mutable.insert(newIndex, moved);
                        await ref.read(apiClientProvider).reorderList(
                              house.id,
                              mutable.map((e) => e.id).toList(),
                            );
                        await ref
                            .read(houseControllerProvider.notifier)
                            .refreshActiveHouse();
                      },
                      itemBuilder: (context, index) {
                        final item = items[index];
                        return Dismissible(
                          key: ValueKey('list_item_${item.id}'),
                          background: Container(
                            color: Colors.green.shade600,
                            alignment: Alignment.centerLeft,
                            padding: const EdgeInsets.symmetric(horizontal: 20),
                            child: const Row(
                              children: [
                                Icon(Icons.shopping_cart, color: Colors.white),
                                SizedBox(width: 8),
                                Text('A la Cesta',
                                    style: TextStyle(color: Colors.white)),
                              ],
                            ),
                          ),
                          secondaryBackground: Container(
                            color: Colors.red.shade600,
                            alignment: Alignment.centerRight,
                            padding: const EdgeInsets.symmetric(horizontal: 20),
                            child: const Row(
                              mainAxisAlignment: MainAxisAlignment.end,
                              children: [
                                Text('Eliminar',
                                    style: TextStyle(color: Colors.white)),
                                SizedBox(width: 8),
                                Icon(Icons.delete_outline, color: Colors.white),
                              ],
                            ),
                          ),
                          onDismissed: (direction) async {
                            if (direction == DismissDirection.startToEnd) {
                              await ref
                                  .read(apiClientProvider)
                                  .moveListItemToBasket(house.id, item.id);
                            } else {
                              await ref
                                  .read(apiClientProvider)
                                  .deleteListItem(house.id, item.id);
                            }
                            await ref
                                .read(houseControllerProvider.notifier)
                                .refreshActiveHouse();
                          },
                          child: ItemLongPressDetector(
                            onTriggered: () => _openTypePopup(item),
                            child: Semantics(
                              container: true,
                              identifier:
                                  'list_item_${item.name.toLowerCase()}',
                              child: ListTile(
                                leading: item.productType != null
                                    ? ProductTypeDot(
                                        fillHex: item.productType!.fillHex,
                                        borderHex: item.productType!.borderHex,
                                      )
                                    : null,
                                title: Text(
                                  item.name,
                                  style: const TextStyle(
                                    color: Colors.black,
                                    fontWeight: FontWeight.w500,
                                  ),
                                ),
                                subtitle: item.productType != null
                                    ? Text(item.productType!.name,
                                        style: const TextStyle(fontSize: 12))
                                    : null,
                                trailing: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Text(item.format,
                                        style: const TextStyle(
                                            color: Colors.black54)),
                                    const SizedBox(width: 8),
                                    const Icon(Icons.drag_handle_rounded),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        );
                      },
                    ),
            ),
          ],
        ),
        Positioned(
          right: 16,
          bottom: 16,
          child: Semantics(
            container: true,
            identifier: 'list_fab_plus',
            button: true,
            child: FloatingActionButton(
              onPressed: () {
                _inputFocus.requestFocus();
                _onQueryChanged(_inputCtrl.text);
              },
              child: const Icon(Icons.add),
            ),
          ),
        ),
      ],
    );
  }
}
