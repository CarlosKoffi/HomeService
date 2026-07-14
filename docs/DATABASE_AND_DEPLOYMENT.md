# Base de donnees et deploiement

La base cible est PostgreSQL, deployee via Coolify.

## Source de verite

La source principale du schema est EF Core dans `HomeService.Infrastructure`.

Au demarrage, l'API execute `DatabaseInitializer.InitializeAsync`, applique les migrations EF Core avec `MigrateAsync`, puis seed les donnees minimales. En deploiement Coolify normal, il n'y a donc pas de script SQL a lancer a la main.

Chaque evolution doit produire:

- une migration EF dans `src/HomeService.Infrastructure/Data/Migrations`;
- un script SQL de reference dans `src/HomeService.Admin/Sql`;
- une note si la migration modifie des donnees existantes.

## Scripts SQL

Les scripts SQL sont gardes dans le projet admin pour inspection, audit DBA et exploitation controlee:

`src/HomeService.Admin/Sql`

Ils ne sont pas la voie d'execution principale en production. La production passe par les migrations EF appliquees par l'API au demarrage. Les scripts SQL servent de reference lisible, ou de secours si une operation exceptionnelle doit etre faite hors application.

Convention:

- numerotation stable;
- nom explicite;
- scripts idempotents quand c'est possible;
- aucune suppression massive sans script de sauvegarde ou validation explicite.

Scripts presents a la date du 2026-07-14:

- `001_create_homeservice_schema.sql`
- `002_seed_admin_modules_roles.sql`
- `003_seed_initial_translations.sql`
- `004_reset_company_application_tests.sql`
- `005_add_notification_outbox.sql`
- `006_add_company_portal_employee_workspace.sql`
- `007_add_country_branding.sql`
- `008_add_company_assignment_mode.sql`
- `009_add_provider_gender.sql`
- `010_add_provider_mobile_workspace.sql`
- `011_move_pricing_to_services.sql`
- `012_add_provider_service_price_tier.sql`
- `013_add_audit_log_entries.sql`
- `014_add_service_icons.sql`
- `015_add_mission_confirmation_contact_release.sql`
- `016_add_provider_interim_affiliation_workflow.sql`
- `017_add_provider_portal_password.sql`

Le manifeste detaille est dans `src/HomeService.Admin/Sql/README.md`.

## Donnees sensibles

Ne jamais commiter:

- mots de passe;
- chaines de connexion reelles;
- secrets SMTP/SMS/WhatsApp;
- tokens;
- documents uploades;
- fichiers generes en local.

## Stockage fichiers

Les documents uploades doivent etre stockes hors image Docker, via volume persistant ou stockage objet.

Chemin runtime actuel recommande:

`/app/storage`

Sur Coolify, l'API doit avoir un volume persistant dedie pour les pieces et fichiers serveur. Les frontaux ne doivent pas stocker durablement les documents.

## Deploiement Coolify

Chaque app deployable a son Dockerfile:

- `src/HomeService.Api/Dockerfile`
- `src/HomeService.Company/Dockerfile`
- `src/HomeService.Admin/Dockerfile`
- `src/HomeService.Client/Dockerfile`
- `src/HomeService.Provider/Dockerfile`

Variables minimales par app:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ASPNETCORE_URLS=http://+:8080`
- `ApiBaseUrl` pour les frontaux
- `ConnectionStrings__DefaultConnection` pour l'API, ou une URL PostgreSQL via `DATABASE_URL` / `POSTGRES_URL`
- variables d'auth temporaire si activees
- variables de stockage et notification quand branchees

Formats acceptes pour la base API:

- `ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=homeservice;Username=homeservice;Password=...`
- `DATABASE_URL=postgres://homeservice:motdepasse@postgres:5432/homeservice`
- `POSTGRES_URL=postgres://homeservice:motdepasse@postgres:5432/homeservice`

Si une URL PostgreSQL est fournie et que la configuration par defaut pointe encore vers `localhost`, l'API utilise automatiquement l'URL Coolify.

## Regle de livraison

Avant push/deploiement:

1. verifier `git status`;
2. lancer les tests pertinents;
3. confirmer les migrations/scripts SQL;
4. verifier qu'aucun secret n'est present;
5. commiter par lot clair.
