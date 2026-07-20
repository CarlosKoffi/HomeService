DO $$
DECLARE
    v_language_id uuid;
    v_hero_section_id uuid;
    v_steps_section_id uuid;
BEGIN
    SELECT "Id"
    INTO v_language_id
    FROM "Languages"
    WHERE "Code" = 'fr'
    LIMIT 1;

    SELECT section."Id"
    INTO v_hero_section_id
    FROM "CmsSites" site
    JOIN "CmsPages" page ON page."SiteId" = site."Id"
    JOIN "CmsPageVersions" version ON version."PageId" = page."Id"
    JOIN "CmsSections" section ON section."PageVersionId" = version."Id"
    JOIN "CmsComponentDefinitions" component ON component."Id" = section."ComponentDefinitionId"
    WHERE site."Code" = 'provider-public'
        AND page."Code" = 'home'
        AND component."Key" = 'HeroStandard'
        AND version."VersionNumber" = (
            SELECT MAX(v2."VersionNumber")
            FROM "CmsPageVersions" v2
            WHERE v2."PageId" = page."Id"
        )
    LIMIT 1;

    SELECT section."Id"
    INTO v_steps_section_id
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

    IF v_hero_section_id IS NOT NULL THEN
        UPDATE "CmsContentValues"
        SET "TextValue" = '/onboarding',
            "UpdatedAt" = now()
        WHERE "SectionId" = v_hero_section_id
            AND "FieldKey" = 'primaryCta.url'
            AND ("LanguageId" = v_language_id OR "LanguageId" IS NULL);
    END IF;

    IF v_steps_section_id IS NULL THEN
        RETURN;
    END IF;

    UPDATE "CmsContentValues"
    SET "TextValue" = 'Un parcours simple pour proposer votre profil en interim a une entreprise partenaire.',
        "UpdatedAt" = now()
    WHERE "SectionId" = v_steps_section_id
        AND "FieldKey" = 'subtitle'
        AND ("LanguageId" = v_language_id OR "LanguageId" IS NULL);

    INSERT INTO "CmsContentValues" ("Id", "SectionId", "FieldKey", "ValueType", "LanguageId", "TextValue", "CreatedAt")
    SELECT gen_random_uuid(), v_steps_section_id, 'subtitle', 'LongText', v_language_id,
        'Un parcours simple pour proposer votre profil en interim a une entreprise partenaire.', now()
    WHERE NOT EXISTS (
        SELECT 1 FROM "CmsContentValues"
        WHERE "SectionId" = v_steps_section_id
            AND "FieldKey" = 'subtitle'
            AND "LanguageId" = v_language_id
    );

    UPDATE "CmsContentValues"
    SET "JsonValue" = '[
        {
            "number": "01",
            "label": "Formulaire",
            "title": "Creez votre compte en ligne",
            "text": "Renseignez vos informations, votre service principal et votre zone.",
            "image": "images/kaza-provider-step-1.svg"
        },
        {
            "number": "02",
            "label": "Entreprise",
            "title": "Choisissez une entreprise",
            "text": "wélé vous propose des entreprises qui acceptent les profils interimaires dans votre domaine.",
            "image": "images/kaza-provider-step-2.svg"
        },
        {
            "number": "03",
            "label": "Validation",
            "title": "L''entreprise etudie votre demande",
            "text": "Si elle vous valide, vous pourrez recevoir des missions dans l''application mobile.",
            "image": "images/kaza-provider-step-3.svg"
        }
    ]'::jsonb,
    "UpdatedAt" = now()
    WHERE "SectionId" = v_steps_section_id
        AND "FieldKey" = 'steps'
        AND ("LanguageId" = v_language_id OR "LanguageId" IS NULL);

    INSERT INTO "CmsContentValues" ("Id", "SectionId", "FieldKey", "ValueType", "LanguageId", "JsonValue", "CreatedAt")
    SELECT gen_random_uuid(), v_steps_section_id, 'steps', 'Json', v_language_id, '[
        {
            "number": "01",
            "label": "Formulaire",
            "title": "Creez votre compte en ligne",
            "text": "Renseignez vos informations, votre service principal et votre zone.",
            "image": "images/kaza-provider-step-1.svg"
        },
        {
            "number": "02",
            "label": "Entreprise",
            "title": "Choisissez une entreprise",
            "text": "wélé vous propose des entreprises qui acceptent les profils interimaires dans votre domaine.",
            "image": "images/kaza-provider-step-2.svg"
        },
        {
            "number": "03",
            "label": "Validation",
            "title": "L''entreprise etudie votre demande",
            "text": "Si elle vous valide, vous pourrez recevoir des missions dans l''application mobile.",
            "image": "images/kaza-provider-step-3.svg"
        }
    ]'::jsonb, now()
    WHERE NOT EXISTS (
        SELECT 1 FROM "CmsContentValues"
        WHERE "SectionId" = v_steps_section_id
            AND "FieldKey" = 'steps'
            AND "LanguageId" = v_language_id
    );
END $$;
