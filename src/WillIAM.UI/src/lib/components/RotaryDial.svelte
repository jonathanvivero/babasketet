<script lang="ts">
	import {
		AUTH_METHODS,
		DIAL_STEP_DEG,
		getNorthMethodIndex,
		snapRotationDeg,
		targetRotationForIndex,
		type AuthMethodSpec
	} from '$lib/authMethods';

	interface Props {
		selectedId: AuthMethodSpec['id'];
		onSelect: (method: AuthMethodSpec) => void;
	}

	let { selectedId, onSelect }: Props = $props();

	let dialEl: HTMLDivElement | undefined = $state();
	let rotationDeg = $state(0);
	let isDragging = $state(false);
	let dragStartAngle = 0;
	let dragStartRotation = 0;
	let movedDuringDrag = false;

	const radiusPx = 122;

	$effect(() => {
		const currentIdx = AUTH_METHODS.findIndex((m) => m.id === selectedId);
		const northIdx = getNorthMethodIndex(rotationDeg);
		if (currentIdx >= 0 && currentIdx !== northIdx && !isDragging) {
			rotationDeg = targetRotationForIndex(currentIdx, rotationDeg);
		}
	});

	function pointerAngleDeg(clientX: number, clientY: number): number {
		if (!dialEl) return 0;
		const rect = dialEl.getBoundingClientRect();
		const cx = rect.left + rect.width / 2;
		const cy = rect.top + rect.height / 2;
		return (Math.atan2(clientY - cy, clientX - cx) * 180) / Math.PI;
	}

	function handlePointerDown(e: PointerEvent) {
		if ((e.target as HTMLElement)?.closest('[data-dial-stop="true"], button')) return;
		isDragging = true;
		movedDuringDrag = false;
		dragStartAngle = pointerAngleDeg(e.clientX, e.clientY);
		dragStartRotation = rotationDeg;
		dialEl?.setPointerCapture(e.pointerId);
	}

	function handlePointerMove(e: PointerEvent) {
		if (!isDragging) return;
		const current = pointerAngleDeg(e.clientX, e.clientY);
		let delta = current - dragStartAngle;
		if (delta > 180) delta -= 360;
		if (delta < -180) delta += 360;
		if (Math.abs(delta) > 3) movedDuringDrag = true;
		rotationDeg = dragStartRotation + delta;
		const idx = getNorthMethodIndex(rotationDeg);
		if (AUTH_METHODS[idx].id !== selectedId) {
			onSelect(AUTH_METHODS[idx]);
		}
	}

	function handlePointerUp(e: PointerEvent) {
		if (!isDragging) return;
		isDragging = false;
		dialEl?.releasePointerCapture(e.pointerId);
		rotationDeg = snapRotationDeg(rotationDeg);
		const idx = getNorthMethodIndex(rotationDeg);
		onSelect(AUTH_METHODS[idx]);
	}

	function selectByClick(index: number) {
		if (movedDuringDrag) return;
		rotationDeg = targetRotationForIndex(index, rotationDeg);
		onSelect(AUTH_METHODS[index]);
	}

	function stepDial(direction: 1 | -1) {
		rotationDeg = snapRotationDeg(rotationDeg + direction * DIAL_STEP_DEG);
		const idx = getNorthMethodIndex(rotationDeg);
		onSelect(AUTH_METHODS[idx]);
	}

	function handleWheel(e: WheelEvent) {
		e.preventDefault();
		stepDial(e.deltaY > 0 ? -1 : 1);
	}

	function handleKeyDown(e: KeyboardEvent) {
		if (e.key === 'ArrowRight' || e.key === 'ArrowDown') {
			e.preventDefault();
			stepDial(-1);
		} else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') {
			e.preventDefault();
			stepDial(1);
		}
	}

	const activeMethod = $derived(
		AUTH_METHODS.find((m) => m.id === selectedId) ?? AUTH_METHODS[0]
	);
</script>

