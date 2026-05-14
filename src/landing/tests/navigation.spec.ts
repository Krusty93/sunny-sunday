import { test, expect } from '@playwright/test';

test.describe('Navigation', () => {
	test.beforeEach(async ({ page }) => {
		await page.goto('/');
	});

	test('navbar renders with "relego." logotype text', async ({ page }) => {
		const logotype = page.locator('nav a').first();
		await expect(logotype).toHaveText('relego.');
	});

	test('"View on GitHub" link points to configured GitHub URL', async ({ page }) => {
		const githubLink = page.locator('nav a[href*="github.com"]').first();
		await expect(githubLink).toBeVisible();
		await expect(githubLink).toHaveAttribute('href', 'https://github.com/Krusty93/relego');
	});

	test('"View on GitHub" link opens in a new tab', async ({ page }) => {
		const githubLink = page.locator('nav a[href*="github.com"]').first();
		await expect(githubLink).toHaveAttribute('target', '_blank');
		await expect(githubLink).toHaveAttribute('rel', /noopener/);
	});

	test('clicking logotype navigates to top of page', async ({ page }) => {
		const logotype = page.locator('nav a').first();
		await expect(logotype).toHaveAttribute('href', '#');
	});

	test('footer renders with "Relego · MIT License"', async ({ page }) => {
		const footer = page.locator('footer');
		await expect(footer).toBeVisible();
		await expect(footer).toContainText('Relego · MIT License');
	});
});
