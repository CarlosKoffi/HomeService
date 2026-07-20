-- Rebrand visible platform content from Kaza/ProxiPro to wélé.
-- Safe to run multiple times. It updates editorial/CMS labels only, not technical keys.

UPDATE "CountryBrandings"
SET "BrandName" = 'wélé',
    "UpdatedAt" = now()
WHERE "BrandName" IN ('Kaza', 'ProxiPro', 'ProxiPro CI', 'Kaza CI');

UPDATE "CmsSites"
SET "Name" = CASE
    WHEN "Code" = 'company' THEN 'wélé entreprises'
    WHEN "Code" = 'provider' THEN 'wélé prestataires'
    WHEN "Code" = 'client' THEN 'wélé clients'
    ELSE replace(replace("Name", 'Kaza', 'wélé'), 'ProxiPro', 'wélé')
END,
    "UpdatedAt" = now()
WHERE "Name" LIKE '%Kaza%'
   OR "Name" LIKE '%ProxiPro%'
   OR "Code" IN ('company', 'provider', 'client');

UPDATE "CmsPages"
SET "InternalName" = replace(replace("InternalName", 'Kaza', 'wélé'), 'ProxiPro', 'wélé'),
    "UpdatedAt" = now()
WHERE "InternalName" LIKE '%Kaza%'
   OR "InternalName" LIKE '%ProxiPro%';

UPDATE "CmsPageTranslations"
SET "Title" = replace(replace("Title", 'Kaza', 'wélé'), 'ProxiPro', 'wélé'),
    "SeoTitle" = CASE
        WHEN "SeoTitle" IS NULL THEN NULL
        ELSE replace(replace("SeoTitle", 'Kaza', 'wélé'), 'ProxiPro', 'wélé')
    END,
    "MetaDescription" = CASE
        WHEN "MetaDescription" IS NULL THEN NULL
        ELSE replace(replace("MetaDescription", 'Kaza', 'wélé'), 'ProxiPro', 'wélé')
    END,
    "UpdatedAt" = now()
WHERE "Title" LIKE '%Kaza%'
   OR "Title" LIKE '%ProxiPro%'
   OR "SeoTitle" LIKE '%Kaza%'
   OR "SeoTitle" LIKE '%ProxiPro%'
   OR "MetaDescription" LIKE '%Kaza%'
   OR "MetaDescription" LIKE '%ProxiPro%';

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
SET "TextValue" = replace("TextValue", 'images/kaza-', 'images/wele-'),
    "UpdatedAt" = now()
WHERE "TextValue" LIKE '%images/kaza-%';

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

UPDATE "CmsContentValues"
SET "JsonValue" = replace("JsonValue"::text, 'images/kaza-', 'images/wele-')::jsonb,
    "UpdatedAt" = now()
WHERE "JsonValue"::text LIKE '%images/kaza-%';

UPDATE "TranslationValues"
SET "Value" = replace(replace("Value", 'Kaza', 'wélé'), 'ProxiPro', 'wélé'),
    "UpdatedAt" = now()
WHERE "Value" LIKE '%Kaza%'
   OR "Value" LIKE '%ProxiPro%';
