import { expect, test } from '@playwright/test';
import {
	enableFlutterSemantics,
	expectSemanticContains,
	fillFlutterText,
	longPress800ms,
	scrollUntilVisible,
	sem
} from '../helpers/flutter-semantics';

test.describe('F3, F4, F5, F6: Flujo completo Tiendas, Lista, Cesta y Compras', () => {
	test('E2E-09..24: Tiendas ES/PT, Lista con [+] y LongPress >2s, Cesta con precios por tienda y Compras', async ({
		page
	}) => {
		await page.goto('/');
		await enableFlutterSemantics(page);

		// Crear Casa
		await fillFlutterText(sem(page, 'onboarding_house_name_input'), 'Casa Compra Completa');
		await sem(page, 'onboarding_create_house_btn').click();
		await expect(page.getByText('Casa Compra Completa').first()).toBeVisible();

		// Seleccionar Mercadona
		await sem(page, 'store_selector_btn').click();
		await expect(sem(page, 'store_option_Default')).toBeVisible();
		await scrollUntilVisible(page, sem(page, 'store_option_Pingo Doce'));
		await scrollUntilVisible(page, sem(page, 'store_option_Mercadona'));
		await sem(page, 'store_option_Mercadona').click();
		await expect(page.getByText('Mercadona').first()).toBeVisible();

		// Añadir Leche en Lista
		await fillFlutterText(sem(page, 'list_input_field'), 'Leche');
		await sem(page, 'list_add_btn').click();
		await expect(sem(page, 'list_item_leche')).toBeVisible();

		// Long-press reducido (~800ms) en Lista -> editar nombre a "Leche Entera"
		await longPress800ms(page, sem(page, 'list_item_leche'));
		await expect(sem(page, 'list_edit_name_input')).toBeVisible();
		await fillFlutterText(sem(page, 'list_edit_name_input'), 'Leche Entera');
		await sem(page, 'save_list_type_btn').click();
		await expect(sem(page, 'list_item_leche entera')).toBeVisible();

		// Mover Leche Entera a Cesta y comprobar Total + Precio por tienda
		const box = await sem(page, 'list_item_leche entera').boundingBox();
		expect(box).not.toBeNull();
		await page.mouse.move(box!.x + 30, box!.y + box!.height / 2);
		await page.mouse.down();
		await page.mouse.move(box!.x + box!.width - 20, box!.y + box!.height / 2, { steps: 12 });
		await page.mouse.up();

		await page.getByRole('tab', { name: /Cesta/ }).click();
		await expect(sem(page, 'basket_item_leche entera')).toBeVisible();

		// Long-press reducido (~800ms) en Cesta -> editar nombre a "Leche Semi"
		await longPress800ms(page, sem(page, 'basket_item_leche entera'));
		await expect(sem(page, 'basket_edit_name_input')).toBeVisible();
		await fillFlutterText(sem(page, 'basket_edit_name_input'), 'Leche Semi');
		await sem(page, 'basket_popup_save_btn').click();
		await expect(sem(page, 'basket_item_leche semi')).toBeVisible();

		// Consignar precio 1,50 en Mercadona
		await sem(page, 'basket_price_leche semi').click();
		await fillFlutterText(sem(page, 'basket_price_input'), '1,50');
		await sem(page, 'basket_price_save_btn').click();
		await expectSemanticContains(sem(page, 'basket_total_label'), '1,50 €');

		// Cambiar a Lidl -> precio de Leche Semi no se muestra
		await sem(page, 'store_selector_btn').click();
		await scrollUntilVisible(page, sem(page, 'store_option_Lidl'));
		await sem(page, 'store_option_Lidl').click();
		await expectSemanticContains(sem(page, 'basket_price_leche semi'), 'Consignar €');

		// Volver a Mercadona -> vuelve 1,50 €
		await sem(page, 'store_selector_btn').click();
		await scrollUntilVisible(page, sem(page, 'store_option_Mercadona'));
		await sem(page, 'store_option_Mercadona').click();
		await expectSemanticContains(sem(page, 'basket_total_label'), '1,50 €');

		// Efectuar compra
		await sem(page, 'basket_checkout_btn').click();
		await sem(page, 'confirm_checkout_btn').click();

		// Verificar en Compras y boton Locked Premium
		await page.getByRole('tab', { name: /Compras/ }).click();
		await expect(sem(page, 'purchase_row_0')).toBeVisible();
		await sem(page, 'purchase_row_0').click();
		await expect(sem(page, 'purchase_receipt_locked_btn')).toBeVisible();
	});
});
