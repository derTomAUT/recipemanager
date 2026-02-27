# Production Deployment (VPS + Docker Compose)

This setup runs:
- `traefik` (TLS terminator + reverse proxy on `${APP_PORT}`, default `8081`)
- `frontend` (Angular static build served by nginx, internal only)
- `backend` (.NET API)
- `postgres` (database)

Persistent Docker volumes:
- `recipemanager_postgres_data` for database data
- `recipemanager_uploads` for uploaded recipe images/files
- `recipemanager_logs` for backend logs
- `recipemanager_letsencrypt` for ACME cert state

## 1) Required GitHub Secrets

Deployment SSH:
- `VPS_HOST`
- `VPS_PORT`
- `VPS_USER`
- `VPS_SSH_KEY`
- `VPS_REPO_URL` (repo clone URL accessible from VPS)
- `VPS_APP_DIR` (optional, e.g. `/opt/recipemanager`)

App configuration:
- `APP_PORT` (public TLS port, default `8081`)
- `PUBLIC_HOST` (e.g. `recipe.weit-weg.at`)
- `PUBLIC_ORIGIN` (e.g. `https://recipe.weit-weg.at:8081`)
- `ACME_EMAIL` (email for ACME registration)
- `ACME_DNS_API_BASE` (acme-dns API base URL, e.g. `https://auth.acme-dns.io`)
- `ACME_DNS_STORAGE_JSON` (JSON content for acme-dns account mapping)
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `JWT_SECRET` (32+ chars)
- `JWT_ISSUER`
- `JWT_AUDIENCE`
- `JWT_EXPIRY_MINUTES`
- `GOOGLE_CLIENT_ID`

## 2) First-Time VPS Setup

Install Docker + Compose plugin, then ensure the deploy user can run Docker:

```bash
sudo usermod -aG docker <your-user>
```

Log out/in once after changing group membership.

## 3) DNS Challenge Setup (DomainFactory + acme-dns)

1. Register an acme-dns account for `recipe.weit-weg.at` (one-time) and obtain the credentials JSON.
   - Example shape: `infra/acmedns.json.example`
2. Add GitHub secrets:
   - `ACME_DNS_API_BASE`
   - `ACME_DNS_STORAGE_JSON` (exact JSON string from step 1)
3. In DomainFactory DNS, add a CNAME:
   - `_acme-challenge.recipe.weit-weg.at` -> `<fulldomain from acme-dns>`
4. Ensure public A/AAAA for `recipe.weit-weg.at` points to your VPS.

## 4) Manual Deploy (optional)

```bash
cp infra/.env.example infra/.env
# edit infra/.env with production values
cat > infra/acmedns.json <<EOF
<ACME_DNS_STORAGE_JSON content>
EOF
docker compose -f infra/docker-compose.prod.yml --env-file infra/.env up -d --build
```

## 5) Automated Deploy

Push to `master`, GitHub Actions workflow `.github/workflows/deploy-vps.yml` will deploy to VPS.
