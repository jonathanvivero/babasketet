import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:qr_flutter/qr_flutter.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../core/models/models.dart';
import 'house_providers.dart';

class ShareHouseDialog extends ConsumerStatefulWidget {
  final HouseDto house;

  const ShareHouseDialog({super.key, required this.house});

  @override
  ConsumerState<ShareHouseDialog> createState() => _ShareHouseDialogState();
}

class _ShareHouseDialogState extends ConsumerState<ShareHouseDialog> {
  InvitationResponse? _invitation;
  bool _loading = true;
  String? _error;

  @override
  void initState() {
    super.initState();
    _generate();
  }

  Future<void> _generate() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final inv = await ref
          .read(apiClientProvider)
          .createInvitation(widget.house.id);
      if (mounted) {
        setState(() {
          _invitation = inv;
          _loading = false;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = e.toString();
          _loading = false;
        });
      }
    }
  }

  String get _shareUrl {
    if (_invitation == null) return '';
    return '${Uri.base.origin}${_invitation!.path}';
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text('Compartir "${widget.house.name}"'),
      content: SizedBox(
        width: 320,
        child: _loading
            ? const SizedBox(
                height: 180,
                child: Center(child: CircularProgressIndicator()),
              )
            : _error != null
                ? Text(_error!, style: const TextStyle(color: Colors.red))
                : Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Text(
                        'Este enlace caduca en 1 hora. Cualquiera que lo abra entrará directamente en esta casa.',
                        textAlign: TextAlign.center,
                        style: TextStyle(fontSize: 13),
                      ),
                      const SizedBox(height: 12),
                      Semantics(
                        container: true,
                        identifier: 'share_qr_code',
                        label: 'Código QR de invitación',
                        child: Container(
                          padding: const EdgeInsets.all(8),
                          color: Colors.white,
                          child: QrImageView(
                            data: _shareUrl,
                            size: 170,
                          ),
                        ),
                      ),
                      const SizedBox(height: 10),
                      Semantics(
                        container: true,
                        identifier: 'share_url_text',
                        label: _shareUrl,
                        child: SelectableText(
                          _shareUrl,
                          key: const Key('share_url_text'),
                          style: const TextStyle(fontSize: 12),
                          textAlign: TextAlign.center,
                        ),
                      ),
                      const SizedBox(height: 12),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        alignment: WrapAlignment.center,
                        children: [
                          Semantics(
                            container: true,
                            identifier: 'share_whatsapp_btn',
                            button: true,
                            child: OutlinedButton.icon(
                              onPressed: () => launchUrl(Uri.parse(
                                  'https://wa.me/?text=${Uri.encodeComponent("Únete a nuestra lista de la compra en Babasketet: $_shareUrl")}')),
                              icon: const Icon(Icons.chat_rounded),
                              label: const Text('WhatsApp'),
                            ),
                          ),
                          Semantics(
                            container: true,
                            identifier: 'share_email_btn',
                            button: true,
                            child: OutlinedButton.icon(
                              onPressed: () => launchUrl(Uri.parse(
                                  'mailto:?subject=${Uri.encodeComponent("Invitación a ${widget.house.name} en Babasketet")}&body=${Uri.encodeComponent("Abre este enlace (válido 1 hora): $_shareUrl")}')),
                              icon: const Icon(Icons.email_outlined),
                              label: const Text('Correo'),
                            ),
                          ),
                          Semantics(
                            container: true,
                            identifier: 'share_copy_btn',
                            button: true,
                            child: FilledButton.tonalIcon(
                              onPressed: () async {
                                await Clipboard.setData(
                                    ClipboardData(text: _shareUrl));
                                if (context.mounted) {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    const SnackBar(
                                        content: Text('Enlace copiado')),
                                  );
                                }
                              },
                              icon: const Icon(Icons.copy),
                              label: const Text('Copiar'),
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cerrar'),
        ),
      ],
    );
  }
}
