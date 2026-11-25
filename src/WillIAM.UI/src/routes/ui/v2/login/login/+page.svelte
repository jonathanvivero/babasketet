<script lang="ts">
	import { startAuthentication, startRegistration } from '@simplewebauthn/browser';
	import { AUTH_METHODS, type AuthMethodId, type AuthMethodSpec } from '$lib/authMethods';
	import RotaryDial from '$lib/components/RotaryDial.svelte';

	let { data } = $props();

	let selectedId = $state<AuthMethodId>('passkey');
	let mode = $state<'login' | 'signup'>('login');
	let email = $state('');
	let password = $state('');
	let name = $state('');
	let countryCode = $state('+34');
	let phoneNumber = $state('');
	let otpCode = $state('');
	let pendingSession = $state<{ sessionId: string; sessionToken: string; channel: 'email' | 'sms' } | null>(
		null
	);
	let pendingPasswordChange = $state<{
		userId: string;
		sessionId: string;
		sessionToken: string;
		currentPassword: string;
	} | null>(null);
	let newPassword = $state('');
	let confirmPassword = $state('');
	let busy = $state(false);
	let errorMessage = $state('');
	let infoMessage = $state('');

	$effect(() => {
		if (data.error) {
			errorMessage = 'No se pudo completar el acceso con el proveedor externo.';
		}
	});

	const selectedMethod = $derived(
		AUTH_METHODS.find((m) => m.id === selectedId) ?? AUTH_METHODS[0]
	);

	function handleSelect(method: AuthMethodSpec) {
		selectedId = method.id;
		errorMessage = '';
		infoMessage = '';
		pendingSession = null;
		pendingPasswordChange = null;
	}

	async function postAuth(action: string, payload: Record<string, unknown>) {
		const res = await fetch(`/api/auth/${action}`, {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify(payload)
		});
		const body = await res.json();
		if (!res.ok) {
			throw new Error(body.error || 'Error inesperado');
		}
		return body;
	}

	async function submitPassword(e: Event) {
		e.preventDefault();
		busy = true;
		errorMessage = '';
		infoMessage = '';
		try {
			const res = await postAuth('password', {
				mode,
				email,
				password,
				name,
				authRequestId: data.authRequestId
			});
			if (res.requiresPasswordChange) {
				pendingPasswordChange = {
					userId: res.userId,
					sessionId: res.sessionId,
					sessionToken: res.sessionToken,
					currentPassword: password
				};
				newPassword = '';
				confirmPassword = '';
				infoMessage = 'Por seguridad, debes cambiar tu contraseña en el primer acceso.';
				return;
			}
			window.location.href = res.callbackUrl;
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Error de acceso';
		} finally {
			busy = false;
		}
	}

	async function submitPasswordChange(e: Event) {
		e.preventDefault();
		if (!pendingPasswordChange) return;
		if (newPassword !== confirmPassword) {
			errorMessage = 'Las contraseñas nuevas no coinciden.';
			return;
		}
		busy = true;
		errorMessage = '';
		try {
			const res = await postAuth('change-password', {
				...pendingPasswordChange,
				newPassword,
				authRequestId: data.authRequestId
			});
			window.location.href = res.callbackUrl;
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'No se pudo cambiar la contraseña';
		} finally {
			busy = false;
		}
	}

	async function runPasskeyLogin() {
		busy = true;
		errorMessage = '';
		try {
			const start = await postAuth('passkey-login-start', { loginName: email });
			const optionsJSON = start.options?.publicKey ?? start.options;
			const credential = await startAuthentication({ optionsJSON });
			const finish = await postAuth('passkey-login-finish', {
				sessionId: start.sessionId,
				sessionToken: start.sessionToken,
				credential,
				authRequestId: data.authRequestId
			});
			window.location.href = finish.callbackUrl;
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Error con Passkey';
		} finally {
			busy = false;
		}
	}

	async function runPasskeyRegister() {
		busy = true;
		errorMessage = '';
		try {
			const start = await postAuth('passkey-register-start', { email, name });
			const rawOpts = start.publicKeyCredentialCreationOptions;
			const optionsJSON = rawOpts?.publicKey ?? rawOpts;
			const credential = await startRegistration({ optionsJSON });
			const finish = await postAuth('passkey-register-finish', {
				userId: start.userId,
				passkeyId: start.passkeyId,
				credential,
				authRequestId: data.authRequestId
			});
			window.location.href = finish.callbackUrl;
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'No se pudo registrar la Passkey';
		} finally {
			busy = false;
		}
	}

	async function startMagicLink(channel: 'email' | 'sms') {
		busy = true;
		errorMessage = '';
		infoMessage = '';
		try {
			if (channel === 'email') {
				const res = await postAuth('email-magic-start', {
					email,
					authRequestId: data.authRequestId
				});
				pendingSession = { sessionId: res.sessionId, sessionToken: res.sessionToken, channel: 'email' };
				infoMessage = 'Hemos enviado un MagicLink / código a tu correo.';
			} else {
				const res = await postAuth('phone-magic-start', {
					countryCode,
					phoneNumber,
					authRequestId: data.authRequestId
				});
				pendingSession = { sessionId: res.sessionId, sessionToken: res.sessionToken, channel: 'sms' };
				infoMessage = `Hemos enviado un MagicLink / código a ${res.phoneE164}.`;
			}
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'No se pudo enviar el enlace';
		} finally {
			busy = false;
		}
	}

	async function verifyOtp(e: Event) {
		e.preventDefault();
		if (!pendingSession) return;
		busy = true;
		errorMessage = '';
		try {
			const res = await postAuth('otp-verify', {
				...pendingSession,
				code: otpCode,
				authRequestId: data.authRequestId
			});
			window.location.href = res.callbackUrl;
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'Código incorrecto';
		} finally {
			busy = false;
		}
	}

	async function startSocial() {
		busy = true;
		errorMessage = '';
		try {
			const res = await postAuth('social-start', {
				provider: selectedMethod.providerKey || selectedMethod.id,
				authRequestId: data.authRequestId
			});
			window.location.href = res.authUrl;
		} catch (err) {
			errorMessage = err instanceof Error ? err.message : 'SSO no disponible';
			busy = false;
		}
	}
