# HomeService

Plateforme de mise en relation pour services a domicile en Cote d'Ivoire.

La premiere base couvre:

- clients
- entreprises validees par la plateforme
- prestataires invites par entreprise puis valides par la plateforme
- services plats et extensibles
- tarification horaire par prestataire et service
- missions instantanees ou planifiees
- paiement cash ou mobile money

## Projets

- `HomeService.Api`: API centrale
- `HomeService.Client`: interface client
- `HomeService.Provider`: interface prestataire mobile-first
- `HomeService.Company`: portail entreprise
- `HomeService.Admin`: back-office plateforme
- `HomeService.Domain`: modele metier pur
- `HomeService.Application`: cas d'usage et contrats applicatifs
- `HomeService.Infrastructure`: PostgreSQL, EF Core, integrations externes
- `HomeService.Contracts`: DTO partages

## Priorite produit

Le premier focus est le portail entreprise:

- inscription et validation de l'entreprise
- collecte des pieces justificatives avant validation
- verification admin en file de travail avec filtres et actions rapides
- envoi du lien d'activation par email apres validation
- relance email si l'entreprise validee ne cree pas son acces
- invitation des employes par SMS
- configuration des services, experience et tarifs horaires
- suivi des missions directes et planifiees
- suivi des encaissements cash et Mobile Money
- back-office avec admins, roles, modules et permissions

## Docker

Chaque application deployable possede son propre `Dockerfile`. Le compose local/Coolify lance:

- PostgreSQL
- API
- portail entreprise
- back-office admin

Dockerfiles disponibles, un par projet deployable:

- `src/HomeService.Api/Dockerfile`
- `src/HomeService.Company/Dockerfile`
- `src/HomeService.Admin/Dockerfile`
- `src/HomeService.Client/Dockerfile`
- `src/HomeService.Provider/Dockerfile`

Le `Dockerfile` racine a ete retire pour eviter les confusions: Coolify doit pointer vers le
Dockerfile du projet a deployer.

Les scripts SQL de reference sont conserves dans le projet admin:

- `src/HomeService.Admin/Sql/001_create_homeservice_schema.sql`
- `src/HomeService.Admin/Sql/002_seed_admin_modules_roles.sql`
- `src/HomeService.Admin/Sql/003_seed_initial_translations.sql`

L'API applique les migrations au demarrage et seed les donnees minimales pour le staging.

## Qualite Afrique

Le projet inclut un agent de revue dans `docs/agents/ux-ui-africa-reviewer.md`.
Il sert de garde-fou avant chaque livraison pour verifier:

- affichage mobile-first
- lisibilite sur petits ecrans
- poids des pages
- sobriete reseau
- formulaires tolerants aux coupures
- compatibilite avec un usage terrain en Afrique de l'Ouest
- rendu professionnel pour les entreprises

## Multi-pays et traductions

Le projet prepare des maintenant l'ouverture a plusieurs pays d'Afrique de l'Ouest.

- `Countries`: pays actifs, devise, pays de lancement
- `Languages`: langues disponibles
- `TranslationKeys`: cles de texte par scope
- `TranslationValues`: valeurs traduites par langue et variante pays optionnelle

L'admin contient une entree `Pays & traductions` pour gerer ces contenus sans redeployer.

## Acces admin

Le back-office prevoit un controle par roles:

- `AdminUsers`: comptes administrateurs
- `AdminRoles`: roles crees par le super admin
- `AdminModules`: modules visibles dans l'admin
- `AdminRolePermissions`: actions autorisees par module
- `AdminUserRoles`: affectation des roles aux admins

Le super admin est le seul profil qui peut creer les roles et definir quel module correspond a quel role.

## Documentation projet

Les documents de pilotage sont dans `docs/`:

- `docs/PROJECT_GOVERNANCE.md`: regles de travail, priorites et definition of done
- `docs/ARCHITECTURE.md`: organisation de la solution et direction des dependances
- `docs/DATABASE_AND_DEPLOYMENT.md`: PostgreSQL, scripts SQL, Coolify et stockage fichiers
- `docs/TEST_STRATEGY.md`: strategie de tests unitaires, integration et front
- `docs/DECISION_LOG.md`: decisions importantes prises pendant le projet
