import { redirect } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import { zitadelFetch } from '$lib/server/zitadel';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ url }) => {
	const sessionId = url.searchParams.get('sessionId');
	const sessionToken = url.searchParams.get('sessionToken');
	const code = url.searchParams.get('code');
	const channel = url.searchParams.get('channel') === 'sms' ? 'sms' : 'email';
	const authRequestId = url.searchParams.get('authRequest');

	if (!sessionId || !sessionToken || !code) {
		throw redirect(302, '/ui/v2/login/login?error=invalid_magic_link');
	}

	const checks = channel === 'sms' ? { otpSms: { code } } : { otpEmail: { code } };
	const updated = await zitadelFetch<{ sessionId: string; sessionToken: string }>(
		`/v2/sessions/${encodeURIComponent(sessionId)}`,
		{
			method: 'PATCH',
			body: JSON.stringify({ sessionToken, checks })
		}
	);

	if (authRequestId) {
		const res = await zitadelFetch<{ callbackUrl: string }>(
			`/v2/oidc/auth_requests/${encodeURIComponent(authRequestId)}`,
			{
				method: 'POST',
				body: JSON.stringify({
					session: {
						sessionId: updated.sessionId || sessionId,
						sessionToken: updated.sessionToken || sessionToken
					}
				})
			}
		);
		throw redirect(302, res.callbackUrl);
	}

	throw redirect(302, env.PUBLIC_BABASKETET_APP_URL || 'http://localhost:8081');
};
