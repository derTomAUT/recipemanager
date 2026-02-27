# Production Deployment (VPS + Docker Compose)

This setup runs:
- `frontend` (Angular static build served by nginx)
- `backend` (.NET API)
- `postgres` (database)

Persistent Docker volumes:
- `recipemanager_postgres_data` for database data
- `recipemanager_uploads` for uploaded recipe images/files
- `recipemanager_logs` for backend logs

## 1) Required GitHub Secrets

Deployment SSH:
- `VPS_HOST`
- `VPS_PORT`
- `VPS_USER`
- `VPS_SSH_KEY`
- `VPS_REPO_URL` (repo clone URL accessible from VPS)
- `VPS_APP_DIR` (optional, e.g. `/opt/recipemanager`)

App configuration:
- `APP_PORT` (usually `80`)
  - default in this setup: `8082`
- `PUBLIC_ORIGIN` (e.g. `https://recipes.example.com`)
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

## 3) Manual Deploy (optional)

```bash
cp infra/.env.example infra/.env
# edit infra/.env with production values
docker compose -f infra/docker-compose.prod.yml --env-file infra/.env up -d --build
```

## 4) Automated Deploy

Push to `master`, GitHub Actions workflow `.github/workflows/deploy-vps.yml` will deploy to VPS.
