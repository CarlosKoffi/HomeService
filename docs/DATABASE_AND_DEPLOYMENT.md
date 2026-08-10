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
- `018_add_cms_foundation.sql`

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

## Stockage des medias avec Cloudflare R2

En developpement et dans les tests, l'API conserve le stockage local par defaut. En production, activer R2 uniquement apres avoir cree les deux buckets et ajoute les secrets dans la plateforme d'hebergement:

- `STORAGE_PROVIDER=R2`
- `R2_ACCOUNT_ID=<identifiant du compte Cloudflare>`
- `R2_ACCESS_KEY_ID=<access key du jeton R2>`
- `R2_SECRET_ACCESS_KEY=<secret du jeton R2>`
- `R2_PUBLIC_BUCKET=wele-public-media-prod`
- `R2_PRIVATE_BUCKET=wele-private-media-prod`
- `R2_PUBLIC_BASE_URL=https://media.wele.africa` lorsque le domaine sera actif
- `R2_PUBLIC_ASSET_VERSION=20260807-optimized-1` pour invalider proprement les caches lors d'une nouvelle version des images integrees
- `R2_PUBLIC_DIRECT_DELIVERY_ENABLED=false` pendant la migration, puis `true` lorsque tous les medias CMS historiques sont presents dans R2
- `R2_SEED_PUBLIC_ASSETS_ON_STARTUP=true` pour copier automatiquement les images de services, prestations et moyens de paiement absentes de R2
- `R2_SYNC_PUBLIC_ASSETS_ON_STARTUP=true` pour resynchroniser les images integrees a chaque deploiement et propager leurs optimisations
- `R2_MIGRATE_LOCAL_ASSETS_ON_STARTUP=true` pour copier automatiquement les anciens medias du volume local vers les buckets public et prive

Ne jamais placer les trois valeurs secretes dans `appsettings.json`, un fichier `.env` commite ou un APK. L'API refuse de demarrer avec `STORAGE_PROVIDER=R2` si une valeur obligatoire manque.

Le bucket public est reserve aux medias CMS diffusables par CDN. Les photos de mission, pieces d'identite, diplomes, candidatures et photos de profil client restent dans le bucket prive et sont servis uniquement par les routes authentifiees de l'API.

Lors du passage a R2, les nouvelles ecritures partent dans R2. Pour garantir une migration sans coupure, une lecture absente de R2 recherche encore le fichier dans l'ancien volume local. Ce volume doit donc rester monte jusqu'a la fin de la migration des objets historiques.

La diffusion directe par `media.wele.africa` reste volontairement desactivee pendant cette phase. Tant que `R2_PUBLIC_DIRECT_DELIVERY_ENABLED=false`, l'API conserve les routes historiques et peut donc retomber sur le volume local. Activer la redirection CDN seulement apres la verification de la migration du bucket public.

Lorsque R2 est active, un traitement en arriere-plan inventorie les repertoires `wwwroot/assets/services`, `wwwroot/catalog/prestations` et `wwwroot/media/payment-providers`. Il envoie uniquement les fichiers absents du bucket public. Le traitement est idempotent et peut donc etre relance apres un redemarrage sans dupliquer les objets.

Un second traitement migre les anciens medias du volume persistant. Seul `storage/cms` rejoint le bucket public. Les repertoires `storage/client-profiles`, `storage/client-missions`, `storage/providers`, `storage/documents/company-applications` et `storage/documents/providers` rejoignent exclusivement le bucket prive. La migration est idempotente et ne supprime jamais les fichiers locaux; le volume peut etre conserve jusqu'a verification complete des objets R2.

## Portefeuille et reversements entreprise

La migration `AddCompanyWalletAndPayouts` cree un portefeuille comptable par entreprise, un journal idempotent des mouvements, importe les reversements historiques deja liberes et separe quatre soldes: en attente, disponible, reserve et retire.

Le calendrier est strict:

- `Fortnightly`: missions liberees du 1er au 14 disponibles le 15; missions liberees du 15 a la fin du mois disponibles le 1er du mois suivant;
- `Monthly`: missions liberees pendant le mois disponibles le 1er du mois suivant;
- aucun endpoint ne permet de rendre une somme disponible avant sa date d'eligibilite;
- la demande de reversement reserve le montant afin d'interdire un double paiement.
- les routes portefeuille exigent un jeton de session entreprise valide et refusent l'acces au portefeuille d'une autre entreprise.

Variables API obligatoires avant l'ajout du premier beneficiaire:

- `PAYOUT_DATA_PROTECTION_KEY=<32 octets aleatoires encodes en Base64>` (par exemple genere avec `openssl rand -base64 32`);
- cette cle doit etre differente entre staging et production, sauvegardee dans le coffre de secrets et ne jamais etre changee sans procedure de rotation.

Configuration Jeko a valider en sandbox avant activation:

- `JEKO_PAYOUTS_ENABLED=false` tant que les tests sandbox ne sont pas signes;
- `JEKO_API_BASE_URL=<URL officielle de l'environnement Jeko>`;
- `JEKO_API_KEY=<cle API serveur>`;
- `JEKO_API_KEY_HEADER=x-api-key` ou la valeur confirmee par Jeko;
- `JEKO_STORE_ID=<identifiant du store>`;
- `JEKO_TRANSFER_PATH=<route de creation de transfert confirmee par Jeko>`;
- `JEKO_TRANSFER_STATUS_PATH=<route de statut avec le marqueur {id}>`;
- `JEKO_WEBHOOK_SECRET=<secret HMAC dedie>`;
- `JEKO_WEBHOOK_SIGNATURE_HEADER=Jeko-Signature` ou le nom d'en-tete confirme par Jeko.

Le webhook de reversement a declarer chez Jeko est `https://api.wele.africa/api/webhooks/jeko/payouts`. Il exige la signature HMAC SHA-256 dans `Jeko-Signature`. Un succes de transfert deplace le montant reserve vers le total retire; un echec definitif restitue automatiquement le montant au solde disponible. Les appels utilisent la reference Wele comme cle d'idempotence.

Les frais actuels sont calcules avant confirmation: 1,5 % pour Mobile Money, 1 000 XOF pour le virement bancaire et 0 XOF pour le retrait cash. Le retrait cash exige une validation admin et une reference de preuve.

## Regle de livraison

Avant push/deploiement:

1. verifier `git status`;
2. lancer les tests pertinents;
3. confirmer les migrations/scripts SQL;
4. verifier qu'aucun secret n'est present;
5. commiter par lot clair.
