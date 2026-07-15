START TRANSACTION;

DO $$
DECLARE
    v_now timestamptz := now();
    v_language_id uuid;
    v_country_id uuid;
    v_site_id uuid;
    v_page_id uuid;
    v_version_id uuid;
    v_hero_id uuid;
    v_steps_id uuid;
    v_trusted_id uuid;
    v_dashboard_id uuid;
    v_faq_id uuid;
    v_contact_id uuid;
    v_footer_id uuid;
BEGIN
    SELECT "Id" INTO v_language_id FROM "Languages" WHERE "Code" = 'fr' LIMIT 1;
    SELECT "Id" INTO v_country_id FROM "Countries" WHERE "IsoCode" = 'CI' LIMIT 1;

    IF v_language_id IS NULL THEN
        RAISE EXCEPTION 'Language fr is required before seeding the CMS.';
    END IF;

    INSERT INTO "CmsComponentDefinitions" ("Id", "Key", "Name", "Description", "SchemaVersion", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES
        (gen_random_uuid(), 'HeroStandard', 'Hero premium', 'Section hero premium issue du design Figma Kaza entreprises.', 1, true, v_now, v_now),
        (gen_random_uuid(), 'StepsTimeline', 'Parcours en etapes', 'Parcours entreprise court en trois etapes.', 1, true, v_now, v_now),
        (gen_random_uuid(), 'TrustedLogos', 'Preuve sociale', 'Bande de references et preuves de confiance.', 1, true, v_now, v_now),
        (gen_random_uuid(), 'DashboardPreview', 'Apercu dashboard', 'Mockup produit avec indicateurs et activite.', 1, true, v_now, v_now),
        (gen_random_uuid(), 'FaqAccordion', 'Foire aux questions', 'Questions/reponses administrables.', 1, true, v_now, v_now),
        (gen_random_uuid(), 'ContactForm', 'Formulaire de contact', 'Formulaire editorial de prise de contact.', 1, true, v_now, v_now),
        (gen_random_uuid(), 'FooterLinks', 'Liens footer', 'Colonnes de liens de bas de page.', 1, true, v_now, v_now)
    ON CONFLICT ("Key", "SchemaVersion") DO UPDATE SET
        "Name" = EXCLUDED."Name",
        "Description" = EXCLUDED."Description",
        "IsActive" = true,
        "UpdatedAt" = v_now;

    SELECT "Id" INTO v_hero_id FROM "CmsComponentDefinitions" WHERE "Key" = 'HeroStandard' AND "SchemaVersion" = 1;
    SELECT "Id" INTO v_steps_id FROM "CmsComponentDefinitions" WHERE "Key" = 'StepsTimeline' AND "SchemaVersion" = 1;
    SELECT "Id" INTO v_trusted_id FROM "CmsComponentDefinitions" WHERE "Key" = 'TrustedLogos' AND "SchemaVersion" = 1;
    SELECT "Id" INTO v_dashboard_id FROM "CmsComponentDefinitions" WHERE "Key" = 'DashboardPreview' AND "SchemaVersion" = 1;
    SELECT "Id" INTO v_faq_id FROM "CmsComponentDefinitions" WHERE "Key" = 'FaqAccordion' AND "SchemaVersion" = 1;
    SELECT "Id" INTO v_contact_id FROM "CmsComponentDefinitions" WHERE "Key" = 'ContactForm' AND "SchemaVersion" = 1;
    SELECT "Id" INTO v_footer_id FROM "CmsComponentDefinitions" WHERE "Key" = 'FooterLinks' AND "SchemaVersion" = 1;

    INSERT INTO "CmsSites" ("Id", "Code", "Name", "Surface", "Status", "DefaultCountryId", "DefaultLanguageId", "HomePageCode", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), 'company-public', 'Kaza entreprises', 'PublicCompany', 'Active', v_country_id, v_language_id, 'home', v_now, v_now)
    ON CONFLICT ("Code") DO UPDATE SET
        "Name" = EXCLUDED."Name",
        "Surface" = EXCLUDED."Surface",
        "Status" = 'Active',
        "DefaultCountryId" = EXCLUDED."DefaultCountryId",
        "DefaultLanguageId" = EXCLUDED."DefaultLanguageId",
        "HomePageCode" = 'home',
        "UpdatedAt" = v_now;

    SELECT "Id" INTO v_site_id FROM "CmsSites" WHERE "Code" = 'company-public';

    INSERT INTO "CmsPages" ("Id", "SiteId", "Code", "InternalName", "TemplateKey", "Status", "RequiresAuthentication", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), v_site_id, 'home', 'Accueil entreprises', 'premium-b2b-landing', 'Draft', false, v_now, v_now)
    ON CONFLICT ("SiteId", "Code") DO UPDATE SET
        "InternalName" = EXCLUDED."InternalName",
        "TemplateKey" = EXCLUDED."TemplateKey",
        "RequiresAuthentication" = false,
        "UpdatedAt" = v_now;

    SELECT "Id" INTO v_page_id FROM "CmsPages" WHERE "SiteId" = v_site_id AND "Code" = 'home';

    INSERT INTO "CmsPageTranslations" ("Id", "SiteId", "PageId", "LanguageId", "Slug", "Title", "SeoTitle", "MetaDescription", "TranslationStatus", "CreatedAt", "UpdatedAt")
    VALUES (
        gen_random_uuid(),
        v_site_id,
        v_page_id,
        v_language_id,
        'entreprises',
        'Kaza pour les entreprises',
        'Kaza entreprises - recevez plus de missions',
        'Kaza connecte les clients aux entreprises de services a domicile verifiees en Cote d''Ivoire.',
        'Draft',
        v_now,
        v_now)
    ON CONFLICT ("SiteId", "LanguageId", "Slug") DO UPDATE SET
        "PageId" = EXCLUDED."PageId",
        "Title" = EXCLUDED."Title",
        "SeoTitle" = EXCLUDED."SeoTitle",
        "MetaDescription" = EXCLUDED."MetaDescription",
        "UpdatedAt" = v_now;

    INSERT INTO "CmsPageVersions" ("Id", "PageId", "VersionNumber", "Status", "PublishedAt", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), v_page_id, 1, 'Draft', NULL, v_now, v_now)
    ON CONFLICT ("PageId", "VersionNumber") DO UPDATE SET
        "Status" = EXCLUDED."Status",
        "UpdatedAt" = v_now;

    SELECT "Id" INTO v_version_id FROM "CmsPageVersions" WHERE "PageId" = v_page_id AND "VersionNumber" = 1;

    DELETE FROM "CmsSections"
    WHERE "PageVersionId" = v_version_id
      AND "Zone" = 'main';

    INSERT INTO "CmsSections" ("Id", "PageVersionId", "ComponentDefinitionId", "InternalName", "Zone", "Position", "Anchor", "Variant", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES
        (gen_random_uuid(), v_version_id, v_hero_id, 'Hero premium entreprises', 'main', 1, 'home', 'premium', true, v_now, v_now),
        (gen_random_uuid(), v_version_id, v_steps_id, 'Fonctionnement entreprises', 'main', 2, 'how', 'three-step-premium', true, v_now, v_now),
        (gen_random_uuid(), v_version_id, v_trusted_id, 'Preuve sociale entreprises', 'main', 3, 'trusted', 'premium-strip', true, v_now, v_now),
        (gen_random_uuid(), v_version_id, v_dashboard_id, 'Apercu dashboard entreprises', 'main', 4, 'dashboard', 'saas-preview', true, v_now, v_now),
        (gen_random_uuid(), v_version_id, v_faq_id, 'FAQ entreprises', 'main', 5, 'faq', 'accordion', true, v_now, v_now),
        (gen_random_uuid(), v_version_id, v_contact_id, 'Contact entreprises', 'main', 6, 'contact', 'two-column-form', true, v_now, v_now),
        (gen_random_uuid(), v_version_id, v_footer_id, 'Footer entreprises', 'main', 7, NULL, 'black-footer', true, v_now, v_now);
END $$;

COMMIT;
