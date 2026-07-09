# Deploiement Coolify

Ce dossier contient la base Docker Compose pour lancer la premiere phase du projet:

- API centrale
- portail entreprise
- back-office admin entreprise
- PostgreSQL

## Variables a creer dans Coolify

Copier les valeurs de `deploy/.env.example` dans les variables d'environnement Coolify, puis remplacer au minimum:

- `POSTGRES_PASSWORD`
- `POSTGRES_USER` si besoin
- `POSTGRES_DB` si besoin
- `API_PORT`, `COMPANY_PORT`, `ADMIN_PORT` selon les ports exposes par Coolify

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
- Ajouter une authentification entreprise avec lien d'activation.
- Brancher les formulaires sur l'API.
- Decider le stockage des documents uploades: volume Docker, S3 compatible ou MinIO.
- Appliquer les migrations EF Core au deploiement.
- Creer un premier compte super admin.
- Ajouter les secrets email/SMS quand les integrations seront choisies.
