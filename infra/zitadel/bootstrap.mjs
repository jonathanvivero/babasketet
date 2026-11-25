import fs from 'node:fs';
import http from 'node:http';
import https from 'node:https';

const ZITADEL_API_URL = (process.env.ZITADEL_API_URL || 'http://zitadel:8080').replace(/\/$/, '');
const ZITADEL_PUBLIC_URL = (process.env.ZITADEL_PUBLIC_URL || 'http://localhost:8085').replace(/\/$/, '');
const ADMIN_TOKEN_FILE = process.env.ZITADEL_ADMIN_TOKEN_FILE || '/zitadel-bootstrap/admin.pat';
const LOGIN_PAT_FILE = process.env.ZITADEL_LOGIN_PAT_FILE || '/zitadel-bootstrap/login-client.pat';
const CLIENT_ID_FILE = process.env.ZITADEL_CLIENT_ID_FILE || '/zitadel-bootstrap/client-id.txt';
const PROJECT_ID_FILE = process.env.ZITADEL_PROJECT_ID_FILE || '/zitadel-bootstrap/project-id.txt';
const APP_ORIGIN = (process.env.PUBLIC_BABASKETET_APP_URL || 'http://localhost:8081').replace(/\/$/, '');
const WILLIAM_PUBLIC_URL = (process.env.WILLIAM_PUBLIC_URL || 'http://localhost:3000').replace(/\/$/, '');
const SEED_ADMIN_EMAIL = (process.env.SEED_ADMIN_EMAIL || 'jonathanvivero@gmail.com').trim().toLowerCase();
const SEED_ADMIN_PASSWORD = process.env.SEED_ADMIN_PASSWORD || 'QWERTYUIOP-789456123!';

