# Deploiement Coolify

Ce dossier contient la base Docker Compose pour lancer la premiere phase du projet:

- API centrale
- portail entreprise
- back-office admin entreprise
- PostgreSQL

Chaque application deployable possede son propre `Dockerfile`:

- `src/HomeService.Api/Dockerfile`
- `src/HomeService.Company/Dockerfile`
- `src/HomeService.Admin/Dockerfile`
- `src/HomeService.Client/Dockerfile`
- `src/HomeService.Provider/Dockerfile`

Le compose Coolify utilise pour l'instant `Api`, `Company` et `Admin`. Les projets `Client` et `Provider`
ont deja leur Dockerfile pour la suite, mais ils ne sont pas encore exposes dans cette premiere phase.

## Variables a creer dans Coolify

Copier les valeurs de `deploy/.env.example` dans les variables d'environnement Coolify, puis remplacer au minimum:

- `POSTGRES_PASSWORD`
- `POSTGRES_USER` si besoin
- `POSTGRES_DB` si besoin
- `API_PORT`, `COMPANY_PORT`, `ADMIN_PORT` selon les ports exposes par Coolify

## Option recommandee: creer les apps une par une

Dans Coolify, tu peux creer chaque application separement avec son propre Dockerfile:

### API

- Type: Dockerfile
- Repository: `CarlosKoffi/HomeService`
- Branch: `main`
- Base directory: racine du repo
- Dockerfile: `src/HomeService.Api/Dockerfile`
- Port interne: `8080`

Variables:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://+:8080`
- `ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=homeservice;Username=homeservice;Password=...`
- Alternative acceptee: `DATABASE_URL=postgres://homeservice:motdepasse@postgres:5432/homeservice`
- L'API applique les migrations EF automatiquement au demarrage.
- Volume persistant recommande: destination `/app/storage`

### Portail entreprise

- Type: Dockerfile
- Dockerfile: `src/HomeService.Company/Dockerfile`
- Port interne: `8080`

Variables:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://+:8080`
- `ApiBaseUrl=https://api.votre-domaine.com`

### Admin

- Type: Dockerfile
- Dockerfile: `src/HomeService.Admin/Dockerfile`
- Port interne: `8080`

Variables:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://+:8080`
- `ApiBaseUrl=https://api.votre-domaine.com`

### Client et prestataire

Les Dockerfiles existent deja pour la suite:

- `src/HomeService.Client/Dockerfile`
- `src/HomeService.Provider/Dockerfile`

On ne les expose pas encore tant que le scope client/prestataire n'est pas avance.

## Option alternative: Docker Compose

Le fichier `deploy/docker-compose.yml` permet de lancer la stack en une seule ressource.

## Services exposes

- `api` ecoute en interne sur `8080`
- `company` ecoute en interne sur `8080`
- `admin` ecoute en interne sur `8080`
- `postgres` reste interne au compose, sauf si le port est explicitement expose

Dans Coolify, l'ideal est d'attacher un domaine ou sous-domaine par interface:

- `api.votre-domaine.com` vers le service `api`
- `entreprise.votre-domaine.com` vers le service `company`
- `admin.votre-domaine.com` vers le service `admin`

## Points restants avant production publique

- Ajouter une authentification reelle pour l'admin.
- Finaliser le durcissement de l'authentification admin.
- Decider le stockage des documents uploades: volume Docker, S3 compatible ou MinIO.
- Creer un premier compte super admin.
- Ajouter les secrets email/SMS quand les integrations seront choisies.

## SQL

Les scripts SQL de reference sont conserves dans:

`src/HomeService.Admin/Sql`

Ils servent a inspecter et auditer la structure de base depuis le projet admin. En production standard,
les migrations EF sont appliquees automatiquement par l'API au demarrage.
