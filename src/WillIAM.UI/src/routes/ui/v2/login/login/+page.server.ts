import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ url }) => {
	const authRequestId = url.searchParams.get('authRequest') || '';
	const error = url.searchParams.get('error') || '';
	return {
		authRequestId,
		error
	};
};
