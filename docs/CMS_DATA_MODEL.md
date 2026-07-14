# Modele de donnees CMS

Ce document decrit le premier socle CMS ajoute dans la migration `AddCmsFoundation`.

## Noyau

- `CmsSite`: surface numerique distincte, par exemple site entreprises ou app prestataire.
- `CmsComponentDefinition`: composant stable attendu par le frontend, versionne par schema.
- `CmsPage`: page rattachee a un site.
- `CmsPageTranslation`: slug, titre et metadonnees par langue.
- `CmsPageVersion`: version editable, publiee ou archivee d'une page.
- `CmsSection`: instance ordonnee d'un composant dans une zone de page.
- `CmsContentValue`: valeur typee d'un champ de section.
- `CmsMenu`: menu rattache a un site et a un emplacement.
- `CmsMenuItem`: element de navigation, hierarchique si necessaire.
- `CmsMediaAsset`: media logique, sans binaire en base.
- `CmsMediaVariant`: variantes optimisees d'un media.

## Contraintes importantes

- `CmsSite.Code` est unique.
- `CmsComponentDefinition.Key + SchemaVersion` est unique.
- `CmsPage.SiteId + Code` est unique.
- `CmsPageTranslation.SiteId + LanguageId + Slug` est unique.
- `CmsPageVersion.PageId + VersionNumber` est unique.
- `CmsSection.PageVersionId + Zone + Position` est unique.
- `CmsMenu.SiteId + Code` est unique.
- `CmsMenuItem.MenuId + ParentMenuItemId + Position` est unique.
- `CmsMediaAsset.StoragePath` est unique.
- `CmsMediaVariant.MediaAssetId + VariantKey` est unique.

## Choix volontairement reportes

- Gestion detaillee des permissions CMS.
- API de lecture publique CMS.
- Ecran admin de composition.
- Upload/optimisation media CMS.
- Preview token.
- Publication planifiee executee par worker.

Ces sujets seront livres par lots separes pour eviter un gros bloc fragile.
