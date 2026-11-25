import { redirect } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import { zitadelFetch } from '$lib/server/zitadel';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ url }) => {
	const id = url.searchParams.get('id');
	const token = url.searchParams.get('token');
	const authRequestId = url.searchParams.get('authRequest');

	if (!id || !token) {
		throw redirect(302, '/ui/v2/login/login?error=idp_callback_missing');
	}

	const intent = await zitadelFetch<{ userId?: string }>(
		`/v2/idp_intents/${encodeURIComponent(id)}`,
		{
			method: 'POST',
			body: JSON.stringify({ idpIntentToken: token })
		}
	);

	if (!intent.userId) {
		throw redirect(302, '/ui/v2/login/login?error=idp_user_not_found');
	}

	const session = await zitadelFetch<{ sessionId: string; sessionToken: string }>('/v2/sessions', {
		method: 'POST',
		body: JSON.stringify({
			checks: {
				user: { userId: intent.userId },
				idpIntent: { idpIntentId: id, idpIntentToken: token }
			}
		})
	});

	if (authRequestId) {
		const cb = await zitadelFetch<{ callbackUrl: string }>(
			`/v2/oidc/auth_requests/${encodeURIComponent(authRequestId)}`,
			{
				method: 'POST',
				body: JSON.stringify({ session })
			}
		);
		throw redirect(302, cb.callbackUrl);
	}

	throw redirect(302, env.PUBLIC_BABASKETET_APP_URL || 'http://localhost:8081');
};
