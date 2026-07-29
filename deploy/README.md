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

Notifications mobile Firebase:

- `FIREBASE_NOTIFICATIONS_ENABLED=true`
- `FIREBASE_PROJECT_ID=homeservice-18c0c`
- `FIREBASE_CREDENTIALS_BASE64=...`
- Optionnel: `FIREBASE_NOTIFICATIONS_INTERVAL_SECONDS=30`
- Optionnel: `FIREBASE_NOTIFICATIONS_BATCH_SIZE=50`

Important Coolify: les variables Firebase doivent etre cochees **Available at Runtime** uniquement.
Ne pas cocher **Available at Buildtime** pour `FIREBASE_CREDENTIALS_JSON` ou
`FIREBASE_CREDENTIALS_BASE64`, sinon Docker les injecte comme `ARG` pendant le build et une cle JSON
multiligne peut casser le deploiement.

Si `FIREBASE_CREDENTIALS_JSON` existe encore dans Coolify, la supprimer ou la laisser vide. Utiliser uniquement
`FIREBASE_CREDENTIALS_BASE64` en runtime.

Pour generer la valeur base64 depuis Windows PowerShell:

```powershell
$json = Get-Content "C:\Users\bruce\Downloads\firebase-key.json" -Raw
[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
```

Smoke tests post-deploiement GitHub:

- `SMOKE_API_BASE_URL=https://...` ou `http://...`
- `SMOKE_SITE_AUTH_USERNAME=...` et `SMOKE_SITE_AUTH_PASSWORD=...` si le site est protege par Basic Auth.
- `SMOKE_ADMIN_EMAIL=...` et `SMOKE_ADMIN_PASSWORD=...` pour tester aussi les routes admin protegees.

Sans `SMOKE_ADMIN_EMAIL` / `SMOKE_ADMIN_PASSWORD`, le smoke test verifie les routes publiques et saute les routes admin
au lieu de produire un faux echec 401.

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

## Verification automatique apres deploiement

La CI compile la solution, lance les tests unitaires puis les tests d'integration workflow. Les workflows critiques
mockent le paiement et les vrais envois de notifications, mais verifient les etats mission, paiements, outbox et
notifications portail.

Pour verifier l'API deployee apres Coolify, renseigner dans GitHub:

- Secret `SMOKE_API_BASE_URL`: URL publique de l'API, par exemple `https://api.votre-domaine.com`
- Secret optionnel `SMOKE_SITE_AUTH_USERNAME`
- Secret optionnel `SMOKE_SITE_AUTH_PASSWORD`
- Variable optionnelle `SMOKE_STARTUP_DELAY_SECONDS`: attente avant verification, par defaut `120` dans la CI

Le script `deploy/smoke-test.ps1` controle notamment:

- `/health`
- catalogue services
- CMS entreprise et prestataire
- demandes entreprises admin
- missions admin
- parametrage missions
- notifications, regles et modeles
- paiements
- acces et roles

Pour les notifications, le smoke test ne se contente pas d'un endpoint joignable: il verifie aussi que
les evenements essentiels du workflow sont bien presents apres migration et seeding, ainsi que les
modeles attendus par canal (`Portal`, `MobilePush`, `Email`, `WhatsApp`) pour les cas critiques:

- mission affectee au prestataire
- devis complementaire demande/disponible/paye
- litige resolu
- remboursement valide

Il echoue volontairement sur les 404, 500 ou 502 afin de detecter rapidement les regressions de deploiement.
- Ajouter les secrets email/SMS quand les integrations seront choisies.

## SQL

Les scripts SQL de reference sont conserves dans:

`src/HomeService.Admin/Sql`

Ils servent a inspecter et auditer la structure de base depuis le projet admin. En production standard,
les migrations EF sont appliquees automatiquement par l'API au demarrage.
