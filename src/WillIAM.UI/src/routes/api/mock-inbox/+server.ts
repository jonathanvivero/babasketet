import { json } from '@sveltejs/kit';
import { listMockNotifications } from '$lib/server/zitadel';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = () => {
	return json({ items: listMockNotifications() });
};
