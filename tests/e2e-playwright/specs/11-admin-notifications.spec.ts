import fs from 'node:fs';
import { expect, test } from '@playwright/test';
import { enableFlutterSemantics, readSemanticText, sem } from '../helpers/flutter-semantics';
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

test.describe('F8b: Panel de Notificaciones en /admin-bo y envío de correo a Admins', () => {
	test('E2E-32: Notificaciones llegan por email a Admins, se listan como Nuevas, abren en popup con Anterior/Siguiente y archivan', async ({
		browser,
		request
	}) => {
		const projectId = fs.readFileSync(PROJECT_ID_FILE, 'utf8').trim();
		const ts = Date.now();
		const adminEmail = `notif.admin.${ts}@babasketet.test`;
		const pwd = 'NotifAdminPass-2026!';

		const createdAdmin = (await zitadelAdmin('/v2/users/human', 'POST', {
			username: adminEmail,
			profile: { givenName: 'Notif', familyName: 'Admin', displayName: 'Notif Admin' },
			email: { email: adminEmail, isVerified: true },
			password: { password: pwd, changeRequired: false }
		})) as { userId: string };
		await zitadelAdmin(`/management/v1/users/${createdAdmin.userId}/grants`, 'POST', {
			projectId,
			roleKeys: ['User', 'Admin']
		});

		const ctx = await browser.newContext({ viewport: { width: 1280, height: 820 } });
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

		// Configurar SMTP apuntando a Mailpit y crear 2 notificaciones
		const smtpRes = await request.put(`${API_URL}/api/v1/admin/settings/email`, {
			headers: { Authorization: `Bearer ${bearer}` },
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

		const n1Title = `Notif Uno ${ts}`;
		const n2Title = `Notif Dos ${ts}`;
		for (const t of [n1Title, n2Title]) {
			const nRes = await request.post(`${API_URL}/api/v1/admin/notifications`, {
				headers: { Authorization: `Bearer ${bearer}` },
				data: { title: t, body: `Cuerpo completo para ${t}`, category: 'system' }
			});
			expect(nRes.status()).toBe(201);
		}

		// Verificar que el correo llegó realmente al email del Admin en Mailpit
		await expect(async () => {
			const mpRes = await request.get(`${MAILPIT_URL}/api/v1/messages`);
			expect(mpRes.ok()).toBeTruthy();
			const mpJson = await mpRes.json();
			const hit = (mpJson.messages || []).some(
				(m: { Subject?: string; To?: Array<{ Address?: string }> }) =>
					(m.Subject || '').includes(n1Title) &&
					(m.To || []).some((to) => to.Address === adminEmail)
			);
			expect(hit).toBeTruthy();
		}).toPass({ timeout: 10_000 });

		// Cargar /admin-bo y comprobar panel de notificaciones en tercio derecho + popup
		await page.goto('/admin-bo');
		await enableFlutterSemantics(page);
		await expect(sem(page, 'admin_notifications_panel')).toBeVisible({ timeout: 20_000 });
		await expect(sem(page, 'admin_notif_item_0')).toBeVisible({ timeout: 10_000 });

		// Abrir la primera notificación en el popup (la más reciente es n2Title)
		await sem(page, 'admin_notif_item_0').click();
		await expect(sem(page, 'admin_notif_detail_dialog')).toBeVisible({ timeout: 10_000 });

		// Navegar a la siguiente y volver a la anterior dentro del popup (marcando ambas como Leídas)
		await expect(async () => {
			const dlgText = await readSemanticText(sem(page, 'admin_notif_detail_dialog'));
			expect(dlgText).toContain('Leída');
		}).toPass({ timeout: 10_000 });
		await sem(page, 'admin_notif_dialog_next').click();
		await expect(async () => {
			const dlgText = await readSemanticText(sem(page, 'admin_notif_detail_dialog'));
			expect(dlgText).toContain('Leída');
		}).toPass({ timeout: 10_000 });
		await sem(page, 'admin_notif_dialog_prev').click();
		await page.locator('[aria-label="Cerrar"]').last().click();

		// Cambiar filtro a solo Leídas (activar Leídas y desactivar Nuevas) y confirmar que aparecen
		await sem(page, 'admin_notif_filter_read').click();
		await sem(page, 'admin_notif_filter_new').click();
		await expect(async () => {
			const txt = await readSemanticText(sem(page, 'admin_notif_item_0'));
			expect(txt).toContain('Leída');
		}).toPass({ timeout: 10_000 });

		// Archivar desde la lista y filtrar solo Archivadas
		await sem(page, 'admin_notif_archive_0').click();
		await sem(page, 'admin_notif_filter_archived').click();
		await sem(page, 'admin_notif_filter_read').click();
		await expect(async () => {
			const txt = await readSemanticText(sem(page, 'admin_notif_item_0'));
			expect(txt).toContain('Archivada');
		}).toPass({ timeout: 10_000 });

		await ctx.close();
	});
});
