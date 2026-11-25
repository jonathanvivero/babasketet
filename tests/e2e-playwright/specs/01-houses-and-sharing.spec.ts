import { expect, test } from '@playwright/test';
import { enableFlutterSemantics, fillFlutterText, readSemanticText, sem } from '../helpers/flutter-semantics';

test.describe('F1: Casas e Invitaciones', () => {
	test('E2E-01 & E2E-02: Crear casa, generar enlace 1h con QR/WhatsApp/Correo y unirse desde 2º dispositivo', async ({
		browser
	}) => {
		const ctx1 = await browser.newContext();
		const page1 = await ctx1.newPage();
		await page1.goto('/');
		await enableFlutterSemantics(page1);

		await fillFlutterText(sem(page1, 'onboarding_house_name_input'), 'Casa E2E');
		await sem(page1, 'onboarding_create_house_btn').click();

		await expect(page1.getByText('Casa E2E').first()).toBeVisible();
		await expect(page1.getByText('Default').first()).toBeVisible();

		await sem(page1, 'share_house_btn').click();
		await expect(sem(page1, 'share_qr_code')).toBeVisible();
		await expect(sem(page1, 'share_whatsapp_btn')).toBeVisible();
		await expect(sem(page1, 'share_email_btn')).toBeVisible();

		await expect(sem(page1, 'share_url_text')).toBeVisible();
		const urlText = await readSemanticText(sem(page1, 'share_url_text'));
		expect(urlText).toContain('/join/');
		const joinPath = urlText.match(/\/join\/[A-Za-z0-9_-]+/)![0];

		// 2º dispositivo abre el enlace directamente
		const ctx2 = await browser.newContext();
		const page2 = await ctx2.newPage();
		await page2.goto(joinPath);
		await enableFlutterSemantics(page2);
		await expect(page2.getByText('Casa E2E').first()).toBeVisible();

		await ctx1.close();
		await ctx2.close();
	});

	test('E2E-04: Enlace inválido o caducado muestra error claro', async ({ page }) => {
		await page.goto('/join/token-caducado-invalido');
		await enableFlutterSemantics(page);
		await expect(page.getByText(/caducado|revocado/i)).toBeVisible();
	});
});
