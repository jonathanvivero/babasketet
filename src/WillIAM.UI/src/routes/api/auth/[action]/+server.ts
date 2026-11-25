import { json } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import { formatE164Phone } from '$lib/authMethods';
import {
	getZitadelConfig,
	recordMockNotification,
	zitadelFetch
} from '$lib/server/zitadel';
import type { RequestHandler } from './$types';

interface SessionResponse {
	sessionId: string;
	sessionToken: string;
	challenges?: {
		webAuthN?: {
			publicKeyCredentialRequestOptions?: unknown;
		};
		otpEmail?: string;
		otpSms?: string;
	};
}

interface AuthCallbackResponse {
	callbackUrl: string;
}

async function finalizeAuthRequest(
	authRequestId: string | undefined,
	sessionId: string,
	sessionToken: string
): Promise<string> {
	if (!authRequestId) {
		return env.PUBLIC_BABASKETET_APP_URL || 'http://localhost:8081';
	}
	const res = await zitadelFetch<AuthCallbackResponse>(
		`/v2/oidc/auth_requests/${encodeURIComponent(authRequestId)}`,
		{
			method: 'POST',
			body: JSON.stringify({
				session: { sessionId, sessionToken }
			})
		}
	);
	return res.callbackUrl;
}

async function findUserByLoginOrEmail(identifier: string): Promise<string | null> {
	const res = await zitadelFetch<{ result?: Array<{ userId: string }> }>('/v2/users', {
		method: 'POST',
		useAdminToken: true,
		body: JSON.stringify({
			queries: [
				{
					orQuery: {
						queries: [
							{ loginNameQuery: { loginName: identifier, method: 'TEXT_QUERY_METHOD_EQUALS' } },
							{ emailQuery: { emailAddress: identifier, method: 'TEXT_QUERY_METHOD_EQUALS' } }
						]
					}
				}
			]
		})
	});
	return res.result?.[0]?.userId ?? null;
}

async function findUserByPhone(phoneE164: string): Promise<string | null> {
	const res = await zitadelFetch<{ result?: Array<{ userId: string }> }>('/v2/users', {
		method: 'POST',
		useAdminToken: true,
		body: JSON.stringify({
			queries: [
				{
					phoneQuery: { number: phoneE164, method: 'TEXT_QUERY_METHOD_EQUALS' }
				}
			]
		})
	});
	return res.result?.[0]?.userId ?? null;
}

