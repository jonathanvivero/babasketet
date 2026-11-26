import fs from 'node:fs';
import { expect, test } from '@playwright/test';
import {
	enableFlutterSemantics,
	fillFlutterText,
	readSemanticText,
	sem
} from '../helpers/flutter-semantics';
import { selectWilliamPasswordMethod } from '../helpers/william-auth';

const ZITADEL_URL = process.env.ZITADEL_URL || 'http://localhost:8085';
const ADMIN_TOKEN_FILE = '/zitadel-bootstrap/admin.pat';
const PROJECT_ID_FILE = '/zitadel-bootstrap/project-id.txt';

async function zitadelAdmin(path: string, method = 'GET', body?: unknown) {
	const token = fs.readFileSync(ADMIN_TOKEN_FILE, 'utf8').trim();
	const res = await fetch(`${ZITADEL_URL}${path}`, {
		method,
		headers: {
			'Content-Type': 'application/json',
			Host: 'localhost:8085',
			Authorization: `Bearer ${token}`
		},
		body: body ? JSON.stringify(body) : undefined
	});
	const data = await res.json().catch(() => ({}));
	if (!res.ok) {
		throw new Error(`Zitadel ${method} ${path} failed (${res.status}): ${JSON.stringify(data)}`);
	}
	return data as Record<string, unknown>;
}

test.describe('F8e: Pantalla de decisión al llegar por enlace de "únete a una casa" (/join/:token)', () => {
	test('E2E-35: Pregunta entre registrarse, introducir credenciales o continuar como invitado y completa las 3 vías', async ({
		browser
	}) => {
		test.setTimeout(120_000);
		const ts = Date.now();

		// 1. Casa origen creada y con enlace de invitación activo
		const ownerCtx = await browser.newContext();
		const ownerPage = await ownerCtx.newPage();
		await ownerPage.goto('/');
		await enableFlutterSemantics(ownerPage);
		await fillFlutterText(sem(ownerPage, 'onboarding_house_name_input'), `Casa Takeoff ${ts}`);
		await sem(ownerPage, 'onboarding_create_house_btn').click();
		await expect(ownerPage.getByText(`Casa Takeoff ${ts}`).first()).toBeVisible({ timeout: 15_000 });

		await sem(ownerPage, 'share_house_btn').click();
		await expect(sem(ownerPage, 'share_url_text')).toBeVisible({ timeout: 10_000 });
		const urlText = await readSemanticText(sem(ownerPage, 'share_url_text'));
		const joinPath = urlText.match(/\/join\/[A-Za-z0-9_-]+/)![0];

		// 2. Vía A: Invitado anónimo (con posibilidad de registrarse después mediante account_btn)
		const guestCtx = await browser.newContext();
		const guestPage = await guestCtx.newPage();
		await guestPage.goto(joinPath);
		await enableFlutterSemantics(guestPage);
		await expect(sem(guestPage, 'join_takeoff_register_btn')).toBeVisible({ timeout: 15_000 });
		await expect(sem(guestPage, 'join_takeoff_login_btn')).toBeVisible();
		await expect(sem(guestPage, 'join_takeoff_guest_btn')).toBeVisible();
		await sem(guestPage, 'join_takeoff_guest_btn').click();
		await expect(guestPage.getByText(`Casa Takeoff ${ts}`).first()).toBeVisible({ timeout: 15_000 });
		await expect(sem(guestPage, 'account_btn')).toBeVisible();
		await guestCtx.close();

		// 3. Vía B: Usuario existente introduce credenciales y entra en la casa
		const projectId = fs.readFileSync(PROJECT_ID_FILE, 'utf8').trim();
		const existingEmail = `existing.join.${ts}@babasketet.test`;
		const existingPwd = 'ExistingJoinPass-2026!';
		const created = (await zitadelAdmin('/v2/users/human', 'POST', {
			username: existingEmail,
			profile: { givenName: 'Existing', familyName: 'Joiner', displayName: 'Existing Joiner' },
			email: { email: existingEmail, isVerified: true },
			password: { password: existingPwd, changeRequired: false }
		})) as { userId: string };
		await zitadelAdmin(`/management/v1/users/${created.userId}/grants`, 'POST', {
			projectId,
			roleKeys: ['User']
		});

		const loginCtx = await browser.newContext();
		const loginPage = await loginCtx.newPage();
		await loginPage.goto(joinPath);
		await enableFlutterSemantics(loginPage);
		await expect(sem(loginPage, 'join_takeoff_login_btn')).toBeVisible({ timeout: 15_000 });
		await sem(loginPage, 'join_takeoff_login_btn').click();

		await selectWilliamPasswordMethod(loginPage, 'login');
		await loginPage.getByTestId('pwd-email-input').fill(existingEmail);
		await loginPage.getByTestId('pwd-password-input').fill(existingPwd);
		await loginPage.getByTestId('pwd-submit-btn').click();

		await loginPage.waitForURL(
			(u) => u.origin === 'http://localhost:8081' && !u.pathname.includes('/auth/callback'),
			{ timeout: 30_000 }
		);
		await enableFlutterSemantics(loginPage);
		await expect(loginPage.getByText(`Casa Takeoff ${ts}`).first()).toBeVisible({ timeout: 15_000 });
		await loginCtx.close();

		// 4. Vía C: Nuevo usuario se registra desde /join/:token y queda dentro de la casa
		const signupCtx = await browser.newContext();
		const signupPage = await signupCtx.newPage();
		await signupPage.goto(joinPath);
		await enableFlutterSemantics(signupPage);
		await expect(sem(signupPage, 'join_takeoff_register_btn')).toBeVisible({ timeout: 15_000 });
		await sem(signupPage, 'join_takeoff_register_btn').click();

		await selectWilliamPasswordMethod(signupPage, 'signup');
		await signupPage.getByTestId('pwd-name-input').fill('Nuevo Invitado Casa');
		await signupPage.getByTestId('pwd-email-input').fill(`signup.join.${ts}@babasketet.test`);
		await signupPage.getByTestId('pwd-password-input').fill('SignupJoinPass-2026!');
		await signupPage.getByTestId('pwd-submit-btn').click();

		await signupPage.waitForURL(
			(u) => u.origin === 'http://localhost:8081' && !u.pathname.includes('/auth/callback'),
			{ timeout: 30_000 }
		);
		await enableFlutterSemantics(signupPage);
		await expect(signupPage.getByText(`Casa Takeoff ${ts}`).first()).toBeVisible({ timeout: 15_000 });
		await signupCtx.close();

		await ownerCtx.close();
	});
});
