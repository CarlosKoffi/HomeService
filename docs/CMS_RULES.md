# Regles CMS Kaza

Le CMS Kaza est un CMS headless multi-site. Il sert a administrer les contenus publics et certaines zones editoriales des portails, sans remplacer les donnees metier.

## Principes

- Le rendu visuel appartient aux frontaux.
- Le CMS fournit sites, pages, menus, sections, valeurs, medias, traductions et statuts.
- Les missions, prestataires, paiements, permissions et documents metier restent hors CMS.
- Les contenus doivent etre versionnes avant publication.
- Les textes traduisibles ne doivent pas etre dupliques dans des colonnes fixes par langue.
- Les composants sont identifies par des cles stables et des versions de schema.
- Le JSON est autorise seulement pour une configuration bornee et versionnee.
- Les medias sont stockes hors base; la base conserve les metadonnees et chemins de stockage.

## Surfaces prevues

- Site public client.
- Site public entreprises partenaires.
- Site public prestataires.
- Portail entreprise authentifie, pour aides, annonces, FAQ et onboarding.
- Application client.
- Application prestataire.
- Back-office administrateur.

## Frontiere metier

Le CMS peut piloter:

- pages publiques;
- menus;
- sections editoriales;
- FAQ;
- aides contextuelles;
- messages de maintenance;
- medias marketing;
- textes de conversion et SEO.

Le CMS ne doit pas piloter directement:

- attribution de mission;
- paiement;
- commission;
- validation de document;
- suspension utilisateur;
- permission admin;
- donnees confidentielles.

## Workflow cible

Les contenus suivent un cycle simple:

1. Brouillon.
2. Revision.
3. Approbation.
4. Publication immediate ou planifiee.
5. Depublication ou archivage.

Une version publiee ne doit pas etre modifiee silencieusement. Toute modification prepare une nouvelle version.
