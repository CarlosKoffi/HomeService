DO $$
DECLARE
    v_section_id uuid;
BEGIN
    SELECT section."Id"
    INTO v_section_id
    FROM "CmsSites" site
    JOIN "CmsPages" page ON page."SiteId" = site."Id"
    JOIN "CmsPageVersions" version ON version."PageId" = page."Id"
    JOIN "CmsSections" section ON section."PageVersionId" = version."Id"
    JOIN "CmsComponentDefinitions" component ON component."Id" = section."ComponentDefinitionId"
    WHERE site."Code" = 'provider-public'
        AND page."Code" = 'home'
        AND component."Key" = 'StepsTimeline'
        AND version."VersionNumber" = (
            SELECT MAX(v2."VersionNumber")
            FROM "CmsPageVersions" v2
            WHERE v2."PageId" = page."Id"
        )
    LIMIT 1;

    IF v_section_id IS NULL THEN
        RETURN;
    END IF;

    UPDATE "CmsContentValues"
    SET "TextValue" = CASE "FieldKey"
        WHEN 'subtitle' THEN 'Un parcours simple pour proposer votre profil en interim a une entreprise partenaire.'
        ELSE "TextValue"
    END,
    "UpdatedAt" = now()
    WHERE "SectionId" = v_section_id
        AND "FieldKey" = 'subtitle'
        AND "LanguageId" IS NULL;

    INSERT INTO "CmsContentValues" ("Id", "SectionId", "FieldKey", "ValueType", "TextValue", "CreatedAt")
    SELECT gen_random_uuid(), v_section_id, 'subtitle', 'LongText',
        'Un parcours simple pour proposer votre profil en interim a une entreprise partenaire.', now()
    WHERE NOT EXISTS (
        SELECT 1 FROM "CmsContentValues"
        WHERE "SectionId" = v_section_id
            AND "FieldKey" = 'subtitle'
            AND "LanguageId" IS NULL
    );

    UPDATE "CmsContentValues"
    SET "JsonValue" = '[
        {
            "Number": "01",
            "Label": "Formulaire",
            "Title": "Creez votre compte en ligne",
            "Description": "Renseignez vos informations, votre service principal et votre zone.",
            "ImageUrl": "images/kaza-provider-step-1.svg"
        },
        {
            "Number": "02",
            "Label": "Entreprise",
            "Title": "Choisissez une entreprise",
            "Description": "Kaza vous propose des entreprises qui acceptent les profils interimaires dans votre domaine.",
            "ImageUrl": "images/kaza-provider-step-2.svg"
        },
        {
            "Number": "03",
            "Label": "Validation",
            "Title": "L''entreprise etudie votre demande",
            "Description": "Si elle vous valide, vous pourrez recevoir des missions dans l''application mobile.",
            "ImageUrl": "images/kaza-provider-step-3.svg"
        }
    ]'::jsonb,
    "UpdatedAt" = now()
    WHERE "SectionId" = v_section_id
        AND "FieldKey" = 'steps'
        AND "LanguageId" IS NULL;

    INSERT INTO "CmsContentValues" ("Id", "SectionId", "FieldKey", "ValueType", "JsonValue", "CreatedAt")
    SELECT gen_random_uuid(), v_section_id, 'steps', 'Json', '[
        {
            "Number": "01",
            "Label": "Formulaire",
            "Title": "Creez votre compte en ligne",
            "Description": "Renseignez vos informations, votre service principal et votre zone.",
            "ImageUrl": "images/kaza-provider-step-1.svg"
        },
        {
            "Number": "02",
            "Label": "Entreprise",
            "Title": "Choisissez une entreprise",
            "Description": "Kaza vous propose des entreprises qui acceptent les profils interimaires dans votre domaine.",
            "ImageUrl": "images/kaza-provider-step-2.svg"
        },
        {
            "Number": "03",
            "Label": "Validation",
            "Title": "L''entreprise etudie votre demande",
            "Description": "Si elle vous valide, vous pourrez recevoir des missions dans l''application mobile.",
            "ImageUrl": "images/kaza-provider-step-3.svg"
        }
    ]'::jsonb, now()
    WHERE NOT EXISTS (
        SELECT 1 FROM "CmsContentValues"
        WHERE "SectionId" = v_section_id
            AND "FieldKey" = 'steps'
            AND "LanguageId" IS NULL
    );
END $$;
