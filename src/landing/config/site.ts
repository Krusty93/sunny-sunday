export interface SiteConfig {
	name: string;
	logotype: string;
	tagline: string;
	githubUrl: string;
	docsUrl: string;
	license: string;
}

export const siteConfig = {
	name: 'Relego',
	logotype: 'relego.',
	tagline: 'Periodic highlights recap, delivered to your Kindle. For free.',
	githubUrl: 'https://github.com/Krusty93/relego',
	docsUrl: '/docs',
	license: 'MIT',
} as const satisfies SiteConfig;
