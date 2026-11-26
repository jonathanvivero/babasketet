import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/models/models.dart';
import '../houses/house_providers.dart';

class AdminSettingsSection extends ConsumerStatefulWidget {
  final String? adminEmail;

  const AdminSettingsSection({super.key, this.adminEmail});

  @override
  ConsumerState<AdminSettingsSection> createState() =>
      _AdminSettingsSectionState();
}

class _AdminSettingsSectionState extends ConsumerState<AdminSettingsSection> {
  final _hostCtrl = TextEditingController();
  final _portCtrl = TextEditingController(text: '587');
  final _usernameCtrl = TextEditingController();
  final _passwordCtrl = TextEditingController();
  final _fromEmailCtrl = TextEditingController();
  final _fromNameCtrl = TextEditingController(text: 'Babasketet');
  final _testRecipientCtrl = TextEditingController();

  String _securityMode = 'StartTls';
  bool _enabled = true;
  bool _hasSavedPassword = false;
  bool _clearPassword = false;
  bool _loading = true;
  bool _saving = false;
  bool _testing = false;
  String? _statusMessage;
  bool _statusIsError = false;

  @override
  void initState() {
    super.initState();
    _testRecipientCtrl.text = widget.adminEmail ?? '';
    Future.microtask(_loadConfig);
  }

  @override
  void dispose() {
    _hostCtrl.dispose();
    _portCtrl.dispose();
    _usernameCtrl.dispose();
    _passwordCtrl.dispose();
    _fromEmailCtrl.dispose();
    _fromNameCtrl.dispose();
    _testRecipientCtrl.dispose();
    super.dispose();
  }