<div class="dial-shell">
	<div class="north-beacon" aria-hidden="true">
		<span class="beacon-arrow">▼</span>
		<span class="beacon-tag">NORTE</span>
	</div>

	<div
		bind:this={dialEl}
		class="rotary-wheel"
		class:dragging={isDragging}
		role="listbox"
		tabindex="0"
		aria-label="Selector rotatorio de método de acceso"
		data-testid="auth-dial-wheel"
		data-selected-method={selectedId}
		onpointerdown={handlePointerDown}
		onpointermove={handlePointerMove}
		onpointerup={handlePointerUp}
		onwheel={handleWheel}
		onkeydown={handleKeyDown}
	>
		<div class="wheel-ring" style:transform={`rotate(${rotationDeg}deg)`}>
			{#each AUTH_METHODS as method, i (method.id)}
				{@const baseAngleDeg = i * DIAL_STEP_DEG - 90}
				{@const isNorth = method.id === selectedId}
				<button
					type="button"
					role="option"
					aria-selected={isNorth}
					aria-label={method.label}
					data-testid={`auth-dial-option-${method.id}`}
					class="dial-hole"
					class:active={isNorth}
					style:--angle={`${baseAngleDeg}deg`}
					style:--counter-rot={`${-rotationDeg}deg`}
					style:--radius={`${radiusPx}px`}
					style:--badge={method.badgeColor}
					onclick={() => selectByClick(i)}
				>
					<span class="hole-icon">{method.shortLabel}</span>
				</button>
			{/each}
		</div>

		<div class="center-hub" data-dial-stop="true">
			<div class="hub-brand">WillIAM</div>
			<div class="hub-method">{activeMethod.label}</div>
			<div class="hub-controls">
				<button
					type="button"
					class="step-btn"
					aria-label="Girar izquierda"
					data-testid="auth-dial-prev"
					onclick={() => stepDial(1)}
				>
					↺
				</button>
				<button
					type="button"
					class="step-btn"
					aria-label="Girar derecha"
					data-testid="auth-dial-next"
					onclick={() => stepDial(-1)}
				>
					↻
				</button>
			</div>
		</div>
	</div>
</div>

<style>
	.dial-shell {
		display: flex;
		flex-direction: column;
		align-items: center;
		user-select: none;
		touch-action: none;
		margin: 0.25rem 0 1rem;
	}

	.north-beacon {
		display: flex;
		flex-direction: column;
		align-items: center;
		margin-bottom: 0.35rem;
		color: #b45309;
		font-weight: 700;
		letter-spacing: 0.08em;
		font-size: 0.68rem;
	}

	.beacon-arrow {
		font-size: 0.9rem;
		line-height: 1;
	}

	.rotary-wheel {
		position: relative;
		width: 316px;
		height: 316px;
		border-radius: 50%;
		background: radial-gradient(circle at 30% 25%, #334155, #0f172a 75%);
		box-shadow:
			inset 0 2px 10px rgba(255, 255, 255, 0.16),
			0 16px 36px rgba(15, 23, 42, 0.35),
			0 0 0 6px #d97706;
		cursor: grab;
		outline: none;
	}

	.rotary-wheel.dragging {
		cursor: grabbing;
	}

	.rotary-wheel:focus-visible {
		box-shadow:
			inset 0 2px 10px rgba(255, 255, 255, 0.16),
			0 16px 36px rgba(15, 23, 42, 0.35),
			0 0 0 6px #f59e0b,
			0 0 0 10px rgba(245, 158, 11, 0.35);
	}

	.wheel-ring {
		position: absolute;
		inset: 0;
		transition: transform 240ms cubic-bezier(0.22, 1, 0.36, 1);
	}

	.rotary-wheel.dragging .wheel-ring {
		transition: none;
	}

	.dial-hole {
		position: absolute;
		left: 50%;
		top: 50%;
		width: 54px;
		height: 54px;
		margin-left: -27px;
		margin-top: -27px;
		border-radius: 50%;
		border: 2.5px solid rgba(255, 255, 255, 0.65);
		background: var(--badge);
		color: #ffffff;
		font-weight: 700;
		font-size: 0.95rem;
		display: grid;
		place-items: center;
		cursor: pointer;
		transform: rotate(var(--angle)) translate(var(--radius)) rotate(calc(-1 * var(--angle))) rotate(var(--counter-rot));
		box-shadow: inset 0 3px 6px rgba(0, 0, 0, 0.35), 0 3px 8px rgba(0, 0, 0, 0.4);
		transition: transform 240ms cubic-bezier(0.22, 1, 0.36, 1), box-shadow 180ms ease;
	}

	.rotary-wheel.dragging .dial-hole {
		transition: none;
	}

	.dial-hole.active {
		border-color: #fde047;
		box-shadow:
			0 0 0 4px rgba(253, 224, 71, 0.45),
			0 6px 14px rgba(0, 0, 0, 0.5);
	}

	.hole-icon {
		pointer-events: none;
	}

	.center-hub {
		position: absolute;
		left: 50%;
		top: 50%;
		width: 136px;
		height: 136px;
		transform: translate(-50%, -50%);
		border-radius: 50%;
		background: radial-gradient(circle at 35% 30%, #fef3c7, #fde68a 70%, #d97706 100%);
		color: #1e293b;
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		padding: 0.5rem;
		text-align: center;
		box-shadow: 0 4px 14px rgba(0, 0, 0, 0.45);
	}

	.hub-brand {
		font-size: 0.72rem;
		font-weight: 800;
		text-transform: uppercase;
		letter-spacing: 0.1em;
		color: #92400e;
	}

	.hub-method {
		font-size: 0.78rem;
		font-weight: 700;
		margin: 0.25rem 0 0.4rem;
		line-height: 1.15;
	}

	.hub-controls {
		display: flex;
		gap: 0.4rem;
	}

	.step-btn {
		width: 28px;
		height: 28px;
		border-radius: 50%;
		border: 1px solid #92400e;
		background: rgba(255, 255, 255, 0.75);
		color: #1e293b;
		font-weight: 700;
		cursor: pointer;
	}
</style>
