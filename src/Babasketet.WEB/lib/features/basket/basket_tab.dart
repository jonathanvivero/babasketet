import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/config/app_config.dart';
import '../../core/models/models.dart';
import '../../core/widgets/long_press_and_dot.dart';
import '../houses/house_providers.dart';

class BasketTab extends ConsumerStatefulWidget {
  const BasketTab({super.key});

  @override
  ConsumerState<BasketTab> createState() => _BasketTabState();
}

class _BasketTabState extends ConsumerState<BasketTab> {
  double _dragAccum = 0;

  Future<void> _changeQuantity(BasketItemDto item, double delta) async {
    final house = ref.read(houseControllerProvider).activeHouse;
    if (house == null) return;
    final next = (item.quantity + delta).clamp(1.0, 999.0);
    if (next == item.quantity) return;
    await ref
        .read(apiClientProvider)
        .updateBasketItem(house.id, item.id, quantity: next);
    await ref.read(houseControllerProvider.notifier).refreshActiveHouse();
  }

  Future<void> _openPriceModal(BasketItemDto item) async {
    final house = ref.read(houseControllerProvider).activeHouse;
    if (house == null) return;
    final initial = item.currentPrice ?? 0.0;
    final ctrl = TextEditingController(
      text: initial.toStringAsFixed(2).replaceAll('.', ','),
    );

    await showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Precio de "${item.name}"'),
        content: Semantics(
          container: true,
          identifier: 'basket_price_input',
          textField: true,
          child: TextField(
            controller: ctrl,
            autofocus: true,
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            decoration: const InputDecoration(
              labelText: 'Precio (€)',
              hintText: '0,00',
              border: OutlineInputBorder(),
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('Cancelar'),
          ),
          Semantics(
            container: true,
            identifier: 'basket_price_save_btn',
            button: true,
            child: FilledButton(
              onPressed: () async {
                final parsed = AppConfig.parseEuroInput(ctrl.text);
                if (parsed == null || parsed < 0) return;
                await ref
                    .read(apiClientProvider)
                    .setBasketItemPrice(house.id, item.id, parsed);
                await ref
                    .read(houseControllerProvider.notifier)
                    .refreshActiveHouse();
                if (ctx.mounted) Navigator.of(ctx).pop();
              },
              child: const Text('Guardar'),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _openLongPressPopup(BasketItemDto item) async {
    final state = ref.read(houseControllerProvider);
    final house = state.activeHouse;
    if (house == null) return;

    final qtyCtrl = TextEditingController(
      text: item.quantity == item.quantity.roundToDouble()
          ? item.quantity.toInt().toString()
          : item.quantity.toString(),
    );
    String selectedFormat = item.format;
    String? selectedTypeId = item.productTypeId;

    await showDialog<void>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setS) => AlertDialog(
          title: Text(item.name),
          content: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(
                  controller: qtyCtrl,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  decoration: const InputDecoration(
                    labelText: 'Cambiar ud.',
                    border: OutlineInputBorder(),
                  ),
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String>(
                  value: selectedFormat,
                  decoration: const InputDecoration(
                    labelText: 'Cambiar formato',
                    border: OutlineInputBorder(),
                  ),
                  items: AppConfig.allowedFormats
                      .map((f) => DropdownMenuItem(value: f, child: Text(f)))
                      .toList(),
                  onChanged: (v) {
                    if (v != null) setS(() => selectedFormat = v);
                  },
                ),
                const SizedBox(height: 12),
                DropdownButtonFormField<String?>(
                  value: selectedTypeId,
                  isExpanded: true,
                  decoration: const InputDecoration(
                    labelText: 'Cambiar tipo',
                    border: OutlineInputBorder(),
                  ),
                  items: [
                    const DropdownMenuItem<String?>(
                      value: null,
                      child: Text('Sin tipo'),
                    ),
                    ...state.productTypes.map(
                      (t) => DropdownMenuItem<String?>(
                        value: t.id,
                        child: Row(
                          children: [
                            ProductTypeDot(
                              fillHex: t.fillHex,
                              borderHex: t.borderHex,
                            ),
                            const SizedBox(width: 8),
                            Flexible(child: Text(t.name)),
                          ],
                        ),
                      ),
                    ),
                  ],
                  onChanged: (v) => setS(() => selectedTypeId = v),
                ),
                const SizedBox(height: 16),
                Semantics(
                  container: true,
                  identifier: 'basket_popup_delete_btn',
                  button: true,
                  child: OutlinedButton.icon(
                    style: OutlinedButton.styleFrom(foregroundColor: Colors.red),
                    onPressed: () async {
                      await ref
                          .read(apiClientProvider)
                          .deleteBasketItem(house.id, item.id);
                      await ref
                          .read(houseControllerProvider.notifier)
                          .refreshActiveHouse();
                      if (ctx.mounted) Navigator.of(ctx).pop();
                    },
                    icon: const Icon(Icons.delete_forever),
                    label: const Text('Eliminar (no vuelve a Lista)'),
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
            FilledButton(
              onPressed: () async {
                final parsedQty = AppConfig.parseEuroInput(qtyCtrl.text);
                if (parsedQty == null || parsedQty <= 0) return;
                await ref.read(apiClientProvider).updateBasketItem(
                      house.id,
                      item.id,
                      quantity: parsedQty,
                      format: selectedFormat,
                      productTypeId: selectedTypeId,
                      clearProductType: selectedTypeId == null,
                    );
                await ref
                    .read(houseControllerProvider.notifier)
                    .refreshActiveHouse();
                if (ctx.mounted) Navigator.of(ctx).pop();
              },
              child: const Text('Aplicar'),
            ),
          ],
        ),
      ),
    );
  }

  Future<void> _confirmCheckout(BasketResponse basket, String houseId) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Efectuar Compra'),
        content: Text(
          '¿Guardar esta compra en "${basket.activeStoreName}" por un total de ${AppConfig.formatEuro(basket.calculatedTotal)} y vaciar la cesta?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(false),
            child: const Text('Cancelar'),
          ),
          Semantics(
            container: true,
            identifier: 'confirm_checkout_btn',
            button: true,
            child: FilledButton(
              onPressed: () => Navigator.of(ctx).pop(true),
              child: const Text('Confirmar Compra'),
            ),
          ),
        ],
      ),
    );

    if (confirmed == true) {
      await ref.read(apiClientProvider).checkoutBasket(houseId);
      await ref.read(houseControllerProvider.notifier).refreshActiveHouse();
    }
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(houseControllerProvider);
    final house = state.activeHouse;
    final basket = state.basket;
    if (house == null || basket == null) {
      return const Center(child: CircularProgressIndicator());
    }

    return Column(
      children: [
        Expanded(
          child: basket.items.isEmpty
              ? const Center(
                  child: Text(
                    'Tu cesta está vacía. Desliza productos a la derecha desde Lista.',
                  ),
                )
              : ListView.builder(
                  itemCount: basket.items.length,
                  itemBuilder: (context, index) {
                    final item = basket.items[index];
                    final normName = item.name.toLowerCase();
                    final qtyText = item.quantity == item.quantity.roundToDouble()
                        ? '${item.quantity.toInt()} ${item.format}.'
                        : '${item.quantity} ${item.format}.';

                    return Dismissible(
                      key: ValueKey('basket_item_${item.id}'),
                      direction: DismissDirection.endToStart,
                      background: Container(
                        color: Colors.orange.shade600,
                        alignment: Alignment.centerRight,
                        padding: const EdgeInsets.symmetric(horizontal: 20),
                        child: const Row(
                          mainAxisAlignment: MainAxisAlignment.end,
                          children: [
                            Text('Volver a Lista',
                                style: TextStyle(color: Colors.white)),
                            SizedBox(width: 8),
                            Icon(Icons.undo, color: Colors.white),
                          ],
                        ),
                      ),
                      onDismissed: (_) async {
                        await ref
                            .read(apiClientProvider)
                            .moveBasketItemToList(house.id, item.id);
                        await ref
                            .read(houseControllerProvider.notifier)
                            .refreshActiveHouse();
                      },
                      child: TwoSecondLongPress(
                        onTriggered: () => _openLongPressPopup(item),
                        child: Semantics(
                          container: true,
                          identifier: 'basket_item_$normName',
                          child: ListTile(
                            leading: item.productType != null
                                ? ProductTypeDot(
                                    fillHex: item.productType!.fillHex,
                                    borderHex: item.productType!.borderHex,
                                  )
                                : null,
                            title: Text(item.name),
                            subtitle: Row(
                              children: [
                                Semantics(
                                  container: true,
                                  identifier: 'basket_qty_$normName',
                                  child: GestureDetector(
                                    onVerticalDragUpdate: (details) {
                                      _dragAccum += details.delta.dy;
                                      if (_dragAccum <= -24) {
                                        _dragAccum = 0;
                                        _changeQuantity(item, 1);
                                      } else if (_dragAccum >= 24) {
                                        _dragAccum = 0;
                                        _changeQuantity(item, -1);
                                      }
                                    },
                                    child: Container(
                                      padding: const EdgeInsets.symmetric(
                                          horizontal: 10, vertical: 4),
                                      decoration: BoxDecoration(
                                        color: Colors.amber.shade100,
                                        borderRadius: BorderRadius.circular(8),
                                      ),
                                      child: Text(
                                        qtyText,
                                        style: const TextStyle(
                                            fontWeight: FontWeight.w700),
                                      ),
                                    ),
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Semantics(
                                  container: true,
                                  identifier: 'basket_price_$normName',
                                  button: true,
                                  child: InkWell(
                                    onTap: () => _openPriceModal(item),
                                    child: Padding(
                                      padding: const EdgeInsets.symmetric(
                                          horizontal: 8, vertical: 4),
                                      child: item.currentPrice != null
                                          ? Text(
                                              AppConfig.formatEuro(
                                                  item.currentPrice!),
                                              style: const TextStyle(
                                                fontWeight: FontWeight.w700,
                                                color: Colors.teal,
                                              ),
                                            )
                                          : const Text(
                                              'Consignar €',
                                              style: TextStyle(
                                                fontSize: 12,
                                                color: Colors.black45,
                                                decoration:
                                                    TextDecoration.underline,
                                              ),
                                            ),
                                    ),
                                  ),
                                ),
                              ],
                            ),
                            trailing: item.currentPrice != null
                                ? Text(
                                    AppConfig.formatEuro(item.lineTotal),
                                    style: const TextStyle(
                                        fontWeight: FontWeight.bold),
                                  )
                                : null,
                          ),
                        ),
                      ),
                    );
                  },
                ),
        ),
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.surfaceContainerHighest,
            border: const Border(top: BorderSide(color: Colors.black12)),
          ),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Total (${basket.activeStoreName})',
                      style: const TextStyle(fontSize: 12, color: Colors.black54),
                    ),
                    Semantics(
                      container: true,
                      identifier: 'basket_total_label',
                      child: Text(
                        AppConfig.formatEuro(basket.calculatedTotal),
                        style: const TextStyle(
                            fontSize: 22, fontWeight: FontWeight.w800),
                      ),
                    ),
                  ],
                ),
              ),
              Semantics(
                container: true,
                identifier: 'basket_checkout_btn',
                button: true,
                child: FilledButton.icon(
                  onPressed: basket.items.isEmpty
                      ? null
                      : () => _confirmCheckout(basket, house.id),
                  icon: const Icon(Icons.shopping_bag_rounded),
                  label: const Text('Efectuar Compra'),
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}
