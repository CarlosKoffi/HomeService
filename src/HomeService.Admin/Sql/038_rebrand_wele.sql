-- Rebrand visible platform content from Kaza/ProxiPro to wélé.
-- Safe to run multiple times. It updates editorial/CMS labels only, not technical keys.

UPDATE "CountryBrandings"
SET "BrandName" = 'wélé',
    "UpdatedAt" = now()
WHERE "BrandName" IN ('Kaza', 'ProxiPro', 'ProxiPro CI', 'Kaza CI');

UPDATE "CmsSites"
SET "Name" = CASE "Code"
    WHEN 'company-public' THEN 'wélé entreprises'
    WHEN 'provider-public' THEN 'wélé prestataires'
    WHEN 'client-public' THEN 'wélé clients'
    ELSE replace(replace("Name", 'Kaza', 'wélé'), 'ProxiPro', 'wélé')
END,
    "UpdatedAt" = now()
WHERE "Name" LIKE '%Kaza%'
   OR "Name" LIKE '%ProxiPro%'
   OR "Code" IN ('company-public', 'provider-public', 'client-public');

UPDATE "CmsPages"
SET "SeoTitle" = replace(replace("SeoTitle", 'Kaza', 'wélé'), 'ProxiPro', 'wélé'),
    "UpdatedAt" = now()
WHERE "SeoTitle" LIKE '%Kaza%'
   OR "SeoTitle" LIKE '%ProxiPro%';

UPDATE "CmsContentValues"
SET "TextValue" = replace(
        replace(
            replace("TextValue", 'Kaza Technologies', 'wélé Technologies'),
            'Kaza',
            'wélé'
        ),
        'ProxiPro',
        'wélé'
    ),
    "UpdatedAt" = now()
WHERE "TextValue" LIKE '%Kaza%'
   OR "TextValue" LIKE '%ProxiPro%';

UPDATE "CmsContentValues"
SET "JsonValue" = replace(
        replace(
            replace("JsonValue"::text, 'Kaza Technologies', 'wélé Technologies'),
            'Kaza',
            'wélé'
        ),
        'ProxiPro',
        'wélé'
    )::jsonb,
    "UpdatedAt" = now()
WHERE "JsonValue"::text LIKE '%Kaza%'
   OR "JsonValue"::text LIKE '%ProxiPro%';

UPDATE "TranslationValues"
SET "Value" = replace(replace("Value", 'Kaza', 'wélé'), 'ProxiPro', 'wélé'),
    "UpdatedAt" = now()
WHERE "Value" LIKE '%Kaza%'
   OR "Value" LIKE '%ProxiPro%';
