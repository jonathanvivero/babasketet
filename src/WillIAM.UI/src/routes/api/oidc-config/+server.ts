import fs from 'node:fs';
import { json } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import type { RequestHandler } from './$types';

export const GET: RequestHandler = () => {
	const clientIdFile = env.ZITADEL_CLIENT_ID_FILE || '/zitadel-bootstrap/client-id.txt';
	const clientId = fs.existsSync(clientIdFile)
		? fs.readFileSync(clientIdFile, 'utf8').trim()
		: env.ZITADEL_CLIENT_ID || '';

	return json(
		{
			issuer: env.ZITADEL_PUBLIC_URL || 'http://localhost:8085',
			clientId
		},
		{
			headers: {
				'Access-Control-Allow-Origin': '*'
			}
		}
	);
};
