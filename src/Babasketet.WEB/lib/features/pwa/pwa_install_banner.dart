import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/pwa/pwa_install.dart';
import '../houses/house_providers.dart';

final pwaInstallPromptVisibleProvider = StateProvider<bool>((ref) => false);

Future<void> evaluateFirstVisitInstallPrompt(WidgetRef ref) async {
  if (!kIsWeb || PwaInstallAdapter.isRunningStandalone) return;
  final seen = await ref.read(localStorageProvider).hasSeenInstallPrompt();
  if (!seen) {
    ref.read(pwaInstallPromptVisibleProvider.notifier).state = true;
  }
}

Future<void> showPwaInstallGuideDialog(BuildContext context) async {
  if (PwaInstallAdapter.hasNativePrompt) {
    await PwaInstallAdapter.promptInstall();
    return;
  }

  final isIos = PwaInstallAdapter.isIosBrowser;
  await showDialog<void>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: const Text('Instalar Babasketet como App'),
      content: Text(
        isIos
            ? 'En Safari, pulsa el botón Compartir (□↑) en la barra inferior y selecciona "Añadir a pantalla de inicio".'
            : 'Abre el menú de tu navegador (⋮) y selecciona "Instalar aplicación" o "Añadir a pantalla de inicio" para usarla como app sin barra del navegador.',
      ),
      actions: [
        FilledButton(
          onPressed: () => Navigator.of(ctx).pop(),
          child: const Text('Entendido'),
        ),
      ],
    ),
  );
}

class PwaInstallBanner extends ConsumerWidget {
  const PwaInstallBanner({super.key});

  Future<void> _dismiss(WidgetRef ref) async {
    await ref.read(localStorageProvider).markInstallPromptSeen();
    ref.read(pwaInstallPromptVisibleProvider.notifier).state = false;
  }

  Future<void> _install(BuildContext context, WidgetRef ref) async {
    await ref.read(localStorageProvider).markInstallPromptSeen();
    ref.read(pwaInstallPromptVisibleProvider.notifier).state = false;
    if (PwaInstallAdapter.hasNativePrompt) {
      await PwaInstallAdapter.promptInstall();
    } else if (context.mounted) {
      await showPwaInstallGuideDialog(context);
    }
  }

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final visible = ref.watch(pwaInstallPromptVisibleProvider);
    if (!visible) return const SizedBox.shrink();

    return Semantics(
      container: true,
      identifier: 'pwa_install_banner',
      child: Material(
        color: const Color(0xFFFFEDD5),
        elevation: 1,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          child: Row(
            children: [
              const Icon(Icons.install_mobile_rounded, color: Color(0xFF9A3412)),
              const SizedBox(width: 10),
              const Expanded(
                child: Text(
                  'Instala Babasketet como App en tu dispositivo para acceder más rápido.',
                  style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
                ),
              ),
              const SizedBox(width: 8),
              Semantics(
                container: true,
                identifier: 'pwa_install_now_btn',
                button: true,
                child: FilledButton.tonal(
                  onPressed: () => _install(context, ref),
                  child: const Text('Instalar'),
                ),
              ),
              Semantics(
                container: true,
                identifier: 'pwa_install_dismiss_btn',
                button: true,
                child: IconButton(
                  tooltip: 'Cerrar aviso de instalación',
                  onPressed: () => _dismiss(ref),
                  icon: const Icon(Icons.close_rounded, size: 18),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
