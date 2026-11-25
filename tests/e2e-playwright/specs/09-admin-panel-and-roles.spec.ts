import fs from 'node:fs';
import { expect, test } from '@playwright/test';
import {
	enableFlutterSemantics,
	fillFlutterText,
	readSemanticText,
	scrollUntilVisible,
	sem
} from '../helpers/flutter-semantics';

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

test.describe('F8: Panel /admin-bo, Usuario Admin Inicial y Roles Zitadel/WillIAM', () => {
	test('E2E-29: jonathanvivero@gmail.com seeded con Admin + changeRequired; flujo primer acceso y panel /admin-bo completo', async ({
		browser
	}) => {
		// 1. Verificar en Zitadel que los 4 roles y jonathanvivero@gmail.com existen correctamente
		const projectId = fs.readFileSync(PROJECT_ID_FILE, 'utf8').trim();
		const rolesRes = (await zitadelAdmin(
			`/management/v1/projects/${projectId}/roles/_search`,
			'POST',
			{}
		)) as { result?: Array<{ key: string }> };
		const roleKeys = (rolesRes.result || []).map((r) => r.key);
		expect(roleKeys).toEqual(expect.arrayContaining(['Guest', 'User', 'Admin', 'Owner']));

		const seededSearch = (await zitadelAdmin('/v2/users', 'POST', {
			queries: [
				{
					emailQuery: {
						emailAddress: 'jonathanvivero@gmail.com',
						method: 'TEXT_QUERY_METHOD_EQUALS'
					}
				}
			]
		})) as { result?: Array<{ userId: string; human?: { passwordChangeRequired?: boolean } }> };
		const seededAdmin = seededSearch.result?.[0];
		expect(seededAdmin).toBeTruthy();
		expect(seededAdmin?.human?.passwordChangeRequired).toBe(true);

		const seededGrants = (await zitadelAdmin('/management/v1/users/grants/_search', 'POST', {
			queries: [
				{ projectIdQuery: { projectId } },
				{ userIdQuery: { userId: seededAdmin!.userId } }
			]
		})) as { result?: Array<{ roleKeys?: string[] }> };
		expect(seededGrants.result?.[0]?.roleKeys).toEqual(
			expect.arrayContaining(['User', 'Admin'])
		);

		// 2. Crear un usuario admin efímero con changeRequired=true (para no consumir el primer login de Jonathan)
		const ts = Date.now();
		const adminEmail = `e2e.admin.${ts}@babasketet.test`;
		const initialPwd = 'QWERTYUIOP-789456123!';
		const updatedPwd = 'NewAdminPass-2026!';
		const createdAdmin = (await zitadelAdmin('/v2/users/human', 'POST', {
			username: adminEmail,
			profile: { givenName: 'E2E', familyName: 'Admin', displayName: 'E2E Admin' },
			email: { email: adminEmail, isVerified: true },
			password: { password: initialPwd, changeRequired: true }
		})) as { userId: string };
		await zitadelAdmin(`/management/v1/users/${createdAdmin.userId}/grants`, 'POST', {
			projectId,
			roleKeys: ['User', 'Admin']
		});

		// 3. Crear datos de prueba vía contexto anónimo para verlos en /admin-bo
		const seedCtx = await browser.newContext();
		const seedPage = await seedCtx.newPage();
		await seedPage.goto('/');
		await enableFlutterSemantics(seedPage);
		await fillFlutterText(sem(seedPage, 'onboarding_house_name_input'), `Casa Alpha ${ts}`);
		await sem(seedPage, 'onboarding_create_house_btn').click();
		await expect(sem(seedPage, 'list_input_field')).toBeVisible({ timeout: 15_000 });
		await fillFlutterText(sem(seedPage, 'list_input_field'), `Miel-${ts}`);
		await sem(seedPage, 'list_add_btn').click();
		await expect(sem(seedPage, `list_item_miel-${ts}`)).toBeVisible();
		await seedCtx.close();

		// 4. Contexto Admin: entrar a /admin-bo -> pedir login -> cambiar contraseña inicial -> ver /admin-bo
		const adminCtx = await browser.newContext();
		const page = await adminCtx.newPage();
		await page.goto('/admin-bo');
		await enableFlutterSemantics(page);
		await expect(sem(page, 'admin_login_btn')).toBeVisible();
		await sem(page, 'admin_login_btn').click();

		// Redirige a WillIAM.UI -> seleccionar Email+Password
		await page.waitForURL((u) => u.pathname.includes('/ui/v2/login'), { timeout: 20_000 });
		await page.getByTestId('auth-dial-option-password').click();
		await page.getByTestId('pwd-email-input').fill(adminEmail);
		await page.getByTestId('pwd-password-input').fill(initialPwd);
		await page.getByTestId('pwd-submit-btn').click();

		// Al ser primer acceso con changeRequired=true, WillIAM.UI obliga a cambiar contraseña
		await expect(page.getByTestId('pwd-change-form')).toBeVisible({ timeout: 15_000 });
		await page.getByTestId('pwd-new-input').fill(updatedPwd);
		await page.getByTestId('pwd-confirm-input').fill(updatedPwd);
		await page.getByTestId('pwd-change-submit-btn').click();

		// Vuelve vía /auth/callback a la app autenticada como User + Admin
		await page.waitForURL(
			(u) => u.origin === 'http://localhost:8081' && !u.pathname.includes('/auth/callback'),
			{ timeout: 30_000 }
		);

		await page.goto('/admin-bo');
		await enableFlutterSemantics(page);
		await expect(sem(page, 'admin_tab_houses')).toBeVisible({ timeout: 20_000 });
		await expect(sem(page, 'admin_tab_users')).toBeVisible();
		await expect(sem(page, 'admin_tab_items')).toBeVisible();

		// Verificar listado y vista detalle de una casa en /admin-bo
		const firstHouse = page.locator('[flt-semantics-identifier^="admin_house_"]').first();
		await expect(firstHouse).toBeVisible();
		await firstHouse.click();
		await expect(sem(page, 'admin_house_detail_back_btn')).toBeVisible();
		await sem(page, 'admin_house_detail_back_btn').click();

		// Verificar listado y editor de usuarios en /admin-bo
		await sem(page, 'admin_tab_users').click();
		const firstUser = page.locator('[flt-semantics-identifier^="admin_user_"]').first();
		await expect(firstUser).toBeVisible();
		await firstUser.click();
		await expect(sem(page, 'admin_user_editor')).toBeVisible();
		await scrollUntilVisible(page, sem(page, 'admin_user_save_btn'));
		await expect(sem(page, 'admin_user_save_btn')).toBeVisible();

		// Verificar pestaña Items y filtrado por texto
		await sem(page, 'admin_tab_items').click();
		await expect(sem(page, 'admin_items_search_input')).toBeVisible();
		await fillFlutterText(sem(page, 'admin_items_search_input'), `Miel-${ts}`);
		await expect(async () => {
			const rowText = await readSemanticText(
				page.locator('[flt-semantics-identifier^="admin_item_row_"]').first()
			);
			expect(rowText).toContain(`Miel-${ts}`);
		}).toPass({ timeout: 10_000 });

		await adminCtx.close();
	});

	test('E2E-30: Usuario estándar (User sin Admin) recibe 403 Acceso denegado en /admin-bo', async ({
		browser
	}) => {
		const ctx = await browser.newContext();
		const page = await ctx.newPage();
		await page.goto('/');
		await enableFlutterSemantics(page);
		await expect(sem(page, 'onboarding_login_btn')).toBeVisible();
		await sem(page, 'onboarding_login_btn').click();

		await page.waitForURL((u) => u.pathname.includes('/ui/v2/login'), { timeout: 20_000 });
		await page.getByTestId('mode-signup-btn').click();
		await page.getByTestId('auth-dial-option-password').click();
		await page.getByTestId('pwd-name-input').fill('Normal User');
		await page.getByTestId('pwd-email-input').fill(`normal.user.${Date.now()}@babasketet.test`);
		await page.getByTestId('pwd-password-input').fill('NormalUser123!');
		await page.getByTestId('pwd-submit-btn').click();

		await page.waitForURL(
			(u) => u.origin === 'http://localhost:8081' && !u.pathname.includes('/auth/callback'),
			{ timeout: 30_000 }
		);

		await page.goto('/admin-bo');
		await enableFlutterSemantics(page);
		await expect
			.poll(
				async () =>
					await page.evaluate(
						() =>
							document.body.innerText +
							' ' +
							Array.from(document.querySelectorAll('flt-semantics'))
								.map((n) => n.getAttribute('aria-label') || '')
								.join(' ')
					),
				{ timeout: 20_000 }
			)
			.toContain('Acceso denegado');

		await ctx.close();
	});
});
