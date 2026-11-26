import { expect, test } from '@playwright/test';
import { enableFlutterSemantics, fillFlutterText, sem } from '../helpers/flutter-semantics';

test.describe('F7b: Prompt PWA en 1ª visita, Icono/Favicon y Cuenta en App instalada', () => {
	test('E2E-26: Muestra aviso de instalación PWA solo en la primera visita', async ({ page }) => {
		await page.goto('/');
		await enableFlutterSemantics(page);

		const banner = sem(page, 'pwa_install_banner');
		await expect(banner).toBeVisible();
		await expect(sem(page, 'pwa_install_now_btn')).toBeVisible();

		await sem(page, 'pwa_install_dismiss_btn').click();
		await expect(banner).toBeHidden();

		// Al recargar en la segunda visita, no vuelve a mostrarse
		await page.reload();
		await enableFlutterSemantics(page);
		await expect(sem(page, 'onboarding_create_house_btn')).toBeVisible();
		await expect(sem(page, 'pwa_install_banner')).toHaveCount(0);
	});

	test('E2E-27: Manifest PWA, favicon e iconos de la piña en cesta responden correctamente', async ({
		request
	}) => {
		const manifestRes = await request.get('/manifest.json');
		expect(manifestRes.ok()).toBeTruthy();
		const manifest = await manifestRes.json();
		expect(manifest.display).toBe('standalone');
		expect(manifest.background_color).toBe('#FFB347');
		expect(manifest.icons.length).toBeGreaterThanOrEqual(2);

		for (const iconPath of ['/favicon.png', '/icons/Icon-192.png', '/icons/Icon-512.png', '/icons/apple-touch-icon.png']) {
			const res = await request.get(iconPath);
			expect(res.ok()).toBeTruthy();
			expect(res.headers()['content-type']).toContain('image/png');
		}
	});

	test('E2E-28: Botón de cuenta visible en Dashboard (incluido modo standalone) para persistir instancia', async ({
		page
	}) => {
		await page.addInitScript(() => {
			const origMatchMedia = window.matchMedia.bind(window);
			window.matchMedia = (q: string) => {
				if (q.includes('display-mode: standalone')) {
					return {
						matches: true,
						media: q,
						onchange: null,
						addListener: () => {},
						removeListener: () => {},
						addEventListener: () => {},
						removeEventListener: () => {},
						dispatchEvent: () => false
					} as MediaQueryList;
				}
				return origMatchMedia(q);
			};
		});

		await page.goto('/');
		await enableFlutterSemantics(page);

		// En modo standalone no debe molestar el banner de instalacion
		await expect(sem(page, 'pwa_install_banner')).toHaveCount(0);

		await fillFlutterText(sem(page, 'onboarding_house_name_input'), 'Casa Standalone');
		await sem(page, 'onboarding_create_house_btn').click();
		await expect(page.getByText('Casa Standalone').first()).toBeVisible();

		// El botón de cuenta sigue visible en el AppBar para crear cuenta / persistir la casa
		await expect(sem(page, 'account_btn')).toBeVisible();
		await sem(page, 'account_btn').click();
		await expect(sem(page, 'account_open_login_btn')).toBeVisible();
	});
});
