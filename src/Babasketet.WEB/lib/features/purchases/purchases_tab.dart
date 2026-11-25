import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:intl/intl.dart';

import '../../core/config/app_config.dart';
import '../../core/models/models.dart';
import '../houses/house_providers.dart';

class PurchasesTab extends ConsumerWidget {
  const PurchasesTab({super.key});

  Future<void> _openDetail(
    BuildContext context,
    WidgetRef ref,
    String houseId,
    PurchaseSummaryDto summary,
  ) async {
    final detail =
        await ref.read(apiClientProvider).getPurchaseDetail(houseId, summary.id);
    if (!context.mounted) return;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                '${detail.storeName} · ${DateFormat('dd/MM/yyyy HH:mm').format(detail.purchasedAt.toLocal())}',
                style:
                    const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 12),
              ...detail.items.map(
                (i) => ListTile(
                  dense: true,
                  title: Text(i.productName),
                  subtitle: Text(
                      '${i.quantity} ${i.format} × ${AppConfig.formatEuro(i.unitPrice)}'),
                  trailing: Text(AppConfig.formatEuro(i.lineTotal)),
                ),
              ),
              const Divider(),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'Total: ${AppConfig.formatEuro(detail.effectiveTotal)}',
                    style: const TextStyle(
                        fontSize: 16, fontWeight: FontWeight.bold),
                  ),
                  TextButton.icon(
                    onPressed: () async {
                      final ctrl = TextEditingController(
                        text: detail.effectiveTotal
                            .toStringAsFixed(2)
                            .replaceAll('.', ','),
                      );
                      final updated = await showDialog<double>(
                        context: ctx,
                        builder: (dCtx) => AlertDialog(
                          title: const Text('Establecer total de la compra'),
                          content: TextField(
                            controller: ctrl,
                            keyboardType: const TextInputType.numberWithOptions(
                                decimal: true),
                            decoration: const InputDecoration(
                              labelText: 'Total (€)',
                              border: OutlineInputBorder(),
                            ),
                          ),
                          actions: [
                            TextButton(
                              onPressed: () => Navigator.of(dCtx).pop(),
                              child: const Text('Cancelar'),
                            ),
                            FilledButton(
                              onPressed: () => Navigator.of(dCtx)
                                  .pop(AppConfig.parseEuroInput(ctrl.text)),
                              child: const Text('Guardar'),
                            ),
                          ],
                        ),
                      );
                      if (updated != null && updated >= 0) {
                        await ref
                            .read(apiClientProvider)
                            .updateManualTotal(houseId, detail.id, updated);
                        await ref
                            .read(houseControllerProvider.notifier)
                            .refreshActiveHouse();
                        if (ctx.mounted) Navigator.of(ctx).pop();
                      }
                    },
                    icon: const Icon(Icons.edit_outlined),
                    label: const Text('Editar total'),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Semantics(
                container: true,
                identifier: 'purchase_receipt_locked_btn',
                button: true,
                child: OutlinedButton.icon(
                  onPressed: () {
                    showDialog<void>(
                      context: ctx,
                      builder: (pCtx) => AlertDialog(
                        title: const Text('Función Premium'),
                        content: const Text(
                          'Adjuntar la foto del ticket está bloqueado (Locked) y reservado exclusivamente para la versión Premium.',
                        ),
                        actions: [
                          FilledButton(
                            onPressed: () => Navigator.of(pCtx).pop(),
                            child: const Text('Entendido'),
                          ),
                        ],
                      ),
                    );
                  },
                  icon: const Icon(Icons.lock_outline_rounded),
                  label: const Text('Adjuntar ticket como foto (Locked · Premium)'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final state = ref.watch(houseControllerProvider);
    final house = state.activeHouse;
    if (house == null) return const SizedBox.shrink();

    if (state.purchases.isEmpty) {
      return const Center(
        child: Text('Aún no has efectuado ninguna compra.'),
      );
    }

    return ListView.separated(
      itemCount: state.purchases.length,
      separatorBuilder: (_, __) => const Divider(height: 1),
      itemBuilder: (context, index) {
        final p = state.purchases[index];
        return Semantics(
          container: true,
          identifier: 'purchase_row_$index',
          button: true,
          child: ListTile(
            leading: const Icon(Icons.receipt_long_rounded),
            title: Text(p.storeName),
            subtitle: Text(
              '${DateFormat('dd/MM/yyyy HH:mm').format(p.purchasedAt.toLocal())} · ${p.itemCount} productos',
            ),
            trailing: Text(
              AppConfig.formatEuro(p.effectiveTotal),
              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
            ),
            onTap: () => _openDetail(context, ref, house.id, p),
          ),
        );
      },
    );
  }
}