const BABASKETET_ROLES = [
  { roleKey: 'Guest', displayName: 'Guest', group: 'global' },
  { roleKey: 'User', displayName: 'User', group: 'global' },
  { roleKey: 'Admin', displayName: 'Admin', group: 'global' },
  { roleKey: 'Owner', displayName: 'Owner', group: 'house' }
];

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

  // 0a. Habilitar Login V2 -> WillIAM.UI a nivel de instancia Zitadel
  try {
    await zitadel('/v2/features/instance', token, 'PUT', {
      loginV2: {
        required: true,
        baseUri: `${WILLIAM_PUBLIC_URL}/ui/v2/login`
      }
    });
  } catch (err) {
    const msg = String(err.message);
    if (!msg.includes('No changes') && !msg.includes('not been changed')) {
      throw err;
    }
  }

  // 0b. Política de complejidad de contraseñas (permite contraseñas seguras sin minúsculas obligatorias como QWERTYUIOP-789456123!)
  try {
    await zitadel('/admin/v1/policies/password/complexity', token, 'PUT', {
      minLength: 8,
      hasUppercase: true,
      hasLowercase: false,
      hasNumber: true,
      hasSymbol: true
    });
  } catch (err) {
    const msg = String(err.message);
    if (!msg.includes('No changes') && !msg.includes('not been changed')) {
      throw err;
    }
  }

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
  fs.writeFileSync(PROJECT_ID_FILE, `${projectId}\n`, 'utf8');

  // 1b. Asegurar roles del proyecto Babasketet: Guest, User, Admin, Owner
  const existingRolesRes = await zitadel(`/management/v1/projects/${projectId}/roles/_search`, token, 'POST', {});
  const existingRoleKeys = new Set((existingRolesRes.result || []).map((r) => r.key));
  for (const role of BABASKETET_ROLES) {
    if (!existingRoleKeys.has(role.roleKey)) {
      await zitadel(`/management/v1/projects/${projectId}/roles`, token, 'POST', role);
    }
  }

  // 2. Buscar o crear app OIDC PKCE "babasketet-web"
  const apps = await zitadel(`/management/v1/projects/${projectId}/apps/_search`, token, 'POST', {
    queries: [{ nameQuery: { name: 'babasketet-web', method: 'TEXT_QUERY_METHOD_EQUALS' } }]
  });

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

  let clientId = apps.result?.[0]?.oidcConfig?.clientId;
  const appId = apps.result?.[0]?.id;
  if (!clientId) {
    const createdApp = await zitadel(`/management/v1/projects/${projectId}/apps/oidc`, token, 'POST', {
      name: 'babasketet-web',
      redirectUris,
      postLogoutRedirectUris: postLogoutUris,
      responseTypes: ['OIDC_RESPONSE_TYPE_CODE'],
      grantTypes: ['OIDC_GRANT_TYPE_AUTHORIZATION_CODE', 'OIDC_GRANT_TYPE_REFRESH_TOKEN'],
      appType: 'OIDC_APP_TYPE_USER_AGENT',
      authMethodType: 'OIDC_AUTH_METHOD_TYPE_NONE',
      accessTokenType: 'OIDC_TOKEN_TYPE_JWT',
      accessTokenRoleAssertion: true,
      idTokenRoleAssertion: true,
      idTokenUserinfoAssertion: true,
      devMode: true
    });
    clientId = createdApp.clientId;
  } else if (appId) {
    try {
      await zitadel(`/management/v1/projects/${projectId}/apps/${appId}/oidc_config`, token, 'PUT', {
        redirectUris,
        postLogoutRedirectUris: postLogoutUris,
        responseTypes: ['OIDC_RESPONSE_TYPE_CODE'],
        grantTypes: ['OIDC_GRANT_TYPE_AUTHORIZATION_CODE', 'OIDC_GRANT_TYPE_REFRESH_TOKEN'],
        appType: 'OIDC_APP_TYPE_USER_AGENT',
        authMethodType: 'OIDC_AUTH_METHOD_TYPE_NONE',
        accessTokenType: 'OIDC_TOKEN_TYPE_JWT',
        accessTokenRoleAssertion: true,
        idTokenRoleAssertion: true,
        idTokenUserinfoAssertion: true,
        devMode: true
      });
    } catch (err) {
      if (!String(err.message).includes('No changes')) {
        throw err;
      }
    }
  }

  // 3. Registrar usuario Admin inicial (jonathanvivero@gmail.com) con cambio obligatorio de contraseña
  const existingUsers = await zitadel('/v2/users', token, 'POST', {
    queries: [
      {
        orQuery: {
          queries: [
            { emailQuery: { emailAddress: SEED_ADMIN_EMAIL, method: 'TEXT_QUERY_METHOD_EQUALS' } },
            { loginNameQuery: { loginName: SEED_ADMIN_EMAIL, method: 'TEXT_QUERY_METHOD_EQUALS' } }
          ]
        }
      }
    ]
  });

  let adminUserId = existingUsers.result?.[0]?.userId;
  if (!adminUserId) {
    const createdAdmin = await zitadel('/v2/users/human', token, 'POST', {
      username: SEED_ADMIN_EMAIL,
      profile: {
        givenName: 'Jonathan',
        familyName: 'Vivero',
        displayName: 'Jonathan Vivero'
      },
      email: {
        email: SEED_ADMIN_EMAIL,
        isVerified: true
      },
      password: {
        password: SEED_ADMIN_PASSWORD,
        changeRequired: true
      }
    });
    adminUserId = createdAdmin.userId;
  }

  // 3b. Asignar roles User + Admin (preservando otros roles ya otorgados como Owner)
  const existingGrants = await zitadel('/management/v1/users/grants/_search', token, 'POST', {
    queries: [
      { projectIdQuery: { projectId } },
      { userIdQuery: { userId: adminUserId } }
    ]
  });
  const currentGrant = existingGrants.result?.[0];
  if (!currentGrant) {
    await zitadel(`/management/v1/users/${adminUserId}/grants`, token, 'POST', {
      projectId,
      roleKeys: ['User', 'Admin']
    });
  } else {
    const mergedRoles = Array.from(new Set([...(currentGrant.roleKeys || []), 'User', 'Admin']));
    if (mergedRoles.length !== (currentGrant.roleKeys || []).length) {
      await zitadel(`/management/v1/users/${adminUserId}/grants/${currentGrant.id}`, token, 'PUT', {
        roleKeys: mergedRoles
      });
    }
  }

  fs.writeFileSync(CLIENT_ID_FILE, `${clientId}\n`, 'utf8');
  console.log(`Zitadel bootstrap OK. Project=${projectId}, ClientId=${clientId}, AdminUserId=${adminUserId}`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
