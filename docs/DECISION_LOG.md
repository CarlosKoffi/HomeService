# Journal de decisions

Ce fichier garde les decisions importantes pour eviter de les redebattre ou de les perdre dans le chat.

## 2026-07-14 - Nettoyage des artefacts generes

Decision: retirer `bin` et `obj` du suivi Git.

Raison:

- ce sont des fichiers generes localement;
- ils gonflent le repo;
- ils creent du bruit dans les diffs;
- `.gitignore` les ignore deja.

Commit: `b826013e Clean tracked build artifacts`

## 2026-07-14 - Documentation de gouvernance

Decision: ajouter une base documentaire courte avant de continuer les gros travaux.

Raison:

- le projet grossit vite;
- les flux entreprise, prestataire, admin, SQL, Coolify et tests doivent rester maitrises;
- les prochains lots doivent etre petits, testables et deployables.

## Decisions deja posees dans le produit

- Nom courant: Kaza.
- Pays pilote: Cote d'Ivoire.
- Extension prevue: Afrique de l'Ouest, multi-pays et multilingue.
- Stack: .NET, Blazor, API centrale, PostgreSQL, Coolify, Docker.
- Entreprises: onboarding, validation, portail, prestataires, missions, encaissements.
- Prestataires: rattachement entreprise, interim, code d'activation, mission mobile, verification position.
- Admin: roles, modules, permissions, validations, textes, pays, audit, notifications.

## 2026-07-14 - Sequence SQL unique

Decision: renommer les scripts SQL de reference pour supprimer les doublons de prefixe `006` et `008`.

Raison:

- l'ordre d'application doit etre lisible pour un humain;
- Coolify/production ne doivent pas dependre d'une interpretation ambigue;
- les scripts restent des references controlees, EF Core reste la source principale du schema.

## 2026-07-15 - Fondation CMS multi-site

Decision: ajouter un premier noyau CMS relationnel multi-site avant de brancher les ecrans admin et les futurs Figma.

Raisons:

- eviter de continuer a ajouter du texte durable en dur;
- permettre plusieurs sites et portails sans dupliquer les modeles;
- garder des contenus versionnes, traduisibles et auditables;
- preparer un mapping Figma propre sans construire un page builder libre.

Portee:

- sites, pages, traductions, versions, sections, composants, valeurs typees, menus et medias;
- migration EF `AddCmsFoundation`;
- script SQL de reference `018_add_cms_foundation.sql`.

Hors portee de ce lot:

- ecrans admin CMS;
- endpoints publics CMS;
- workflow complet de publication;
- permissions CMS detaillees.

## 2026-08-04 - Durcissement des mots de passe et de l'authentification

Decision: remplacer les nouveaux hashes de mots de passe SHA-256 par PBKDF2-SHA256 a 210 000 iterations.

Compatibilite:

- les anciens hashes SHA-256 sales restent acceptes temporairement;
- ils sont remplaces automatiquement par PBKDF2 apres une connexion reussie;
- aucun mot de passe en clair et aucun jeton de session en clair ne sont stockes en base;
- les routes publiques de connexion, inscription et activation sont limitees a 10 tentatives par minute et par adresse IP.
