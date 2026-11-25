import fs from 'node:fs';
import http from 'node:http';
import https from 'node:https';

const ZITADEL_API_URL = (process.env.ZITADEL_API_URL || 'http://zitadel:8080').replace(/\/$/, '');
const ZITADEL_PUBLIC_URL = (process.env.ZITADEL_PUBLIC_URL || 'http://localhost:8085').replace(/\/$/, '');
const ADMIN_TOKEN_FILE = process.env.ZITADEL_ADMIN_TOKEN_FILE || '/zitadel-bootstrap/admin.pat';
const LOGIN_PAT_FILE = process.env.ZITADEL_LOGIN_PAT_FILE || '/zitadel-bootstrap/login-client.pat';
const CLIENT_ID_FILE = process.env.ZITADEL_CLIENT_ID_FILE || '/zitadel-bootstrap/client-id.txt';
const APP_ORIGIN = (process.env.PUBLIC_BABASKETET_APP_URL || 'http://localhost:8081').replace(/\/$/, '');

const apiParsed = new URL(ZITADEL_API_URL);
const publicHost = new URL(ZITADEL_PUBLIC_URL).host;

function zitadel(path, token, method = 'GET', body = undefined) {
  return new Promise((resolve, reject) => {
    const payload = body ? JSON.stringify(body) : undefined;
    const lib = apiParsed.protocol === 'https:' ? https : http;
    const req = lib.request(
      {
        hostname: apiParsed.hostname,
        port: apiParsed.port || (apiParsed.protocol === 'https:' ? 443 : 80),
        path,
        method,
        headers: {
          Host: publicHost,
          'Content-Type': 'application/json',
          Authorization: `Bearer ${token}`,
          ...(payload ? { 'Content-Length': Buffer.byteLength(payload) } : {})
        }
      },
      (res) => {
        let raw = '';
        res.on('data', (chunk) => (raw += chunk));
        res.on('end', () => {
          const data = raw ? JSON.parse(raw) : {};
          if (res.statusCode && res.statusCode >= 200 && res.statusCode < 300) {
            resolve(data);
          } else {
            reject(new Error(`${method} ${path} failed (${res.statusCode}): ${raw}`));
          }
        });
      }
    );
    req.on('error', reject);
    if (payload) req.write(payload);
    req.end();
  });
}

async function ensureLoginClientPat(token) {
  if (fs.existsSync(LOGIN_PAT_FILE) && fs.readFileSync(LOGIN_PAT_FILE, 'utf8').trim().length > 0) {
    return;
  }

  const users = await zitadel('/management/v1/users/_search', token, 'POST', {
    queries: [{ userNameQuery: { userName: 'login-client', method: 'TEXT_QUERY_METHOD_EQUALS' } }]
  });

  let userId = users.result?.[0]?.id;
  if (!userId) {
    const created = await zitadel('/management/v1/users/machine', token, 'POST', {
      userName: 'login-client',
      name: 'WillIAM Login Client',
      description: 'Machine user for WillIAM.UI Login V2'
    });
    userId = created.userId;
  }

  try {
    await zitadel('/admin/v1/members', token, 'POST', {
      userId,
      roles: ['IAM_LOGIN_CLIENT']
    });
  } catch (err) {
    if (!String(err.message).includes('AlreadyExists')) {
      console.warn('Note granting IAM_LOGIN_CLIENT:', err.message);
    }
  }

  const patRes = await zitadel(`/management/v1/users/${userId}/pats`, token, 'POST', {
    expirationDate: '2029-01-01T00:00:00Z'
  });
  fs.writeFileSync(LOGIN_PAT_FILE, `${patRes.token}\n`, 'utf8');
}

async function main() {
  for (let i = 0; i < 30; i++) {
    if (fs.existsSync(ADMIN_TOKEN_FILE)) break;
    await new Promise((r) => setTimeout(r, 2000));
  }
  if (!fs.existsSync(ADMIN_TOKEN_FILE)) {
    throw new Error(`Admin PAT not found at ${ADMIN_TOKEN_FILE}`);
  }

  const token = fs.readFileSync(ADMIN_TOKEN_FILE, 'utf8').trim();

  await ensureLoginClientPat(token);

  // 1. Buscar o crear proyecto "Babasketet"
  const projects = await zitadel('/management/v1/projects/_search', token, 'POST', {
    queries: [{ nameQuery: { name: 'Babasketet', method: 'TEXT_QUERY_METHOD_EQUALS' } }]
  });

  let projectId = projects.result?.[0]?.id;
  if (!projectId) {
    const createdProj = await zitadel('/management/v1/projects', token, 'POST', {
      name: 'Babasketet',
      projectRoleAssertion: true
    });
    projectId = createdProj.id;
  }

  // 2. Buscar o crear app OIDC PKCE "babasketet-web"
  const apps = await zitadel(`/management/v1/projects/${projectId}/apps/_search`, token, 'POST', {
    queries: [{ nameQuery: { name: 'babasketet-web', method: 'TEXT_QUERY_METHOD_EQUALS' } }]
  });

  let clientId = apps.result?.[0]?.oidcConfig?.clientId;
  if (!clientId) {
    const redirectUris = Array.from(
      new Set([
        `${APP_ORIGIN}/auth/callback`,
        'http://localhost:8081/auth/callback',
        'http://localhost/auth/callback',
        'http://web:80/auth/callback'
      ])
    );
    const postLogoutUris = Array.from(
      new Set([
        `${APP_ORIGIN}/`,
        'http://localhost:8081/',
        'http://localhost/',
        'http://web:80/'
      ])
    );

    const createdApp = await zitadel(`/management/v1/projects/${projectId}/apps/oidc`, token, 'POST', {
      name: 'babasketet-web',
      redirectUris,
      postLogoutRedirectUris: postLogoutUris,
      responseTypes: ['OIDC_RESPONSE_TYPE_CODE'],
      grantTypes: ['OIDC_GRANT_TYPE_AUTHORIZATION_CODE', 'OIDC_GRANT_TYPE_REFRESH_TOKEN'],
      appType: 'OIDC_APP_TYPE_USER_AGENT',
      authMethodType: 'OIDC_AUTH_METHOD_TYPE_NONE',
      accessTokenType: 'OIDC_TOKEN_TYPE_JWT',
      idTokenRoleAssertion: true,
      idTokenUserinfoAssertion: true,
      devMode: true
    });
    clientId = createdApp.clientId;
  }

  fs.writeFileSync(CLIENT_ID_FILE, `${clientId}\n`, 'utf8');
  console.log(`Zitadel bootstrap OK. Project=${projectId}, ClientId=${clientId}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
