import fs from 'node:fs';
import { expect, test } from '@playwright/test';
import {
	enableFlutterSemantics,
	fillFlutterText,
	readSemanticText,
	scrollUntilVisible,
	sem
} from '../helpers/flutter-semantics';
import { selectWilliamPasswordMethod } from '../helpers/william-auth';

const ZITADEL_URL = process.env.ZITADEL_URL || 'http://localhost:8085';
const MAILPIT_URL = process.env.MAILPIT_URL || 'http://localhost:8025';
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

test.describe('F8a: Configuraciones del sistema > Correo Electrónico Saliente (SMTP)', () => {
	test('E2E-31: Admin configura SMTP contra Mailpit, persiste en BD y envía email de prueba real', async ({
		browser,
		request
	}) => {
		const projectId = fs.readFileSync(PROJECT_ID_FILE, 'utf8').trim();
		const ts = Date.now();
		const adminEmail = `smtp.admin.${ts}@babasketet.test`;
		const pwd = 'SmtpAdminPass-2026!';

		const createdAdmin = (await zitadelAdmin('/v2/users/human', 'POST', {
			username: adminEmail,
			profile: { givenName: 'SMTP', familyName: 'Admin', displayName: 'SMTP Admin' },
			email: { email: adminEmail, isVerified: true },
			password: { password: pwd, changeRequired: false }
		})) as { userId: string };
		await zitadelAdmin(`/management/v1/users/${createdAdmin.userId}/grants`, 'POST', {
			projectId,
			roleKeys: ['User', 'Admin']
		});

		const ctx = await browser.newContext();
		const page = await ctx.newPage();
		await page.goto('/admin-bo');
		await enableFlutterSemantics(page);
		await sem(page, 'admin_login_btn').click();

		await selectWilliamPasswordMethod(page);
		await page.getByTestId('pwd-email-input').fill(adminEmail);
		await page.getByTestId('pwd-password-input').fill(pwd);
		await page.getByTestId('pwd-submit-btn').click();

		await page.waitForURL(
			(u) => u.origin === 'http://localhost:8081' && !u.pathname.includes('/auth/callback'),
			{ timeout: 30_000 }
		);

		await page.goto('/admin-bo');
		await enableFlutterSemantics(page);
		await expect(sem(page, 'admin_tab_settings')).toBeVisible({ timeout: 20_000 });
		await sem(page, 'admin_tab_settings').click();

		await expect(sem(page, 'admin_settings_menu_email')).toBeVisible();
		await fillFlutterText(sem(page, 'admin_email_host_input'), 'mailpit');
		await fillFlutterText(sem(page, 'admin_email_port_input'), '1025');

		// Seleccionar modo Sin cifrado (None / Mailpit 1025)
		await sem(page, 'admin_email_security_dropdown').click();
		await page.locator('[aria-label="Sin cifrado (None / Mailpit 1025)"]').last().click();

		await scrollUntilVisible(page, sem(page, 'admin_email_from_email_input'));
		await fillFlutterText(sem(page, 'admin_email_from_email_input'), 'noreply@babasketet.local');
		await fillFlutterText(sem(page, 'admin_email_from_name_input'), 'Babasketet SMTP');

		await scrollUntilVisible(page, sem(page, 'admin_email_save_btn'));
		await sem(page, 'admin_email_save_btn').click();

		await expect(async () => {
			const msg = await readSemanticText(sem(page, 'admin_email_test_result'));
			expect(msg).toContain('mailpit:1025');
		}).toPass({ timeout: 10_000 });

		// Recargar para comprobar persistencia real en PostgreSQL
		await page.reload();
		await enableFlutterSemantics(page);
		await sem(page, 'admin_tab_settings').click();
		await expect(sem(page, 'admin_email_host_input')).toBeVisible({ timeout: 10_000 });
		await sem(page, 'admin_email_host_input').click();
		await expect(async () => {
			const hostVal = await readSemanticText(sem(page, 'admin_email_host_input'));
			expect(hostVal).toContain('mailpit');
		}).toPass({ timeout: 10_000 });

		// Enviar email de prueba real hacia Mailpit
		const testRecipient = `prueba.${ts}@babasketet.test`;
		await scrollUntilVisible(page, sem(page, 'admin_email_test_btn'));
		await fillFlutterText(sem(page, 'admin_email_test_to_input'), testRecipient);
		await sem(page, 'admin_email_test_btn').click();

		await expect(async () => {
			const msg = await readSemanticText(sem(page, 'admin_email_test_result'));
			expect(msg).toContain(testRecipient);
		}).toPass({ timeout: 15_000 });

		// Comprobar que Mailpit recibió realmente el correo SMTP
		await expect(async () => {
			const mpRes = await request.get(`${MAILPIT_URL}/api/v1/messages`);
			expect(mpRes.ok()).toBeTruthy();
			const mpJson = await mpRes.json();
			const found = (mpJson.messages || []).some((m: { To?: Array<{ Address?: string }> }) =>
				(m.To || []).some((t) => t.Address === testRecipient)
			);
			expect(found).toBeTruthy();
		}).toPass({ timeout: 10_000 });

		await ctx.close();
	});
});
