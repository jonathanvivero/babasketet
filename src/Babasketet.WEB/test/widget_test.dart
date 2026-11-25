import 'package:babasketet_web/core/config/app_config.dart';
import 'package:babasketet_web/core/widgets/long_press_and_dot.dart';
import 'package:babasketet_web/features/houses/onboarding_page.dart';
import 'package:babasketet_web/features/pwa/pwa_install_banner.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  test('AppConfig formats and parses Euro amounts with es-ES comma', () {
    expect(AppConfig.formatEuro(0), '0,00 €');
    expect(AppConfig.formatEuro(12.5), '12,50 €');
    expect(AppConfig.parseEuroInput('1,95'), 1.95);
    expect(AppConfig.parseEuroInput('2.40 €'), 2.40);
    expect(AppConfig.allowedFormats.length, 11);
  });

  testWidgets(
      'ItemLongPressDetector does not trigger before 800ms and triggers after 800ms',
      (tester) async {
    var triggered = 0;

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: ItemLongPressDetector(
            onTriggered: () => triggered++,
            child: const SizedBox(
              width: 200,
              height: 60,
              child: Text('Leche'),
            ),
          ),
        ),
      ),
    );

    final gesture =
        await tester.startGesture(tester.getCenter(find.text('Leche')));
    await tester.pump(const Duration(milliseconds: 400));
    expect(triggered, 0);

    await tester.pump(const Duration(milliseconds: 450));
    expect(triggered, 1);

    await gesture.up();
  });

  testWidgets('ProductTypeDot renders fill and optional border',
      (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: Column(
            children: [
              ProductTypeDot(fillHex: '#FFFFFF', borderHex: '#FF00FF'),
              ProductTypeDot(fillHex: '#86EFAC'),
            ],
          ),
        ),
      ),
    );

    final containers =
        tester.widgetList<Container>(find.byType(Container)).toList();
    final withBorder = containers[0].decoration as BoxDecoration;
    final withoutBorder = containers[1].decoration as BoxDecoration;
    expect(withBorder.color, const Color(0xFFFFFFFF));
    expect(withBorder.border, isNotNull);
    expect(withoutBorder.color, const Color(0xFF86EFAC));
    expect(withoutBorder.border, isNull);
  });

  testWidgets('OnboardingPage shows Create and Join house sections',
      (tester) async {
    await tester.pumpWidget(
      const ProviderScope(
        child: MaterialApp(
          home: OnboardingPage(),
        ),
      ),
    );

    expect(find.text('Babasketet'), findsOneWidget);
    expect(find.text('Crear Casa'), findsOneWidget);
    expect(find.text('Unirme a la Casa'), findsOneWidget);
  });

  testWidgets('PwaInstallBanner renders when enabled and hides on dismiss',
      (tester) async {
    SharedPreferences.setMockInitialValues({});

    await tester.pumpWidget(
      ProviderScope(
        overrides: [
          pwaInstallPromptVisibleProvider.overrideWith((ref) => true),
        ],
        child: const MaterialApp(
          home: Scaffold(
            body: PwaInstallBanner(),
          ),
        ),
      ),
    );

    expect(find.text('Instalar'), findsOneWidget);
    await tester.tap(find.byIcon(Icons.close_rounded));
    await tester.pumpAndSettle();
    expect(find.text('Instalar'), findsNothing);
  });
}