</script>

<main class="auth-page">
	<section class="auth-card">
		<header class="card-header">
			<h1>Babasketet · WillIAM</h1>
			<p>Gira el disco y deja en el <strong>NORTE</strong> tu método de acceso</p>
		</header>

		<div class="mode-switch">
			<button
				type="button"
				class:active={mode === 'login'}
				data-testid="mode-login-btn"
				onclick={() => (mode = 'login')}
			>
				Entrar
			</button>
			<button
				type="button"
				class:active={mode === 'signup'}
				data-testid="mode-signup-btn"
				onclick={() => (mode = 'signup')}
			>
				Crear cuenta
			</button>
		</div>

		<RotaryDial {selectedId} onSelect={handleSelect} />

		{#if errorMessage}
			<div class="alert error" role="alert" data-testid="auth-error">{errorMessage}</div>
		{/if}
		{#if infoMessage}
			<div class="alert info" role="status" data-testid="auth-info">{infoMessage}</div>
		{/if}

		<div class="method-panel" data-testid={`panel-${selectedId}`}>
			{#if selectedMethod.kind === 'passkey'}
				<div class="form-stack">
					<label>
						<span>Email (opcional al entrar, requerido al registrar)</span>
						<input
							type="email"
							bind:value={email}
							placeholder="tu@correo.com"
							data-testid="passkey-email-input"
						/>
					</label>
					{#if mode === 'signup'}
						<label>
							<span>Nombre</span>
							<input type="text" bind:value={name} placeholder="Tu nombre" data-testid="passkey-name-input" />
						</label>
						<button
							type="button"
							class="primary-btn"
							disabled={busy}
							data-testid="passkey-register-btn"
							onclick={runPasskeyRegister}
						>
							{busy ? 'Esperando dispositivo...' : 'Crear cuenta con Passkey'}
						</button>
					{:else}
						<button
							type="button"
							class="primary-btn"
							disabled={busy}
							data-testid="passkey-login-btn"
							onclick={runPasskeyLogin}
						>
							{busy ? 'Verificando Passkey...' : 'Entrar con Passkey'}
						</button>
					{/if}
				</div>
			{:else if selectedMethod.kind === 'password'}
				{#if pendingPasswordChange}
					<form class="form-stack" onsubmit={submitPasswordChange} data-testid="pwd-change-form">
						<label>
							<span>Nueva contraseña</span>
							<input
								type="password"
								required
								minlength="8"
								bind:value={newPassword}
								placeholder="Mínimo 8 caracteres"
								data-testid="pwd-new-input"
							/>
						</label>
						<label>
							<span>Confirmar nueva contraseña</span>
							<input
								type="password"
								required
								minlength="8"
								bind:value={confirmPassword}
								placeholder="Repite la nueva contraseña"
								data-testid="pwd-confirm-input"
							/>
						</label>
						<button type="submit" class="primary-btn" disabled={busy} data-testid="pwd-change-submit-btn">
							{busy ? 'Actualizando...' : 'Guardar contraseña y entrar'}
						</button>
					</form>
				{:else}
					<form class="form-stack" onsubmit={submitPassword}>
						{#if mode === 'signup'}
							<label>
								<span>Nombre</span>
								<input type="text" bind:value={name} placeholder="Tu nombre" data-testid="pwd-name-input" />
							</label>
						{/if}
						<label>
							<span>Correo electrónico</span>
							<input
								type="email"
								required
								bind:value={email}
								placeholder="tu@correo.com"
								data-testid="pwd-email-input"
							/>
						</label>
						<label>
							<span>Contraseña</span>
							<input
								type="password"
								required
								bind:value={password}
								placeholder="••••••••"
								data-testid="pwd-password-input"
							/>
						</label>
						<button type="submit" class="primary-btn" disabled={busy} data-testid="pwd-submit-btn">
							{mode === 'signup' ? 'Registrarme' : 'Entrar'}
						</button>
					</form>
				{/if}
			{:else if selectedMethod.kind === 'email-magic'}
				{#if !pendingSession}
					<form
						class="form-stack"
						onsubmit={(e) => {
							e.preventDefault();
							startMagicLink('email');
						}}
					>
						<label>
							<span>Correo electrónico</span>
							<input
								type="email"
								required
								bind:value={email}
								placeholder="tu@correo.com"
								data-testid="magic-email-input"
							/>
						</label>
						<button type="submit" class="primary-btn" disabled={busy} data-testid="magic-email-submit">
							Enviar MagicLink por Email
						</button>
					</form>
				{:else}
					<form class="form-stack" onsubmit={verifyOtp}>
						<label>
							<span>Código de verificación</span>
							<input type="text" required bind:value={otpCode} placeholder="123456" data-testid="magic-otp-input" />
						</label>
						<button type="submit" class="primary-btn" disabled={busy} data-testid="magic-otp-verify">
							Confirmar acceso
						</button>
					</form>
				{/if}
			{:else if selectedMethod.kind === 'phone-magic'}
				{#if !pendingSession}
					<form
						class="form-stack"
						onsubmit={(e) => {
							e.preventDefault();
							startMagicLink('sms');
						}}
					>
						<div class="phone-row">
							<label class="prefix-field">
								<span>País</span>
								<input
									type="text"
									required
									bind:value={countryCode}
									placeholder="+34"
									data-testid="phone-country-input"
								/>
							</label>
							<label class="number-field">
								<span>Número</span>
								<input
									type="tel"
									required
									bind:value={phoneNumber}
									placeholder="600112233"
									data-testid="phone-number-input"
								/>
							</label>
						</div>
						<button type="submit" class="primary-btn" disabled={busy} data-testid="phone-magic-submit">
							Enviar MagicLink por SMS
						</button>
					</form>
				{:else}
					<form class="form-stack" onsubmit={verifyOtp}>
						<label>
							<span>Código recibido por SMS</span>
							<input type="text" required bind:value={otpCode} placeholder="123456" data-testid="phone-otp-input" />
						</label>
						<button type="submit" class="primary-btn" disabled={busy} data-testid="phone-otp-verify">
							Confirmar acceso
						</button>
					</form>
				{/if}
			{:else}
				<div class="form-stack">
					<button
						type="button"
						class="primary-btn"
						disabled={busy}
						data-testid={`social-continue-${selectedId}`}
						onclick={startSocial}
					>
						Continuar con {selectedMethod.label}
					</button>
				</div>
			{/if}
		</div>
	</section>
</main>

<style>
	:global(body) {
		margin: 0;
		font-family: system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif;
		background: linear-gradient(160deg, #fffbeb 0%, #f1f5f9 100%);
		color: #0f172a;
	}

	.auth-page {
		min-height: 100vh;
		display: grid;
		place-items: center;
		padding: 1rem;
	}

	.auth-card {
		width: 100%;
		max-width: 410px;
		background: #ffffff;
		border-radius: 24px;
		padding: 1.5rem 1.25rem;
		box-shadow: 0 20px 50px rgba(15, 23, 42, 0.12);
		display: flex;
		flex-direction: column;
		align-items: center;
	}

	.card-header {
		text-align: center;
		margin-bottom: 0.75rem;
	}

	.card-header h1 {
		margin: 0;
		font-size: 1.35rem;
	}

	.card-header p {
		margin: 0.35rem 0 0;
		font-size: 0.88rem;
		color: #475569;
	}

	.mode-switch {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 0.35rem;
		width: 100%;
		max-width: 260px;
		background: #f1f5f9;
		padding: 0.25rem;
		border-radius: 999px;
		margin-bottom: 0.75rem;
	}

	.mode-switch button {
		border: none;
		background: transparent;
		padding: 0.45rem 0.75rem;
		border-radius: 999px;
		font-weight: 600;
		cursor: pointer;
		color: #475569;
	}

	.mode-switch button.active {
		background: #0f172a;
		color: #ffffff;
	}

	.method-panel {
		width: 100%;
	}

	.form-stack {
		display: flex;
		flex-direction: column;
		gap: 0.75rem;
	}

	label {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
		font-size: 0.85rem;
		font-weight: 600;
		color: #334155;
	}

	input {
		padding: 0.7rem 0.85rem;
		border-radius: 12px;
		border: 1.5px solid #cbd5e1;
		font-size: 1rem;
	}

	.phone-row {
		display: flex;
		gap: 0.5rem;
	}

	.prefix-field {
		width: 90px;
	}

	.number-field {
		flex: 1;
	}

	.primary-btn {
		padding: 0.85rem 1rem;
		border-radius: 14px;
		border: none;
		background: #0f172a;
		color: #ffffff;
		font-size: 1rem;
		font-weight: 700;
		cursor: pointer;
	}

	.primary-btn:disabled {
		opacity: 0.6;
		cursor: wait;
	}

	.alert {
		width: 100%;
		padding: 0.65rem 0.85rem;
		border-radius: 12px;
		font-size: 0.85rem;
		margin-bottom: 0.75rem;
		box-sizing: border-box;
	}

	.alert.error {
		background: #fee2e2;
		color: #991b1b;
	}

	.alert.info {
		background: #dcfce7;
		color: #166534;
	}
</style>