  Future<void> _loadConfig() async {
    setState(() {
      _loading = true;
      _statusMessage = null;
    });
    try {
      final cfg = await ref.read(apiClientProvider).adminGetEmailConfig();
      if (!mounted) return;
      _applyDto(cfg);
      setState(() => _loading = false);
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _statusMessage = e.toString();
        _statusIsError = true;
      });
    }
  }

  void _applyDto(EmailConfigDto cfg) {
    _hostCtrl.text = cfg.host;
    _portCtrl.text = cfg.port.toString();
    _usernameCtrl.text = cfg.username ?? '';
    _passwordCtrl.clear();
    _fromEmailCtrl.text = cfg.fromEmail;
    _fromNameCtrl.text = cfg.fromName;
    _securityMode = cfg.securityMode;
    _enabled = cfg.enabled;
    _hasSavedPassword = cfg.hasPassword;
    _clearPassword = false;
  }

  Future<void> _save() async {
    final port = int.tryParse(_portCtrl.text.trim()) ?? 0;
    setState(() {
      _saving = true;
      _statusMessage = null;
    });
    try {
      final saved = await ref.read(apiClientProvider).adminSaveEmailConfig(
            host: _hostCtrl.text.trim(),
            port: port,
            username: _usernameCtrl.text.trim().isEmpty
                ? null
                : _usernameCtrl.text.trim(),
            password: _passwordCtrl.text.isEmpty ? null : _passwordCtrl.text,
            clearPassword: _clearPassword,
            securityMode: _securityMode,
            fromEmail: _fromEmailCtrl.text.trim(),
            fromName: _fromNameCtrl.text.trim(),
            enabled: _enabled,
          );
      if (!mounted) return;
      _applyDto(saved);
      setState(() {
        _saving = false;
        _statusMessage =
            'Configuración SMTP guardada en base de datos (${saved.host}:${saved.port}).';
        _statusIsError = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _saving = false;
        _statusMessage = e.toString();
        _statusIsError = true;
      });
    }
  }

  Future<void> _sendTestEmail() async {
    final port = int.tryParse(_portCtrl.text.trim()) ?? 0;
    setState(() {
      _testing = true;
      _statusMessage = null;
    });
    try {
      final result = await ref.read(apiClientProvider).adminSendTestEmail(
        toEmail: _testRecipientCtrl.text.trim().isEmpty
            ? null
            : _testRecipientCtrl.text.trim(),
        overrideConfig: {
          'host': _hostCtrl.text.trim(),
          'port': port,
          'username': _usernameCtrl.text.trim().isEmpty
              ? null
              : _usernameCtrl.text.trim(),
          'password': _passwordCtrl.text.isEmpty ? null : _passwordCtrl.text,
          'clearPassword': _clearPassword,
          'securityMode': _securityMode,
          'fromEmail': _fromEmailCtrl.text.trim(),
          'fromName': _fromNameCtrl.text.trim(),
          'enabled': _enabled,
        },
      );
      if (!mounted) return;
      setState(() {
        _testing = false;
        _statusMessage = result.message;
        _statusIsError = !result.success;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _testing = false;
        _statusMessage = e.toString();
        _statusIsError = true;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator());
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        const Text(
          'Configuraciones del sistema',
          style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          children: [
            Semantics(
              container: true,
              identifier: 'admin_settings_menu_email',
              button: true,
              selected: true,
              child: ChoiceChip(
                selected: true,
                avatar: const Icon(Icons.mark_email_read_rounded, size: 18),
                label: const Text('Correo Electrónico Saliente'),
                onSelected: (_) {},
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),
        if (_statusMessage != null)
          Semantics(
            container: true,
            identifier: 'admin_email_test_result',
            child: Container(
              margin: const EdgeInsets.only(bottom: 16),
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: _statusIsError
                    ? const Color(0xFFFEE2E2)
                    : const Color(0xFFDCFCE7),
                borderRadius: BorderRadius.circular(12),
              ),
              child: Text(
                _statusMessage!,
                style: TextStyle(
                  color: _statusIsError
                      ? const Color(0xFF991B1B)
                      : const Color(0xFF166534),
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'Correo Electrónico Saliente (SMTP)',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 12),
                Semantics(
                  container: true,
                  identifier: 'admin_email_enabled_switch',
                  child: SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Envío de correos habilitado'),
                    value: _enabled,
                    onChanged: (v) => setState(() => _enabled = v),
                  ),
                ),
                const SizedBox(height: 8),
                Row(
                  children: [
                    Expanded(
                      flex: 3,
                      child: Semantics(
                        container: true,
                        identifier: 'admin_email_host_input',
                        child: TextField(
                          controller: _hostCtrl,
                          decoration: const InputDecoration(
                            labelText: 'Servidor SMTP (Host)',
                            hintText: 'ej. mailpit o smtp.tu-dominio.com',
                            border: OutlineInputBorder(),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Semantics(
                        container: true,
                        identifier: 'admin_email_port_input',
                        child: TextField(
                          controller: _portCtrl,
                          keyboardType: TextInputType.number,
                          decoration: const InputDecoration(
                            labelText: 'Puerto',
                            hintText: '587',
                            border: OutlineInputBorder(),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                Semantics(
                  container: true,
                  identifier: 'admin_email_security_dropdown',
                  child: DropdownButtonFormField<String>(
                    initialValue: _securityMode,
                    decoration: const InputDecoration(
                      labelText: 'Seguridad / Cifrado',
                      border: OutlineInputBorder(),
                    ),
                    items: const [
                      DropdownMenuItem(
                          value: 'StartTls', child: Text('STARTTLS (puerto 587)')),
                      DropdownMenuItem(
                          value: 'SslTls', child: Text('SSL/TLS (puerto 465)')),
                      DropdownMenuItem(
                          value: 'None', child: Text('Sin cifrado (None / Mailpit 1025)')),
                      DropdownMenuItem(
                          value: 'Auto', child: Text('Automático (Auto)')),
                    ],
                    onChanged: (val) {
                      if (val != null) setState(() => _securityMode = val);
                    },
                  ),
                ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: Semantics(
                        container: true,
                        identifier: 'admin_email_username_input',
                        child: TextField(
                          controller: _usernameCtrl,
                          decoration: const InputDecoration(
                            labelText: 'Cuenta / Usuario SMTP (opcional)',
                            border: OutlineInputBorder(),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Semantics(
                        container: true,
                        identifier: 'admin_email_password_input',
                        child: TextField(
                          controller: _passwordCtrl,
                          obscureText: true,
                          decoration: InputDecoration(
                            labelText: _hasSavedPassword
                                ? 'Contraseña SMTP (guardada; dejar vacío para conservar)'
                                : 'Contraseña SMTP',
                            border: const OutlineInputBorder(),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
                if (_hasSavedPassword)
                  CheckboxListTile(
                    contentPadding: EdgeInsets.zero,
                    value: _clearPassword,
                    title: const Text('Borrar contraseña almacenada'),
                    onChanged: (v) =>
                        setState(() => _clearPassword = v ?? false),
                  ),
                const SizedBox(height: 12),
                Row(
                  children: [
                    Expanded(
                      child: Semantics(
                        container: true,
                        identifier: 'admin_email_from_email_input',
                        child: TextField(
                          controller: _fromEmailCtrl,
                          decoration: const InputDecoration(
                            labelText: 'Email remitente (From)',
                            hintText: 'noreply@babasketet.local',
                            border: OutlineInputBorder(),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Semantics(
                        container: true,
                        identifier: 'admin_email_from_name_input',
                        child: TextField(
                          controller: _fromNameCtrl,
                          decoration: const InputDecoration(
                            labelText: 'Nombre remitente',
                            border: OutlineInputBorder(),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                Semantics(
                  container: true,
                  identifier: 'admin_email_save_btn',
                  button: true,
                  child: FilledButton.icon(
                    onPressed: _saving ? null : _save,
                    icon: const Icon(Icons.save_rounded),
                    label: Text(_saving
                        ? 'Guardando...'
                        : 'Guardar configuración SMTP'),
                  ),
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'Testear envío de correo de prueba',
                  style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 10),
                Semantics(
                  container: true,
                  identifier: 'admin_email_test_to_input',
                  child: TextField(
                    controller: _testRecipientCtrl,
                    decoration: const InputDecoration(
                      labelText: 'Destinatario de prueba',
                      hintText: 'admin@tu-dominio.com',
                      border: OutlineInputBorder(),
                    ),
                  ),
                ),
                const SizedBox(height: 12),
                Semantics(
                  container: true,
                  identifier: 'admin_email_test_btn',
                  button: true,
                  child: OutlinedButton.icon(
                    onPressed: _testing ? null : _sendTestEmail,
                    icon: const Icon(Icons.send_rounded),
                    label: Text(_testing
                        ? 'Enviando prueba...'
                        : 'Enviar email de prueba'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ],
    );
  }
}