export const POST: RequestHandler = async ({ params, request, url }) => {
	const action = params.action;
	const body = await request.json();
	const cfg = getZitadelConfig();

	try {
		if (action === 'password') {
			const { mode, email, password, name, authRequestId } = body as {
				mode: 'login' | 'signup';
				email: string;
				password: string;
				name?: string;
				authRequestId?: string;
			};

			if (!email || !password) {
				return json({ error: 'Email y contraseña obligatorios.' }, { status: 400 });
			}

			if (mode === 'signup') {
				const parts = (name?.trim() || email.split('@')[0]).split(/\s+/);
				const givenName = parts[0] || 'Usuario';
				const familyName = parts.slice(1).join(' ') || 'Babasketet';
				await zitadelFetch('/v2/users/human', {
					method: 'POST',
					useAdminToken: true,
					body: JSON.stringify({
						username: email.trim().toLowerCase(),
						profile: { givenName, familyName },
						email: { email: email.trim().toLowerCase(), isVerified: true },
						password: { password, changeRequired: false }
					})
				});
			}

			const session = await zitadelFetch<SessionResponse>('/v2/sessions', {
				method: 'POST',
				body: JSON.stringify({
					checks: {
						user: { loginName: email.trim().toLowerCase() },
						password: { password }
					}
				})
			});

			const callbackUrl = await finalizeAuthRequest(authRequestId, session.sessionId, session.sessionToken);
			return json({ callbackUrl, sessionId: session.sessionId });
		}

		if (action === 'passkey-login-start') {
			const { loginName } = body as { loginName?: string };
			const checks = loginName?.trim()
				? { user: { loginName: loginName.trim().toLowerCase() } }
				: undefined;

			const session = await zitadelFetch<SessionResponse>('/v2/sessions', {
				method: 'POST',
				body: JSON.stringify({
					...(checks ? { checks } : {}),
					challenges: {
						webAuthN: {
							domain: cfg.rpId,
							userVerificationRequirement: 'USER_VERIFICATION_REQUIREMENT_PREFERRED'
						}
					}
				})
			});

			return json({
				sessionId: session.sessionId,
				sessionToken: session.sessionToken,
				options: session.challenges?.webAuthN?.publicKeyCredentialRequestOptions
			});
		}

		if (action === 'passkey-login-finish') {
			const { sessionId, sessionToken, credential, authRequestId } = body as {
				sessionId: string;
				sessionToken: string;
				credential: unknown;
				authRequestId?: string;
			};

			const updated = await zitadelFetch<SessionResponse>(`/v2/sessions/${encodeURIComponent(sessionId)}`, {
				method: 'PATCH',
				body: JSON.stringify({
					sessionToken,
					checks: {
						webAuthN: {
							credentialAssertionData: credential
						}
					}
				})
			});

			const callbackUrl = await finalizeAuthRequest(
				authRequestId,
				updated.sessionId || sessionId,
				updated.sessionToken || sessionToken
			);
			return json({ callbackUrl });
		}

		if (action === 'passkey-register-start') {
			const { email, name } = body as { email: string; name?: string };
			const cleanEmail = email?.trim().toLowerCase();
			if (!cleanEmail) {
				return json({ error: 'Indica tu email para asociar la Passkey.' }, { status: 400 });
			}

			let userId = await findUserByLoginOrEmail(cleanEmail);
			if (!userId) {
				const parts = (name?.trim() || cleanEmail.split('@')[0]).split(/\s+/);
				const created = await zitadelFetch<{ userId: string }>('/v2/users/human', {
					method: 'POST',
					useAdminToken: true,
					body: JSON.stringify({
						username: cleanEmail,
						profile: { givenName: parts[0] || 'Usuario', familyName: parts.slice(1).join(' ') || 'Babasketet' },
						email: { email: cleanEmail, isVerified: true }
					})
				});
				userId = created.userId;
			}

			const reg = await zitadelFetch<{
				passkeyId: string;
				publicKeyCredentialCreationOptions: unknown;
			}>(`/v2/users/${encodeURIComponent(userId)}/passkeys`, {
				method: 'POST',
				useAdminToken: true,
				body: JSON.stringify({
					authenticator: 'PASSKEY_AUTHENTICATOR_UNSPECIFIED',
					domain: cfg.rpId
				})
			});

			return json({ userId, ...reg });
		}

		if (action === 'passkey-register-finish') {
			const { userId, passkeyId, credential, authRequestId } = body as {
				userId: string;
				passkeyId: string;
				credential: unknown;
				authRequestId?: string;
			};

			await zitadelFetch(
				`/v2/users/${encodeURIComponent(userId)}/passkeys/${encodeURIComponent(passkeyId)}`,
				{
					method: 'POST',
					useAdminToken: true,
					body: JSON.stringify({
						publicKeyCredential: credential,
						passkeyName: 'Passkey Babasketet'
					})
				}
			);

			const session = await zitadelFetch<SessionResponse>('/v2/sessions', {
				method: 'POST',
				body: JSON.stringify({
					checks: { user: { userId } }
				})
			});

			const callbackUrl = await finalizeAuthRequest(authRequestId, session.sessionId, session.sessionToken);
			return json({ callbackUrl });
		}

		if (action === 'email-magic-start') {
			const { email, authRequestId } = body as { email: string; authRequestId?: string };
			const cleanEmail = email?.trim().toLowerCase();
			if (!cleanEmail) {
				return json({ error: 'Introduce un email válido.' }, { status: 400 });
			}

			let userId = await findUserByLoginOrEmail(cleanEmail);
			if (!userId) {
				const created = await zitadelFetch<{ userId: string }>('/v2/users/human', {
					method: 'POST',
					useAdminToken: true,
					body: JSON.stringify({
						username: cleanEmail,
						profile: { givenName: cleanEmail.split('@')[0], familyName: 'Babasketet' },
						email: { email: cleanEmail, isVerified: true }
					})
				});
				userId = created.userId;
			}

			try {
				await zitadelFetch(`/v2/users/${encodeURIComponent(userId)}/otp_email`, {
					method: 'POST',
					useAdminToken: true,
					body: JSON.stringify({})
				});
			} catch (err) {
				if (!String(err).toLowerCase().includes('already')) {
					console.warn('otp_email setup note:', err);
				}
			}

			const session = await zitadelFetch<SessionResponse>('/v2/sessions', {
				method: 'POST',
				body: JSON.stringify({
					checks: { user: { userId } },
					challenges: {
						otpEmail: {
							returnCode: {}
						}
					}
				})
			});

			const code = session.challenges?.otpEmail ?? '';
			const magicUrl = `${url.origin}/ui/v2/login/magic?sessionId=${encodeURIComponent(session.sessionId)}&sessionToken=${encodeURIComponent(session.sessionToken)}&code=${encodeURIComponent(code)}&channel=email&authRequest=${encodeURIComponent(authRequestId || '')}`;

			recordMockNotification({
				channel: 'email',
				recipient: cleanEmail,
				code,
				magicUrl
			});

			// En dev/test enviamos tambien a Mailpit si está configurado SMTP_HOST o Mailpit HTTP
			const mailpitHost = env.SMTP_HOST || 'mailpit';
			try {
				await fetch(`http://${mailpitHost}:8025/api/v1/send`, {
					method: 'POST',
					headers: { 'Content-Type': 'application/json' },
					body: JSON.stringify({
						From: { Email: 'noreply@babasketet.localhost', Name: 'WillIAM IAM' },
						To: [{ Email: cleanEmail }],
						Subject: 'Tu enlace mágico de acceso a Babasketet',
						Text: `Código: ${code}\nEnlace mágico: ${magicUrl}`,
						HTML: `<p>Pulsa el enlace mágico para acceder:</p><p><a href="${magicUrl}">${magicUrl}</a></p><p>O introduce el código: <strong>${code}</strong></p>`
					})
				});
			} catch {
				// En entorno sin Mailpit, queda en mockInbox
			}

			return json({
				sessionId: session.sessionId,
				sessionToken: session.sessionToken,
				sent: true
			});
		}

		if (action === 'phone-magic-start') {
			const { countryCode, phoneNumber, authRequestId } = body as {
				countryCode: string;
				phoneNumber: string;
				authRequestId?: string;
			};

			const phoneE164 = formatE164Phone(countryCode, phoneNumber);
			let userId = await findUserByPhone(phoneE164);
			if (!userId) {
				const syntheticEmail = `${phoneE164.replace('+', 'tel_')}@phone.babasketet.local`;
				const created = await zitadelFetch<{ userId: string }>('/v2/users/human', {
					method: 'POST',
					useAdminToken: true,
					body: JSON.stringify({
						username: phoneE164,
						profile: { givenName: 'Usuario', familyName: phoneE164 },
						email: { email: syntheticEmail, isVerified: true },
						phone: { phone: phoneE164, isVerified: true }
					})
				});
				userId = created.userId;
			}

			try {
				await zitadelFetch(`/v2/users/${encodeURIComponent(userId)}/otp_sms`, {
					method: 'POST',
					useAdminToken: true,
					body: JSON.stringify({})
				});
			} catch (err) {
				if (!String(err).toLowerCase().includes('already')) {
					console.warn('otp_sms setup note:', err);
				}
			}

			const session = await zitadelFetch<SessionResponse>('/v2/sessions', {
				method: 'POST',
				body: JSON.stringify({
					checks: { user: { userId } },
					challenges: {
						otpSms: {
							returnCode: true
						}
					}
				})
			});

			const code = session.challenges?.otpSms ?? '';
			const magicUrl = `${url.origin}/ui/v2/login/magic?sessionId=${encodeURIComponent(session.sessionId)}&sessionToken=${encodeURIComponent(session.sessionToken)}&code=${encodeURIComponent(code)}&channel=sms&authRequest=${encodeURIComponent(authRequestId || '')}`;

			recordMockNotification({
				channel: 'sms',
				recipient: phoneE164,
				code,
				magicUrl
			});

			return json({
				sessionId: session.sessionId,
				sessionToken: session.sessionToken,
				phoneE164,
				sent: true
			});
		}

		if (action === 'otp-verify') {
			const { sessionId, sessionToken, code, channel, authRequestId } = body as {
				sessionId: string;
				sessionToken: string;
				code: string;
				channel: 'email' | 'sms';
				authRequestId?: string;
			};

			const checks = channel === 'sms' ? { otpSms: { code } } : { otpEmail: { code } };
			const updated = await zitadelFetch<SessionResponse>(`/v2/sessions/${encodeURIComponent(sessionId)}`, {
				method: 'PATCH',
				body: JSON.stringify({
					sessionToken,
					checks
				})
			});

			const callbackUrl = await finalizeAuthRequest(
				authRequestId,
				updated.sessionId || sessionId,
				updated.sessionToken || sessionToken
			);
			return json({ callbackUrl });
		}

		if (action === 'social-start') {
			const { provider, authRequestId } = body as { provider: string; authRequestId?: string };
			const idps = await zitadelFetch<{ result?: Array<{ id: string; name: string; type: string }> }>(
				'/management/v1/idps/_search',
				{
					method: 'POST',
					useAdminToken: true,
					body: JSON.stringify({})
				}
			);

			const match = idps.result?.find((i) =>
				i.name.toLowerCase().includes(provider.toLowerCase()) ||
				i.type.toLowerCase().includes(provider.toLowerCase())
			);

			if (!match) {
				return json(
					{
						error: `El proveedor social '${provider}' aún no tiene credenciales OAuth configuradas en Zitadel.`
					},
					{ status: 400 }
				);
			}

			const intent = await zitadelFetch<{ authUrl: string }>('/v2/idp_intents', {
				method: 'POST',
				body: JSON.stringify({
					idpId: match.id,
					urls: {
						successUrl: `${url.origin}/ui/v2/login/idp/callback?authRequest=${encodeURIComponent(authRequestId || '')}`,
						failureUrl: `${url.origin}/ui/v2/login/login?authRequest=${encodeURIComponent(authRequestId || '')}&error=idp_failed`
					}
				})
			});

			return json({ authUrl: intent.authUrl });
		}

		return json({ error: 'Acción no soportada.' }, { status: 404 });
	} catch (err) {
		const message = err instanceof Error ? err.message : 'Error de autenticación';
		return json({ error: message }, { status: 400 });
	}
};
