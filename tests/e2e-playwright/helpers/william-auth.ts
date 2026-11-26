import { expect, Page } from '@playwright/test';

/**
 * Selecciona la opción de Email + Contraseña en el disco rotatorio de WillIAM.UI,
 * tolerando la ventana de hidratación SSR de SvelteKit antes de pulsar.
 */
export async function selectWilliamPasswordMethod(
	page: Page,
	mode: 'login' | 'signup' = 'login'
) {
	await page.waitForURL((u) => u.pathname.includes('/ui/v2/login'), { timeout: 20_000 });
	await expect(async () => {
		if (mode === 'signup') {
			await page.getByTestId('mode-signup-btn').click();
		}
		await page.getByTestId('auth-dial-option-password').click();
		await expect(page.getByTestId('pwd-email-input')).toBeVisible({ timeout: 1_500 });
		if (mode === 'signup') {
			await expect(page.getByTestId('pwd-name-input')).toBeVisible({ timeout: 1_500 });
		}
	}).toPass({ timeout: 15_000 });
}
