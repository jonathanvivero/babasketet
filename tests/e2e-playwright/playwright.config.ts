import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
	testDir: './specs',
	timeout: 60_000,
	expect: {
		timeout: 15_000
	},
	fullyParallel: false,
	workers: 1,
	reporter: [['list'], ['html', { open: 'never' }]],
	use: {
		baseURL: process.env.BASE_URL || 'http://localhost:8081',
		trace: 'retain-on-failure',
		screenshot: 'only-on-failure',
		video: 'retain-on-failure'
	},
	projects: [
		{
			name: 'mobile-chromium',
			use: { ...devices['Pixel 7'] }
		}
	]
});
