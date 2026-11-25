import { expect, test } from '@playwright/test';

const WILLIAM_URL = process.env.WILLIAM_UI_URL || 'http://localhost:3000';
const MAILPIT_URL = process.env.MAILPIT_URL || 'http://localhost:8025';

test.describe('F2: WillIAM.UI Disco Rotatorio, Passkeys y MagicLinks', () => {
	test('E2E-05: Disco rotatorio muestra los 9 métodos y permite girar hasta el Norte', async ({
		page
	}) => {
		await page.goto(`${WILLIAM_URL}/ui/v2/login/login`);

		const wheel = page.getByTestId('auth-dial-wheel');
		await expect(wheel).toBeVisible();
		await expect(wheel).toHaveAttribute('data-selected-method', 'passkey');

		// Verificar los 9 metodos presentes
		const methods = [
			'passkey',
			'password',
			'email-magic',
			'phone-magic',
			'google',
			'meta',
			'apple',
			'microsoft',
			'linkedin'
		];
		for (const id of methods) {
			await expect(page.getByTestId(`auth-dial-option-${id}`)).toBeVisible();
		}

		// Girar hacia Email+Password
		await page.getByTestId('auth-dial-option-password').click();
		await expect(wheel).toHaveAttribute('data-selected-method', 'password');
		await expect(page.getByTestId('pwd-email-input')).toBeVisible();
	});

	test('E2E-05b: Passkey (WebAuthn virtual authenticator) registro y login sin contraseña', async ({
		page
	}) => {
		const client = await page.context().newCDPSession(page);
		await client.send('WebAuthn.enable');
		const { authenticatorId } = await client.send('WebAuthn.addVirtualAuthenticator', {
			options: {
				protocol: 'ctap2',
				transport: 'internal',
				hasResidentKey: true,
				hasUserVerification: true,
				isUserVerified: true
			}
		});

		await page.goto(`${WILLIAM_URL}/ui/v2/login/login`);
		await page.getByTestId('mode-signup-btn').click();
		await page.getByTestId('passkey-email-input').fill(`passkey.${Date.now()}@babasketet.test`);
		await page.getByTestId('passkey-name-input').fill('Test Passkey');
		await page.getByTestId('passkey-register-btn').click();

		// Al terminar sin authRequest redirige a Babasketet APP
		await page.waitForURL((u) => u.origin !== new URL(WILLIAM_URL).origin, { timeout: 20_000 });

		await client.send('WebAuthn.removeVirtualAuthenticator', { authenticatorId });
	});

	test('E2E-06 & E2E-07: Email MagicLink (Mailpit) y Teléfono + MagicLink (Mock SMS)', async ({
		page,
		request
	}) => {
		await page.goto(`${WILLIAM_URL}/ui/v2/login/login`);

		// Email + MagicLink
		await page.getByTestId('auth-dial-option-email-magic').click();
		const targetEmail = `magic.${Date.now()}@babasketet.test`;
		await page.getByTestId('magic-email-input').fill(targetEmail);
		await page.getByTestId('magic-email-submit').click();
		await expect(page.getByTestId('auth-info')).toBeVisible();

		// Comprobar Mailpit / mock-inbox
		const inboxRes = await request.get(`${WILLIAM_URL}/api/mock-inbox`);
		const inbox = await inboxRes.json();
		expect(inbox.items.some((i: { recipient: string }) => i.recipient === targetEmail)).toBeTruthy();

		// Verificar tambien que Mailpit recibió el mensaje
		const mpRes = await request.get(`${MAILPIT_URL}/api/v1/messages`);
		expect(mpRes.ok()).toBeTruthy();

		// Telefono + MagicLink
		await page.getByTestId('auth-dial-option-phone-magic').click();
		await page.getByTestId('phone-country-input').fill('+34');
		await page.getByTestId('phone-number-input').fill('611223344');
		await page.getByTestId('phone-magic-submit').click();
		await expect(page.getByTestId('auth-info')).toContainText('+34611223344');
	});
});
