import { describe, expect, it } from 'vitest';
import {
	AUTH_METHODS,
	DIAL_STEP_DEG,
	formatE164Phone,
	getNorthMethodIndex,
	snapRotationDeg,
	targetRotationForIndex
} from '../src/lib/authMethods';

describe('RotaryDial math & auth methods', () => {
	it('defines the 9 authentication methods in order', () => {
		expect(AUTH_METHODS).toHaveLength(9);
		expect(AUTH_METHODS.map((m) => m.id)).toEqual([
			'passkey',
			'password',
			'email-magic',
			'phone-magic',
			'google',
			'meta',
			'apple',
			'microsoft',
			'linkedin'
		]);
		expect(DIAL_STEP_DEG).toBe(40);
	});

	it('maps rotation angle to the method at North (12 o clock)', () => {
		expect(getNorthMethodIndex(0)).toBe(0); // passkey
		expect(getNorthMethodIndex(-40)).toBe(1); // password
		expect(getNorthMethodIndex(-80)).toBe(2); // email-magic
		expect(getNorthMethodIndex(40)).toBe(8); // linkedin (wrap around)
		expect(getNorthMethodIndex(360)).toBe(0);
	});

	it('snaps arbitrary drag angle to nearest 40-degree step', () => {
		expect(snapRotationDeg(-38)).toBe(-40);
		expect(snapRotationDeg(22)).toBe(40);
		expect(snapRotationDeg(18)).toBe(0);
	});

	it('computes shortest rotation path to target index', () => {
		const target = targetRotationForIndex(3, 0); // phone-magic
		expect(getNorthMethodIndex(target)).toBe(3);
	});

	it('formats country code and phone number to E.164', () => {
		expect(formatE164Phone('+34', '600 11 22 33')).toBe('+34600112233');
		expect(formatE164Phone('351', '912-345-678')).toBe('+351912345678');
		expect(() => formatE164Phone('+34', '123')).toThrow();
	});
});
