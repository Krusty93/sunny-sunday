import { test, expect } from '@playwright/test';

test.describe('Content Sections', () => {
	test.beforeEach(async ({ page }) => {
		await page.goto('/');
	});

	test('sections appear in correct order', async ({ page }) => {
		const sections = page.locator('section[id]');
		const ids = await sections.evaluateAll((els) => els.map((el) => el.id));
		const expected = ['hero', 'getting-started', 'why-relego'];
		expect(ids).toEqual(expect.arrayContaining(expected));
		const heroIdx = ids.indexOf('hero');
		const gsIdx = ids.indexOf('getting-started');
		const wrIdx = ids.indexOf('why-relego');
		expect(heroIdx).toBeLessThan(gsIdx);
		expect(gsIdx).toBeLessThan(wrIdx);
	});

	test('section titles use dual-color pattern with accent text', async ({ page }) => {
		const gsAccent = page.locator('#getting-started h2 span');
		await expect(gsAccent).toContainText('in just 3 steps');
		const wrAccent = page.locator('#why-relego h2 span');
		await expect(wrAccent).toContainText('built for readers');
	});

	test('Getting Started renders 3 step tabs with correct titles', async ({ page }) => {
		const section = page.locator('#getting-started');
		const tabs = section.locator('[data-step-tab]');
		await expect(tabs).toHaveCount(3);
		await expect(tabs.nth(0).locator('h3')).toHaveText('Sync your highlights');
		await expect(tabs.nth(1).locator('h3')).toHaveText('Let the server schedule');
		await expect(tabs.nth(2).locator('h3')).toHaveText('Read on your Kindle');
	});

	test('clicking a step tab switches the visible panel', async ({ page }) => {
		const section = page.locator('#getting-started');
		// Initially panel 0 is visible
		await expect(section.locator('[data-step-panel="0"]')).toBeVisible();
		await expect(section.locator('[data-step-panel="1"]')).toBeHidden();
		// Click step 2
		await section.locator('[data-step-tab="1"]').click();
		await expect(section.locator('[data-step-panel="1"]')).toBeVisible();
		await expect(section.locator('[data-step-panel="0"]')).toBeHidden();
	});

	test('Why Relego renders 4 feature cards with correct titles', async ({ page }) => {
		const section = page.locator('#why-relego');
		const titles = section.locator('h3');
		await expect(titles).toHaveCount(4);
		await expect(titles.nth(0)).toHaveText('Built for e-ink');
		await expect(titles.nth(1)).toHaveText('Free & self-hosted');
		await expect(titles.nth(2)).toHaveText('No lock-in');
		await expect(titles.nth(3)).toHaveText('Privacy');
	});

	test('Why Relego feature cards have icons', async ({ page }) => {
		const section = page.locator('#why-relego');
		const icons = section.locator('svg');
		await expect(icons).toHaveCount(4);
	});
});
