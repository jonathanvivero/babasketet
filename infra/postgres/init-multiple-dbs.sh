#!/usr/bin/env bash
set -euo pipefail

# Crea roles y bases de datos estrictamente aisladas para Babasketet y Zitadel.
# Ningun servicio comparte credenciales ni acceso a la base de datos del otro.

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '${BABASKETET_DB_USER}') THEN
            CREATE ROLE ${BABASKETET_DB_USER} WITH LOGIN PASSWORD '${BABASKETET_DB_PASSWORD}';
        END IF;
        IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '${ZITADEL_DB_USER}') THEN
            CREATE ROLE ${ZITADEL_DB_USER} WITH LOGIN PASSWORD '${ZITADEL_DB_PASSWORD}';
        END IF;
    END
    \$\$;

    SELECT 'CREATE DATABASE ${BABASKETET_DB_NAME} OWNER ${BABASKETET_DB_USER}'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '${BABASKETET_DB_NAME}')\gexec

    SELECT 'CREATE DATABASE ${ZITADEL_DB_NAME} OWNER ${ZITADEL_DB_USER}'
    WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '${ZITADEL_DB_NAME}')\gexec

    REVOKE ALL ON DATABASE ${BABASKETET_DB_NAME} FROM PUBLIC;
    REVOKE ALL ON DATABASE ${ZITADEL_DB_NAME} FROM PUBLIC;
    GRANT ALL PRIVILEGES ON DATABASE ${BABASKETET_DB_NAME} TO ${BABASKETET_DB_USER};
    GRANT ALL PRIVILEGES ON DATABASE ${ZITADEL_DB_NAME} TO ${ZITADEL_DB_USER};
EOSQL
