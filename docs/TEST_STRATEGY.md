# Strategie de tests

Le projet doit rester testable par couches. L'objectif est qu'un changement metier soit couvert par des tests unitaires et, si besoin, par un test d'integration ou fonctionnel.

## Etat actuel

Le pipeline GitHub Actions compile la solution en Release, lance les tests unitaires, lance les tests d'integration workflow, puis execute un smoke test non destructif sur l'API deployee quand la livraison part sur `main`.

Au dernier controle local:

- `HomeService.Tests.Unit`: 469 tests passes.
- `HomeService.Tests.Integration`: 10 tests passes.
- total: 479 tests passes.

Commande:

`dotnet build HomeService.sln --configuration Release`

`dotnet test tests/HomeService.Tests.Unit/HomeService.Tests.Unit.csproj --configuration Release --no-build`

`dotnet test tests/HomeService.Tests.Integration/HomeService.Tests.Integration.csproj --configuration Release --no-build`

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
- demande client -> proposition entreprise -> devis -> paiement mocke -> affectation prestataire -> acceptation/refus -> arrivee GPS -> debut/fin -> validation client -> notation.

Les tests d'integration ne doivent jamais appeler les vrais prestataires externes. Les paiements restent simules et les notifications sont verifiees dans l'outbox ou les notifications portail, sans envoi Firebase, email ou WhatsApp reel.

## Smoke post-deploiement

Le smoke test deploiement verifie uniquement que l'application livree et la base sont alignees:

- sante API;
- catalogue services/prestations;
- CMS entreprise et prestataire;
- onboarding prestataire;
- demandes entreprises;
- missions admin;
- parametrage mission;
- notifications admin;
- regles et modeles de notification;
- paiements admin;
- controle d'acces.

Tout `404`, `500` ou `502` bloque la livraison: cela signifie que le code, les routes, les migrations ou les seeders ne sont pas coherents en environnement deployee.

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
