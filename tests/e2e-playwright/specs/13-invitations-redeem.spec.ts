import fs from 'node:fs';
import { expect, test } from '@playwright/test';
import {
	enableFlutterSemantics,
	fillFlutterText,
	readSemanticText,
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

test.describe('F8d: Ruta /invitation-redeem/:code (Canje y Solicitud de Renovación)', () => {
	test('E2E-34: Código expirado solicita renovación por panel+email y vuelve al input; código válido pide registro y canjea hacia Onboarding', async ({
		browser,
		request
	}) => {
		test.setTimeout(90_000);
		const projectId = fs.readFileSync(PROJECT_ID_FILE, 'utf8').trim();
		const ts = Date.now();
		const adminEmail = `redeem.admin.${ts}@babasketet.test`;
		const adminPwd = 'RedeemAdminPass-2026!';

		const createdAdmin = (await zitadelAdmin('/v2/users/human', 'POST', {
			username: adminEmail,
			profile: { givenName: 'Redeem', familyName: 'Admin', displayName: 'Redeem Admin' },
			email: { email: adminEmail, isVerified: true },
			password: { password: adminPwd, changeRequired: false }
		})) as { userId: string };
		await zitadelAdmin(`/management/v1/users/${createdAdmin.userId}/grants`, 'POST', {
			projectId,
			roleKeys: ['User', 'Admin']
		});

		// Autenticar Admin para obtener bearer y crear invitaciones
		const adminCtx = await browser.newContext({ viewport: { width: 1280, height: 860 } });
		const adminPage = await adminCtx.newPage();
		await adminPage.goto('/admin-bo');
		await enableFlutterSemantics(adminPage);
		await sem(adminPage, 'admin_login_btn').click();
		await selectWilliamPasswordMethod(adminPage);
		await adminPage.getByTestId('pwd-email-input').fill(adminEmail);
		await adminPage.getByTestId('pwd-password-input').fill(adminPwd);
		await adminPage.getByTestId('pwd-submit-btn').click();
		await adminPage.waitForURL(
			(u) => u.origin === 'http://localhost:8081' && !u.pathname.includes('/auth/callback'),
			{ timeout: 30_000 }
		);

		let adminBearer = '';
		await expect
			.poll(
				async () => {
					const raw = await adminPage.evaluate(() => {
						return (
							window.localStorage.getItem('flutter.babasketet.oidc_token') ||
							window.localStorage.getItem('babasketet.oidc_token') ||
							''
						);
					});
					try {
						adminBearer = raw.startsWith('"') ? (JSON.parse(raw) as string) : raw;
					} catch {
						adminBearer = raw;
					}
					return adminBearer.length;
				},
				{ timeout: 10_000 }
			)
			.toBeGreaterThan(20);

		const smtpRes = await request.put(`${API_URL}/api/v1/admin/settings/email`, {
			headers: { Authorization: `Bearer ${adminBearer}` },
			data: {
				host: 'mailpit',
				port: 1025,
				securityMode: 'None',
				fromEmail: 'noreply@babasketet.local',
				fromName: 'Babasketet Alertas',
				enabled: true
			}
		});
		expect(smtpRes.ok()).toBeTruthy();

		const expRes = await request.post(`${API_URL}/api/v1/admin/invitations`, {
			headers: { Authorization: `Bearer ${adminBearer}` },
			data: { expiresAt: new Date(Date.now() + 2500).toISOString(), isIndefinite: false }
		});
		expect(expRes.status()).toBe(201);
		const expiredInv = (await expRes.json()) as { code: string };

		const actRes = await request.post(`${API_URL}/api/v1/admin/invitations`, {
			headers: { Authorization: `Bearer ${adminBearer}` },
			data: {}
		});
		expect(actRes.status()).toBe(201);
		const activeInv = (await actRes.json()) as { code: string };

		// Esperar a que caduque de forma natural la primera invitación
		await adminPage.waitForTimeout(3_200);

		// Contexto visitante: aterrizar en /invitation-redeem/<expiredCode> -> solicitar renovación
		const visitorCtx = await browser.newContext();
		const page = await visitorCtx.newPage();
		await page.goto(`/invitation-redeem/${expiredInv.code}`);
		await enableFlutterSemantics(page);

		await expect(sem(page, 'invite_redeem_request_renewal_btn')).toBeVisible({ timeout: 15_000 });
		await fillFlutterText(sem(page, 'invite_redeem_renewal_email_input'), `renew.${ts}@babasketet.test`);
		await sem(page, 'invite_redeem_request_renewal_btn').click();

		// Vuelve a la pantalla de consignar código con confirmación
		await expect(sem(page, 'invite_redeem_feedback')).toBeVisible({ timeout: 10_000 });
		await expect(sem(page, 'invite_redeem_code_input')).toBeVisible();

		// Verificar que el Admin recibió notificación por email en Mailpit y en el panel de /admin-bo
		await expect(async () => {
			const mpRes = await request.get(
				`${MAILPIT_URL}/api/v1/search?query=${encodeURIComponent(expiredInv.code)}&limit=100`
			);
			const mpJson = await mpRes.json();
			const hit = (mpJson.messages || []).some(
				(m: { Subject?: string; To?: Array<{ Address?: string }> }) =>
					(m.Subject || '').includes(expiredInv.code) &&
					(m.To || []).some((to) => to.Address === adminEmail)
			);
			expect(hit).toBeTruthy();
		}).toPass({ timeout: 10_000 });

		await adminPage.goto('/admin-bo');
		await enableFlutterSemantics(adminPage);
		await expect(sem(adminPage, 'admin_notifications_panel')).toBeVisible({ timeout: 15_000 });
		await expect(async () => {
			const txt = await readSemanticText(sem(adminPage, 'admin_notifications_panel'));
			expect(txt).toContain(expiredInv.code);
		}).toPass({ timeout: 10_000 });

		// Ahora el visitante consigna el código activo -> pide registro -> registra -> canjea -> Onboarding
		await fillFlutterText(sem(page, 'invite_redeem_code_input'), activeInv.code);
		await sem(page, 'invite_redeem_check_btn').click();
		await expect(sem(page, 'invite_redeem_register_btn')).toBeVisible({ timeout: 10_000 });
		await sem(page, 'invite_redeem_register_btn').click();

		const newEmail = `invited.user.${ts}@babasketet.test`;
		await selectWilliamPasswordMethod(page, 'signup');
		await page.getByTestId('pwd-name-input').fill('Usuario Invitado');
		await page.getByTestId('pwd-email-input').fill(newEmail);
		await page.getByTestId('pwd-password-input').fill('InvitedPass2026!');
		await page.getByTestId('pwd-submit-btn').click();

		await page.waitForURL((u) => u.pathname.includes('/onboarding'), { timeout: 30_000 });
		await enableFlutterSemantics(page);
		await expect(sem(page, 'onboarding_create_house_btn')).toBeVisible({ timeout: 15_000 });
		await expect(sem(page, 'onboarding_join_house_btn')).toBeVisible();

		// Verificar en admin-bo > Invitaciones > Canjeadas que figura el usuario canjeador
		await sem(adminPage, 'admin_tab_invitations').click();
		await sem(adminPage, 'admin_inv_filter_redeemed').click();
		await sem(adminPage, 'admin_inv_filter_active').click();
		await expect(async () => {
			const redeemedItem = await readSemanticText(sem(adminPage, 'admin_inv_item_0'));
			expect(redeemedItem).toContain('Canjeada');
			expect(redeemedItem).toContain(newEmail);
		}).toPass({ timeout: 10_000 });

		await visitorCtx.close();
		await adminCtx.close();
	});
});
