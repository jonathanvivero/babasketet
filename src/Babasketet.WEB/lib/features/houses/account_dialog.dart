import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'house_providers.dart';

Future<void> triggerOidcAccountFlow(BuildContext context, WidgetRef ref) async {
  try {
    final completedInline = await ref.read(oidcServiceProvider).startLogin();
    if (completedInline) {
      await ref.read(apiClientProvider).claimAnonymousHouses();
      await ref.read(houseControllerProvider.notifier).bootstrap();
    }
  } catch (e) {
    if (context.mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('No se pudo abrir el acceso WillIAM: $e')),
      );
    }
  }
}

Future<void> showAccountDialog(BuildContext context, WidgetRef ref) async {
  final state = ref.read(houseControllerProvider);
  await showDialog<void>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: Text(state.isRegistered ? 'Cuenta WillIAM conectada' : 'Guardar sesión y Casas'),
      content: Text(
        state.isRegistered
            ? 'Tu sesión está persistida en Zitadel / WillIAM. Gestionas ${state.houses.length} de ${state.maxHouses} casas permitidas.'
            : 'Estás en modo anónimo (máximo 1 casa en este dispositivo). Crea una cuenta o inicia sesión con Passkey, Email, Teléfono o SSO para no perder tus datos y gestionar hasta 4 casas.',
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(ctx).pop(),
          child: const Text('Cerrar'),
        ),
        if (!state.isRegistered)
          Semantics(
            container: true,
            identifier: 'account_open_login_btn',
            button: true,
            child: FilledButton.icon(
              onPressed: () async {
                Navigator.of(ctx).pop();
                await triggerOidcAccountFlow(context, ref);
              },
              icon: const Icon(Icons.key_rounded),
              label: const Text('Crear cuenta / Iniciar sesión'),
            ),
          )
        else
          Semantics(
            container: true,
            identifier: 'account_logout_btn',
            button: true,
            child: OutlinedButton(
              onPressed: () async {
                await ref.read(localStorageProvider).setOidcToken(null);
                await ref.read(houseControllerProvider.notifier).bootstrap();
                if (ctx.mounted) Navigator.of(ctx).pop();
              },
              child: const Text('Cerrar sesión'),
            ),
          ),
      ],
    ),
  );
}
