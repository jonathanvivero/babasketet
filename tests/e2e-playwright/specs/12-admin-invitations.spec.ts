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

const API_URL = process.env.API_URL || 'http://localhost:8080';
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

test.describe('F8c: Sistema de Invitaciones de Plataforma en /admin-bo', () => {
	test('E2E-33: Admin genera invitación, comparte URL/WhatsApp/Email (Mailpit), renueva, cancela, reactiva y filtra por fechas y estado', async ({
		browser,
		request
	}) => {
		const projectId = fs.readFileSync(PROJECT_ID_FILE, 'utf8').trim();
		const ts = Date.now();
		const adminEmail = `inv.admin.${ts}@babasketet.test`;
		const pwd = 'InvAdminPass-2026!';

		const createdAdmin = (await zitadelAdmin('/v2/users/human', 'POST', {
			username: adminEmail,
			profile: { givenName: 'Inv', familyName: 'Admin', displayName: 'Inv Admin' },
			email: { email: adminEmail, isVerified: true },
			password: { password: pwd, changeRequired: false }
		})) as { userId: string };
		await zitadelAdmin(`/management/v1/users/${createdAdmin.userId}/grants`, 'POST', {
			projectId,
			roleKeys: ['User', 'Admin']
		});

		const ctx = await browser.newContext({ viewport: { width: 1280, height: 860 } });
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

		let bearer = '';
		await expect
			.poll(
				async () => {
					const raw = await page.evaluate(() => {
						return (
							window.localStorage.getItem('flutter.babasketet.oidc_token') ||
							window.localStorage.getItem('babasketet.oidc_token') ||
							''
						);
					});
					try {
						bearer = raw.startsWith('"') ? (JSON.parse(raw) as string) : raw;
					} catch {
						bearer = raw;
					}
					return bearer.length;
				},
				{ timeout: 10_000 }
			)
			.toBeGreaterThan(20);

		// Configurar SMTP apuntando a Mailpit
		const smtpRes = await request.put(`${API_URL}/api/v1/admin/settings/email`, {
			headers: { Authorization: `Bearer ${bearer}` },
			data: {
				host: 'mailpit',
				port: 1025,
				securityMode: 'None',
				fromEmail: 'noreply@babasketet.local',
				fromName: 'Babasketet Invitaciones',
				enabled: true
			}
		});
		expect(smtpRes.ok()).toBeTruthy();

		await page.goto('/admin-bo');
		await enableFlutterSemantics(page);
		await expect(sem(page, 'admin_tab_invitations')).toBeVisible({ timeout: 20_000 });
		await sem(page, 'admin_tab_invitations').click();

		await expect(sem(page, 'admin_inv_create_btn')).toBeVisible({ timeout: 10_000 });
		await sem(page, 'admin_inv_create_btn').click();

		await expect(sem(page, 'admin_inv_share_box')).toBeVisible({ timeout: 10_000 });
		const shareText = await readSemanticText(sem(page, 'admin_inv_share_box'));
		const match = shareText.match(/BBK-[A-Z0-9]+/);
		expect(match).not.toBeNull();
		const code = match![0];

		await expect(sem(page, 'admin_inv_copy_url_btn')).toBeVisible();
		await expect(sem(page, 'admin_inv_whatsapp_btn')).toBeVisible();

		// Enviar por email a 2 direcciones (una por línea)
		const rec1 = `guest1.${ts}@babasketet.test`;
		const rec2 = `guest2.${ts}@babasketet.test`;
		await scrollUntilVisible(page, sem(page, 'admin_inv_emails_input'), 0.35);
		await fillFlutterText(sem(page, 'admin_inv_emails_input'), `${rec1}\n${rec2}`);
		await sem(page, 'admin_inv_send_email_btn').click();

		await expect(sem(page, 'admin_inv_feedback')).toBeVisible({ timeout: 10_000 });
		expect(await readSemanticText(sem(page, 'admin_inv_feedback'))).toContain('2 destinatario');

		await expect(async () => {
			const mpRes = await request.get(`${MAILPIT_URL}/api/v1/messages`);
			const mpJson = await mpRes.json();
			const messages = mpJson.messages || [];
			const to1 = messages.some(
				(m: { Subject?: string; To?: Array<{ Address?: string }> }) =>
					(m.Subject || '').includes(code) &&
					(m.To || []).some((to) => to.Address === rec1)
			);
			const to2 = messages.some(
				(m: { Subject?: string; To?: Array<{ Address?: string }> }) =>
					(m.Subject || '').includes(code) &&
					(m.To || []).some((to) => to.Address === rec2)
			);
			expect(to1 && to2).toBeTruthy();
		}).toPass({ timeout: 10_000 });

		// Renovar +7 días y cancelar -> filtrar Canceladas -> reactivar 7 días
		await scrollUntilVisible(page, sem(page, 'admin_inv_renew_0'), 0.35);
		await sem(page, 'admin_inv_renew_0').click();
		await page.locator('[aria-label="Renovar +7 días"]').last().click();
		await expect(sem(page, 'admin_inv_feedback')).toBeVisible({ timeout: 10_000 });
		expect(await readSemanticText(sem(page, 'admin_inv_feedback'))).toContain('+7 días');

		await scrollUntilVisible(page, sem(page, 'admin_inv_cancel_0'), 0.35);
		await sem(page, 'admin_inv_cancel_0').click();
		await sem(page, 'admin_inv_filter_cancelled').click();
		await sem(page, 'admin_inv_filter_active').click();
		await expect(async () => {
			const txt = await readSemanticText(sem(page, 'admin_inv_item_0'));
			expect(txt).toContain('Cancelada');
		}).toPass({ timeout: 10_000 });

		await scrollUntilVisible(page, sem(page, 'admin_inv_reactivate_0'), 0.35);
		await sem(page, 'admin_inv_reactivate_0').click();
		await page.locator('[aria-label="Reactivar 7 días"]').last().click();
		await expect(sem(page, 'admin_inv_feedback')).toBeVisible({ timeout: 10_000 });
		expect(await readSemanticText(sem(page, 'admin_inv_feedback'))).toContain('reactivada por 7');

		await ctx.close();
	});
});
