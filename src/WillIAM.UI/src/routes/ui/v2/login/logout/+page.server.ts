import { redirect } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = ({ url }) => {
	const target =
		url.searchParams.get('post_logout_redirect') ||
		env.PUBLIC_BABASKETET_APP_URL ||
		'http://localhost:8081';
	throw redirect(302, target);
};
