import { expect, type Locator, type Page } from '@playwright/test';

export async function enableFlutterSemantics(page: Page): Promise<void> {
	await page.waitForSelector('flt-glass-pane, flutter-view', { timeout: 25_000 });
	const placeholder = page.locator('flt-semantics-placeholder');
	if ((await placeholder.count()) > 0) {
		await placeholder.first().dispatchEvent('click');
	}
	await page.waitForSelector('flt-semantics', { timeout: 15_000 });
}

export function sem(page: Page, identifier: string): Locator {
	return page.locator(`[flt-semantics-identifier="${identifier}"]`).first();
}

export async function fillFlutterText(locator: Locator, text: string): Promise<void> {
	await locator.click();
	const page = locator.page();
	await page.waitForTimeout(150);
	await page.keyboard.press('ControlOrMeta+a');
	await page.keyboard.press('Backspace');
	await page.keyboard.type(text, { delay: 15 });
}

export async function readSemanticText(locator: Locator): Promise<string> {
	const aria = (await locator.getAttribute('aria-label')) ?? '';
	const txt = (await locator.textContent()) ?? '';
	const childArias = await locator
		.locator('[aria-label]')
		.evaluateAll((els) => els.map((e) => e.getAttribute('aria-label') ?? '').join(' '))
		.catch(() => '');
	const inputVals = await locator
		.locator('input, textarea')
		.evaluateAll((els) => els.map((e) => (e as HTMLInputElement).value ?? '').join(' '))
		.catch(() => '');
	return `${aria} ${txt} ${childArias} ${inputVals}`.trim();
}

export async function expectSemanticContains(
	locator: Locator,
	expected: string | RegExp
): Promise<void> {
	await expect(async () => {
		const combined = await readSemanticText(locator);
		if (typeof expected === 'string') {
			expect(combined).toContain(expected);
		} else {
			expect(combined).toMatch(expected);
		}
	}).toPass({ timeout: 15_000 });
}

export async function scrollUntilVisible(
	page: Page,
	locator: Locator,
	xFraction = 0.5
): Promise<void> {
	const vp = page.viewportSize() ?? { width: 412, height: 915 };
	await page.mouse.move(vp.width * xFraction, vp.height * 0.7);
	for (let i = 0; i < 15; i++) {
		const visible = await locator.isVisible().catch(() => false);
		if (visible) {
			const box = await locator.boundingBox().catch(() => null);
			if (box && box.y >= 0 && box.y + box.height <= vp.height - 16) {
				return;
			}
		}
		await page.mouse.wheel(0, 320);
		await page.waitForTimeout(150);
	}
	await expect(locator).toBeVisible();
}

export async function longPress800ms(page: Page, locator: Locator): Promise<void> {
	const box = await locator.boundingBox();
	expect(box).not.toBeNull();
	const x = box!.x + box!.width / 2;
	const y = box!.y + box!.height / 2;
	await page.mouse.move(x, y);
	await page.mouse.down();
	await page.waitForTimeout(950);
	await page.mouse.up();
}

export async function swipeHorizontal(
	page: Page,
	locator: Locator,
	direction: 'left' | 'right'
): Promise<void> {
	const box = await locator.boundingBox();
	expect(box).not.toBeNull();
	const startX = direction === 'right' ? box!.x + 30 : box!.x + box!.width - 30;
	const endX = direction === 'right' ? box!.x + box!.width - 20 : box!.x + 20;
	const y = box!.y + box!.height / 2;
	await page.mouse.move(startX, y);
	await page.mouse.down();
	await page.mouse.move(endX, y, { steps: 12 });
	await page.mouse.up();
}

export async function verticalDrag(
	page: Page,
	locator: Locator,
	deltaY: number
): Promise<void> {
	const box = await locator.boundingBox();
	expect(box).not.toBeNull();
	const x = box!.x + box!.width / 2;
	const y = box!.y + box!.height / 2;
	await page.mouse.move(x, y);
	await page.mouse.down();
	await page.mouse.move(x, y + deltaY, { steps: 10 });
	await page.mouse.up();
}
