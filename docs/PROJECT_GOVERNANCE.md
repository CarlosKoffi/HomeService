# Gouvernance Kaza

Ce document sert de garde-fou pour les prochains lots de developpement. Il reprend les principes du prompt maitre et les rend actionnables dans le repo.

## Objectif produit

Kaza est une plateforme de services a domicile pour l'Afrique de l'Ouest, avec un demarrage en Cote d'Ivoire.

Le systeme relie:

- des clients qui demandent un service;
- des entreprises partenaires qui valident, encadrent et affectent leurs prestataires;
- des prestataires qui recoivent et executent les missions;
- une equipe admin Kaza qui controle la qualite, les validations, les contenus, les droits et le suivi operationnel.

## Principes non negociables

- Pas de texte produit durable en dur quand il doit etre administrable ou traduisible.
- Toute evolution de schema doit avoir une migration EF et un script SQL de reference dans `src/HomeService.Admin/Sql`.
- Les actions qui touchent la base doivent etre auditables quand elles concernent un utilisateur, une entreprise, un prestataire, une mission, un paiement, un document ou un statut.
- Les endpoints API doivent rester fins: validation d'entree, appel application/metier, mapping DTO, reponse.
- Les regles metier doivent vivre dans `HomeService.Application` ou dans des services clairement isoles et testables.
- Les DTO doivent etre adaptes a l'ecran cible, surtout pour mobile Afrique: pas de payload inutile, pas d'aller-retour evitable.
- Chaque lot significatif doit etre teste avant commit.
- Les commits doivent rester petits et comprehensibles.

## Ordre de priorite

1. Stabilite API et modele de donnees.
2. Portail entreprise et onboarding complet.
3. Parcours prestataire mobile-first.
4. Admin, droits, audit, notifications et contenu multilingue.
5. Client final, paiement, missions live et algorithmes d'affectation.

## Definition of done

Un lot est considere pret quand:

- le comportement attendu est implemente;
- les changements de base sont versionnes;
- les tests unitaires ou integration pertinents passent;
- les pages principales ont ete verifiees en responsive quand le front est touche;
- les nouveaux textes importants sont preparables pour traduction;
- le commit ne contient pas d'artefacts generes (`bin`, `obj`, logs, builds locaux).

## Questions a poser avant de coder

Interrompre le travail et demander une clarification si:

- une regle impacte le paiement, la commission, la responsabilite ou le droit du travail;
- un changement peut casser le deploiement Coolify;
- une migration risque de modifier des donnees existantes;
- une action admin peut avoir des consequences irreversibles;
- le wording ou le design peut changer la comprehension utilisateur.

