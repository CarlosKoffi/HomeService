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

    INSERT INTO "CmsContentValues" ("Id", "SectionId", "FieldKey", "ValueType", "LanguageId", "TextValue", "JsonValue", "CreatedAt", "UpdatedAt")
    SELECT
        gen_random_uuid(),
        section."Id",
        content."FieldKey",
        content."ValueType",
        v_language_id,
        content."TextValue",
        content."JsonValue",
        v_now,
        v_now
    FROM (
        VALUES
            ('Hero premium entreprises', 'label', 'ShortText', 'Plateforme partenaire', NULL::jsonb),
            ('Hero premium entreprises', 'headline', 'ShortText', 'Recevez plus de missions.', NULL::jsonb),
            ('Hero premium entreprises', 'highlight', 'ShortText', 'Developpez votre entreprise.', NULL::jsonb),
            ('Hero premium entreprises', 'body', 'LongText', 'Kaza connecte les clients aux entreprises de services a domicile verifiees. Vous gardez le controle de vos equipes, de vos demandes et de vos interventions.', NULL::jsonb),
            ('Hero premium entreprises', 'primaryCtaLabel', 'ShortText', 'Commencer', NULL::jsonb),
            ('Hero premium entreprises', 'primaryCtaUrl', 'InternalLink', 'register', NULL::jsonb),
            ('Hero premium entreprises', 'secondaryCtaLabel', 'ShortText', 'Voir le fonctionnement', NULL::jsonb),
            ('Hero premium entreprises', 'secondaryCtaUrl', 'InternalLink', '#how', NULL::jsonb),
            ('Hero premium entreprises', 'imagePath', 'Media', 'images/kaza-premium-hero.png', NULL::jsonb),
            ('Hero premium entreprises', 'proofItems', 'Json', NULL, '["Inscription gratuite","Validation dossier","Portail entreprise"]'::jsonb),

            ('Fonctionnement entreprises', 'label', 'ShortText', 'Comment ca marche', NULL::jsonb),
            ('Fonctionnement entreprises', 'title', 'ShortText', 'Trois etapes, puis votre portail est pret.', NULL::jsonb),
            ('Fonctionnement entreprises', 'body', 'LongText', 'Un parcours court pour verifier l''entreprise et demarrer avec une base claire.', NULL::jsonb),
            ('Fonctionnement entreprises', 'steps', 'Json', NULL, '[{"number":"01","label":"Compte","title":"Creez votre compte","body":"Renseignez votre entreprise, vos services et le contact responsable.","imagePath":"images/kaza-how-step-1.png"},{"number":"02","label":"Verification","title":"Nous verifions votre dossier","body":"Kaza controle les informations pour securiser les clients et les missions.","imagePath":"images/kaza-how-step-2.png"},{"number":"03","label":"Portail","title":"Travaillez depuis votre portail","body":"Ajoutez vos prestataires, recevez des demandes et suivez vos interventions.","imagePath":"images/kaza-how-step-3.png"}]'::jsonb),

            ('Preuve sociale entreprises', 'title', 'ShortText', 'Ils font confiance a Kaza', NULL::jsonb),
            ('Preuve sociale entreprises', 'items', 'Json', NULL, '["Services verifies","Entreprises locales","Prestataires suivis","Paiements traces","Support partenaire"]'::jsonb),

            ('Apercu dashboard entreprises', 'label', 'ShortText', 'Dashboard', NULL::jsonb),
            ('Apercu dashboard entreprises', 'title', 'ShortText', 'Tout ce qui compte, lisible en un coup d''oeil.', NULL::jsonb),
            ('Apercu dashboard entreprises', 'body', 'LongText', 'Demandes, equipe, missions, documents et paiements restent au meme endroit.', NULL::jsonb),
            ('Apercu dashboard entreprises', 'stats', 'Json', NULL, '[{"label":"Demandes","value":"12","caption":"+4 cette semaine"},{"label":"Assignees","value":"8","caption":"Equipe mobilisee"},{"label":"Paiements","value":"185k","caption":"XOF suivis"}]'::jsonb),
            ('Apercu dashboard entreprises', 'requests', 'Json', NULL, '["Menage a Cocody Riviera","Jardinage a Marcory","Nounou aux Deux Plateaux"]'::jsonb),
            ('Apercu dashboard entreprises', 'providers', 'Json', NULL, '["Awa K. - Menage","Jean M. - Jardinage","Fatou C. - Nounou"]'::jsonb),

            ('FAQ entreprises', 'label', 'ShortText', 'FAQ', NULL::jsonb),
            ('FAQ entreprises', 'title', 'ShortText', 'Foire aux questions', NULL::jsonb),
            ('FAQ entreprises', 'items', 'Json', NULL, '[{"question":"Comment sont verifiees les entreprises sur Kaza ?","answer":"Nous verifions les informations de l''entreprise, les documents essentiels et le contact responsable avant l''activation complete."},{"question":"L''inscription est-elle gratuite ?","answer":"Oui. L''inscription est gratuite. Kaza applique ensuite une commission uniquement sur les missions realisees."},{"question":"Puis-je refuser une demande client ?","answer":"Oui. Votre entreprise reste libre d''accepter les demandes qui correspondent a son equipe, sa zone et ses disponibilites."},{"question":"Qui choisit le prestataire ?","answer":"Vous pouvez affecter vous-meme un prestataire depuis le portail ou laisser Kaza vous accompagner selon le mode choisi."},{"question":"Comment sont suivis les paiements ?","answer":"Le portail permet de suivre les paiements Mobile Money, les encaissements terrain et les commissions."},{"question":"Combien de temps prend la validation ?","answer":"Elle depend de la qualite du dossier. Plus les informations sont claires, plus la validation est rapide."}]'::jsonb),

            ('Contact entreprises', 'label', 'ShortText', 'Contact', NULL::jsonb),
            ('Contact entreprises', 'title', 'ShortText', 'Vous voulez en parler avant de vous inscrire ?', NULL::jsonb),
            ('Contact entreprises', 'body', 'LongText', 'Laissez vos coordonnees. Nous vous rappelons pour voir comment Kaza peut aider votre entreprise.', NULL::jsonb),
            ('Contact entreprises', 'tags', 'Json', NULL, '["Abidjan","Services a domicile","Partenariat entreprise"]'::jsonb),

            ('Footer entreprises', 'brand', 'ShortText', 'Kaza', NULL::jsonb),
            ('Footer entreprises', 'body', 'LongText', 'La plateforme qui connecte les clients aux entreprises de services a domicile verifiees.', NULL::jsonb),
            ('Footer entreprises', 'columns', 'Json', NULL, '[{"title":"Produit","links":[{"label":"Comment ca marche","url":"#how"},{"label":"Dashboard","url":"#dashboard"},{"label":"FAQ","url":"#faq"}]},{"title":"Entreprise","links":[{"label":"Devenir partenaire","url":"register"},{"label":"Se connecter","url":"dashboard"},{"label":"Contact","url":"#contact"}]},{"title":"Legal","links":[{"label":"Support","url":"#contact"},{"label":"Conditions partenaires","url":"#contact"},{"label":"Confidentialite","url":"#contact"}]}]'::jsonb)
    ) AS content("SectionName", "FieldKey", "ValueType", "TextValue", "JsonValue")
    JOIN "CmsSections" section
        ON section."PageVersionId" = v_version_id
       AND section."InternalName" = content."SectionName";
END $$;

COMMIT;
