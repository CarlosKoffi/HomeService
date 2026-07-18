-- Update visible admin labels after simplifying the localization scope to translations only.
-- Non-destructive: only wording is changed.

UPDATE "AdminModules"
SET
    "Name" = 'Traductions',
    "Description" = 'Gestion des langues et textes traduisibles.',
    "UpdatedAt" = now()
WHERE "Key" = 'Localization';

UPDATE "AdminRoles"
SET
    "Description" = 'Peut gerer les textes et les langues.',
    "UpdatedAt" = now()
WHERE "Name" = 'Contenu et traduction';

UPDATE "TranslationValues"
SET
    "Value" = 'Traductions',
    "UpdatedAt" = now()
WHERE "TranslationKeyId" IN (
    SELECT "Id"
    FROM "TranslationKeys"
    WHERE "Key" = 'admin.localization.title'
);
