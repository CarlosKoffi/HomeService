# Architecture

La solution est organisee en couches .NET afin de garder le metier testable et lisible.

## Projets

- `HomeService.Domain`: entites, enums et concepts metier purs.
- `HomeService.Application`: cas d'usage, regles applicatives, interfaces de services.
- `HomeService.Infrastructure`: EF Core, PostgreSQL, configurations, integrations techniques.
- `HomeService.Contracts`: DTO partages entre API et frontaux.
- `HomeService.Api`: endpoints HTTP, composition, authentification, orchestration courte.
- `HomeService.Admin`: back-office Kaza.
- `HomeService.Company`: portail entreprise.
- `HomeService.Provider`: portail prestataire web/mobile-first.
- `HomeService.Provider.Mobile`: base MAUI pour l'application prestataire.
- `HomeService.Client`: site/application client final.

## Direction des dependances

Les dependances doivent rester dans ce sens:

`Domain` <- `Application` <- `Infrastructure` / `Api` / frontaux

Regles:

- `Domain` ne depend pas de EF, HTTP, Blazor ou d'un fournisseur externe.
- `Application` porte le metier testable et expose des abstractions.
- `Infrastructure` implemente les acces techniques.
- `Api` ne doit pas concentrer de logique metier durable.

## Etat actuel a surveiller

`src/HomeService.Api/Program.cs` contient encore beaucoup de routes et de logique inline. Les prochains gros blocs doivent etre extraits progressivement vers:

- des groupes d'endpoints par domaine;
- des services applicatifs testables;
- des validateurs d'entree;
- des mappers DTO.

L'objectif n'est pas de tout refondre d'un coup, mais de sortir chaque nouveau bloc proprement et de reduire `Program.cs` par lots.

## Frontaux

Les frontaux Blazor doivent:

- charger les donnees via l'API;
- eviter les mocks en production;
- garder les mocks uniquement derriere une route ou un mode demo clair;
- afficher des etats de chargement, vide, erreur et succes;
- rester mobile-first, surtout pour `Provider`.

## Mobile Afrique

Pour les ecrans prestataire et client:

- payloads courts;
- pas d'image lourde sans compression/CDN;
- actions tolerantes aux reseaux instables;
- textes courts;
- boutons visibles;
- navigation simple;
- cache/session quand pertinent.

