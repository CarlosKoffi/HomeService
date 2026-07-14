# Base de donnees et deploiement

La base cible est PostgreSQL, deployee via Coolify.

## Source de verite

La source principale du schema est EF Core dans `HomeService.Infrastructure`.

Chaque evolution doit produire:

- une migration EF dans `src/HomeService.Infrastructure/Data/Migrations`;
- un script SQL de reference dans `src/HomeService.Admin/Sql`;
- une note si la migration modifie des donnees existantes.

## Scripts SQL

Les scripts SQL sont gardes dans le projet admin pour inspection et exploitation:

`src/HomeService.Admin/Sql`

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
- `006_add_country_branding.sql`
- `007_add_company_assignment_mode.sql`
- `008_add_provider_gender.sql`
- `008_add_provider_mobile_workspace.sql`
- `009_move_pricing_to_services.sql`
- `010_add_provider_service_price_tier.sql`
- `011_add_audit_log_entries.sql`
- `012_add_service_icons.sql`
- `013_add_mission_confirmation_contact_release.sql`
- `014_add_provider_interim_affiliation_workflow.sql`
- `015_add_provider_portal_password.sql`

Attention: il existe deux numeros `006` et deux numeros `008`. Avant production longue, il faudra renumeroter ou documenter cette sequence pour eviter toute confusion humaine.

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
- `ConnectionStrings__DefaultConnection` pour l'API
- variables d'auth temporaire si activees
- variables de stockage et notification quand branchees

## Regle de livraison

Avant push/deploiement:

1. verifier `git status`;
2. lancer les tests pertinents;
3. confirmer les migrations/scripts SQL;
4. verifier qu'aucun secret n'est present;
5. commiter par lot clair.

