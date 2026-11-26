import 'dart:async';

import 'package:flutter/material.dart';

/// Detector personalizado de pulsación larga continua (800 ms por defecto)
/// para abrir el popup de edición de un item en Lista o Cesta.
class ItemLongPressDetector extends StatefulWidget {
  final Widget child;
  final VoidCallback onTriggered;
  final Duration duration;

  const ItemLongPressDetector({
    super.key,
    required this.child,
    required this.onTriggered,
    this.duration = const Duration(milliseconds: 800),
  });

  @override
  State<ItemLongPressDetector> createState() => _ItemLongPressDetectorState();
}

class _ItemLongPressDetectorState extends State<ItemLongPressDetector> {
  Timer? _timer;
  Offset? _downPosition;
  bool _holding = false;

  void _startHold(PointerDownEvent event) {
    _cancelHold();
    _downPosition = event.position;
    setState(() => _holding = true);
    _timer = Timer(widget.duration, () {
      if (!mounted) return;
      setState(() => _holding = false);
      widget.onTriggered();
    });
  }

  void _checkMove(PointerMoveEvent event) {
    if (_downPosition == null) return;
    if ((event.position - _downPosition!).distance > 18) {
      _cancelHold();
    }
  }

  void _cancelHold() {
    _timer?.cancel();
    _timer = null;
    _downPosition = null;
    if (_holding && mounted) {
      setState(() => _holding = false);
    }
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Listener(
      onPointerDown: _startHold,
      onPointerMove: _checkMove,
      onPointerUp: (_) => _cancelHold(),
      onPointerCancel: (_) => _cancelHold(),
      child: Stack(
        alignment: Alignment.centerRight,
        children: [
          widget.child,
          if (_holding)
            const Padding(
              padding: EdgeInsets.only(right: 12),
              child: SizedBox(
                width: 18,
                height: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
        ],
      ),
    );
  }
}

class ProductTypeDot extends StatelessWidget {
  final String fillHex;
  final String? borderHex;
  final double size;

  const ProductTypeDot({
    super.key,
    required this.fillHex,
    this.borderHex,
    this.size = 16,
  });

  static Color parseHex(String hex) {
    final cleaned = hex.replaceAll('#', '');
    return Color(int.parse('FF$cleaned', radix: 16));
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: parseHex(fillHex),
        border: borderHex != null
            ? Border.all(color: parseHex(borderHex!), width: 2.0)
            : null,
      ),
    );
  }
}
