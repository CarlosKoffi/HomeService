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
