export type AuthMethodId =
	| 'passkey'
	| 'password'
	| 'email-magic'
	| 'phone-magic'
	| 'google'
	| 'meta'
	| 'apple'
	| 'microsoft'
	| 'linkedin';

export interface AuthMethodSpec {
	id: AuthMethodId;
	label: string;
	shortLabel: string;
	badgeColor: string;
	kind: 'passkey' | 'password' | 'email-magic' | 'phone-magic' | 'social';
	providerKey?: string;
}

export const AUTH_METHODS: AuthMethodSpec[] = [
	{ id: 'passkey', label: 'Passkey (Biometría)', shortLabel: 'Key', badgeColor: '#0EA5E9', kind: 'passkey' },
	{ id: 'password', label: 'Email + Password', shortLabel: '@**', badgeColor: '#1E293B', kind: 'password' },
	{ id: 'email-magic', label: 'Email + MagicLink', shortLabel: '✉', badgeColor: '#10B981', kind: 'email-magic' },
	{ id: 'phone-magic', label: 'Teléfono + MagicLink', shortLabel: '☎', badgeColor: '#F59E0B', kind: 'phone-magic' },
	{ id: 'google', label: 'SSO Google', shortLabel: 'G', badgeColor: '#EA4335', kind: 'social', providerKey: 'google' },
	{ id: 'meta', label: 'SSO Meta (IG/FB)', shortLabel: 'M', badgeColor: '#1877F2', kind: 'social', providerKey: 'facebook' },
	{ id: 'apple', label: 'SSO Apple', shortLabel: '', badgeColor: '#111827', kind: 'social', providerKey: 'apple' },
	{ id: 'microsoft', label: 'SSO Microsoft', shortLabel: '⊞', badgeColor: '#0078D4', kind: 'social', providerKey: 'microsoft' },
	{ id: 'linkedin', label: 'SSO LinkedIn', shortLabel: 'in', badgeColor: '#0A66C2', kind: 'social', providerKey: 'linkedin' }
];

export const DIAL_STEP_DEG = 360 / AUTH_METHODS.length; // 40 deg

export function normalizeAngleDeg(deg: number): number {
	const mod = deg % 360;
	const res = mod < 0 ? mod + 360 : mod;
	return res === 0 ? 0 : res;
}

/**
 * Dado un angulo de rotacion del disco en grados (sentido horario positivo),
 * devuelve el indice del metodo situado en el "norte" (12 en punto).
 */
export function getNorthMethodIndex(rotationDeg: number, count = AUTH_METHODS.length): number {
	const step = 360 / count;
	const norm = normalizeAngleDeg(-rotationDeg);
	return Math.round(norm / step) % count || 0;
}

export function snapRotationDeg(rotationDeg: number, count = AUTH_METHODS.length): number {
	const step = 360 / count;
	return Math.round(rotationDeg / step) * step || 0;
}

export function targetRotationForIndex(index: number, currentRotationDeg: number, count = AUTH_METHODS.length): number {
	const step = 360 / count;
	const desiredBase = normalizeAngleDeg(-index * step);
	const currentNorm = normalizeAngleDeg(currentRotationDeg);
	let delta = desiredBase - currentNorm;
	if (delta > 180) delta -= 360;
	if (delta < -180) delta += 360;
	return currentRotationDeg + delta;
}

export function formatE164Phone(countryCode: string, localNumber: string): string {
	const cleanCode = countryCode.trim().replace(/[^\d+]/g, '');
	const prefixed = cleanCode.startsWith('+') ? cleanCode : `+${cleanCode}`;
	const cleanNum = localNumber.replace(/[\s\-()]/g, '');
	if (!/^\+\d{1,4}$/.test(prefixed) || !/^\d{6,14}$/.test(cleanNum)) {
		throw new Error('Número de teléfono inválido. Debe incluir prefijo de país (ej. +34) y número.');
	}
	return `${prefixed}${cleanNum}`;
}
