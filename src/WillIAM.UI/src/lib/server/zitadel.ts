import fs from 'node:fs';
import http from 'node:http';
import https from 'node:https';
import { env } from '$env/dynamic/private';

export interface ZitadelConfig {
	apiUrl: string;
	publicUrl: string;
	serviceToken: string;
	adminToken: string;
	projectId: string;
	rpId: string;
}

function readToken(inlineValue?: string, filePath?: string): string {
	if (inlineValue && inlineValue.trim().length > 0) {
		return inlineValue.trim();
	}
	if (filePath && fs.existsSync(filePath)) {
		return fs.readFileSync(filePath, 'utf8').trim();
	}
	return '';
}

export function getZitadelConfig(): ZitadelConfig {
	return {
		apiUrl: (env.ZITADEL_API_URL || 'http://zitadel:8080').replace(/\/$/, ''),
		publicUrl: (env.ZITADEL_PUBLIC_URL || 'http://localhost:8085').replace(/\/$/, ''),
		serviceToken: readToken(env.ZITADEL_SERVICE_USER_TOKEN, env.ZITADEL_SERVICE_USER_TOKEN_FILE),
		adminToken: readToken(env.ZITADEL_ADMIN_TOKEN, env.ZITADEL_ADMIN_TOKEN_FILE),
		projectId: readToken(env.ZITADEL_PROJECT_ID, env.ZITADEL_PROJECT_ID_FILE || '/zitadel-bootstrap/project-id.txt'),
		rpId: env.PUBLIC_RP_ID || 'localhost'
	};
}

export function zitadelFetch<T>(
	path: string,
	options: { method?: string; body?: string; useAdminToken?: boolean; headers?: Record<string, string> } = {}
): Promise<T> {
	const cfg = getZitadelConfig();
	const token = options.useAdminToken ? cfg.adminToken : cfg.serviceToken;
	const apiParsed = new URL(cfg.apiUrl);
	const publicHost = new URL(cfg.publicUrl).host;
	const method = options.method || 'GET';
	const payload = options.body;

	return new Promise((resolve, reject) => {
		const lib = apiParsed.protocol === 'https:' ? https : http;
		const req = lib.request(
			{
				hostname: apiParsed.hostname,
				port: apiParsed.port || (apiParsed.protocol === 'https:' ? 443 : 80),
				path,
				method,
				headers: {
					Host: publicHost,
					'Content-Type': 'application/json',
					...(token ? { Authorization: `Bearer ${token}` } : {}),
					...(payload ? { 'Content-Length': String(Buffer.byteLength(payload)) } : {}),
					...(options.headers || {})
				}
			},
			(res) => {
				let raw = '';
				res.on('data', (chunk) => (raw += chunk));
				res.on('end', () => {
					const data = raw ? JSON.parse(raw) : {};
					if (res.statusCode && res.statusCode >= 200 && res.statusCode < 300) {
						resolve(data as T);
					} else {
						const msg =
							data.message || data.details?.[0]?.message || `Zitadel error ${res.statusCode}`;
						reject(new Error(msg));
					}
				});
			}
		);
		req.on('error', reject);
		if (payload) req.write(payload);
		req.end();
	});
}

// Almacen en memoria para desarrollo/CI (SMS_PROVIDER=mock) de magic links / OTPs
export interface MockNotification {
	id: string;
	channel: 'email' | 'sms';
	recipient: string;
	code: string;
	magicUrl: string;
	createdAt: string;
}

const mockInbox: MockNotification[] = [];

export function recordMockNotification(
	item: Omit<MockNotification, 'id' | 'createdAt'>
): MockNotification {
	const entry: MockNotification = {
		...item,
		id: crypto.randomUUID(),
		createdAt: new Date().toISOString()
	};
	mockInbox.unshift(entry);
	if (mockInbox.length > 50) mockInbox.pop();
	return entry;
}

export function listMockNotifications(): MockNotification[] {
	return mockInbox;
}

export async function resolveBabasketetProjectId(): Promise<string> {
	const cfg = getZitadelConfig();
	if (cfg.projectId) {
		return cfg.projectId;
	}
	const projects = await zitadelFetch<{ result?: Array<{ id: string }> }>(
		'/management/v1/projects/_search',
		{
			method: 'POST',
			useAdminToken: true,
			body: JSON.stringify({
				queries: [{ nameQuery: { name: 'Babasketet', method: 'TEXT_QUERY_METHOD_EQUALS' } }]
			})
		}
	);
	return projects.result?.[0]?.id ?? '';
}

/**
 * Garantiza que todo usuario registrado tenga el rol global 'User' y no 'Guest' en el proyecto Babasketet.
 * Conserva otros roles acumulados (Admin, Owner).
 */
export async function ensureRegisteredUserRoles(userId: string): Promise<void> {
	if (!userId) return;
	const projectId = await resolveBabasketetProjectId();
	if (!projectId) return;

	const grants = await zitadelFetch<{ result?: Array<{ id: string; roleKeys?: string[] }> }>(
		'/management/v1/users/grants/_search',
		{
			method: 'POST',
			useAdminToken: true,
			body: JSON.stringify({
				queries: [{ projectIdQuery: { projectId } }, { userIdQuery: { userId } }]
			})
		}
	);

	const current = grants.result?.[0];
	if (!current) {
		await zitadelFetch(`/management/v1/users/${encodeURIComponent(userId)}/grants`, {
			method: 'POST',
			useAdminToken: true,
			body: JSON.stringify({
				projectId,
				roleKeys: ['User']
			})
		});
		return;
	}

	const withoutGuest = (current.roleKeys ?? []).filter((r) => r !== 'Guest');
	const nextRoles = Array.from(new Set([...withoutGuest, 'User']));
	const changed =
		nextRoles.length !== (current.roleKeys ?? []).length ||
		nextRoles.some((r) => !(current.roleKeys ?? []).includes(r));

	if (changed) {
		await zitadelFetch(
			`/management/v1/users/${encodeURIComponent(userId)}/grants/${encodeURIComponent(current.id)}`,
			{
				method: 'PUT',
				useAdminToken: true,
				body: JSON.stringify({ roleKeys: nextRoles })
			}
		);
	}
}
