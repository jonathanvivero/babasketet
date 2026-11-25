import { expect, test } from '@playwright/test';
import {
	enableFlutterSemantics,
	fillFlutterText,
	readSemanticText,
	sem,
	swipeHorizontal
} from '../helpers/flutter-semantics';

test.describe('F7: Sincronización en tiempo real en 3 dispositivos', () => {
	test('E2E-25: 3 dispositivos conectados a la misma casa ven cambios en tiempo real', async ({
		browser
	}) => {
		const ctx1 = await browser.newContext();
		const ctx2 = await browser.newContext();
		const ctx3 = await browser.newContext();

		const p1 = await ctx1.newPage();
		const p2 = await ctx2.newPage();
		const p3 = await ctx3.newPage();

		// Dispositivo 1 crea la casa y comparte enlace
		await p1.goto('/');
		await enableFlutterSemantics(p1);
		await fillFlutterText(sem(p1, 'onboarding_house_name_input'), 'Casa 3 Dispositivos');
		await sem(p1, 'onboarding_create_house_btn').click();
		await sem(p1, 'share_house_btn').click();
		await expect(sem(p1, 'share_url_text')).toBeVisible();
		const urlText = await readSemanticText(sem(p1, 'share_url_text'));
		const joinPath = urlText.match(/\/join\/[A-Za-z0-9_-]+/)![0];
		await p1.getByText('Cerrar').click();

		// Dispositivo 2 y 3 se unen
		await p2.goto(joinPath);
		await enableFlutterSemantics(p2);
		await expect(sem(p2, 'list_input_field')).toBeVisible({ timeout: 15_000 });
		await p3.goto(joinPath);
		await enableFlutterSemantics(p3);
		await expect(sem(p3, 'list_input_field')).toBeVisible({ timeout: 15_000 });
		await p3.waitForTimeout(800);

		// Dispositivo 1 añade "Café" -> Dispositivos 2 y 3 lo ven en tiempo real
		await fillFlutterText(sem(p1, 'list_input_field'), 'Café');
		await sem(p1, 'list_add_btn').click();

		await expect(sem(p2, 'list_item_café')).toBeVisible({ timeout: 10_000 });
		await expect(sem(p3, 'list_item_café')).toBeVisible({ timeout: 10_000 });

		// Dispositivo 2 desliza "Café" a Cesta -> Dispositivo 3 lo ve en Cesta
		await swipeHorizontal(p2, sem(p2, 'list_item_café'), 'right');
		await p3.getByRole('tab', { name: /Cesta/ }).click();
		await expect(sem(p3, 'basket_item_café')).toBeVisible({ timeout: 10_000 });

		await ctx1.close();
		await ctx2.close();
		await ctx3.close();
	});
});
