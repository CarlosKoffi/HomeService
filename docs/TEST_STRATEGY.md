# Strategie de tests

Le projet doit rester testable par couches. L'objectif est qu'un changement metier soit couvert par des tests unitaires et, si besoin, par un test d'integration ou fonctionnel.

## Etat actuel

Au dernier controle:

- `HomeService.Tests.Unit`: 170 tests passes.
- `HomeService.Tests.Integration`: 3 tests passes.
- total: 173 tests passes.

Commande:

`dotnet test HomeService.sln`

## Cibles de couverture

Priorite haute:

- validation entreprise;
- lien d'activation;
- creation compte entreprise;
- gestion documents;
- creation et mise a jour prestataire;
- code d'invitation prestataire;
- onboarding prestataire independant/interim;
- demandes d'affiliation entreprise;
- affectation mission;
- acceptation/refus prestataire;
- verification position/arrivee;
- paiement, commission, annulation;
- audit logs.

## Tests unitaires

A privilegier pour:

- regles metier;
- validations;
- transitions de statut;
- calculs de commission;
- decisions d'affectation;
- generation de codes/liens;
- mapping DTO si non trivial.

Une methode metier doit faire une chose claire et etre testable sans lancer l'API.

## Tests d'integration

A utiliser pour:

- endpoints critiques;
- persistance EF/PostgreSQL ou provider test;
- migrations sensibles;
- workflow complet court.

Exemples:

- inscription entreprise -> validation admin -> lien activation -> creation compte;
- prestataire cree par entreprise -> code -> activation -> connexion;
- demande interim -> approbation entreprise -> prestataire eligible.

## Tests frontaux

Pour les ecrans critiques, verifier:

- mobile;
- desktop;
- etat vide;
- erreur API;
- donnees longues;
- boutons/actions principales.

Le visuel n'a pas besoin d'etre teste partout au pixel, mais les parcours doivent etre utilisables.

## Regle avant commit

- Petit lot backend: tests unitaires du projet touche minimum.
- Lot API/metier: `dotnet test HomeService.sln`.
- Lot front pur: build du projet touche + verification navigateur si possible.
- Lot SQL: verification migration/script + test d'integration si impact fonctionnel.

