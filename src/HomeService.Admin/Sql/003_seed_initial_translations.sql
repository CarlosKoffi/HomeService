-- Seed initial French/Cote d'Ivoire translations.
-- The application also seeds these values automatically at API startup.

DO $$
DECLARE
    fr_id uuid;
    ci_id uuid;
    key_id uuid;
BEGIN
    SELECT "Id" INTO fr_id FROM "Languages" WHERE "Code" = 'fr' LIMIT 1;
    SELECT "Id" INTO ci_id FROM "Countries" WHERE "IsoCode" = 'CI' LIMIT 1;

    IF fr_id IS NULL OR ci_id IS NULL THEN
        RAISE NOTICE 'Missing fr language or CI country; run base seed first.';
        RETURN;
    END IF;

    CREATE TEMP TABLE IF NOT EXISTS seed_translations (
        key text,
        scope text,
        description text,
        value text
    ) ON COMMIT DROP;

    INSERT INTO seed_translations (key, scope, description, value)
    VALUES
        ('company.home.hero.title', 'Company', 'Titre hero portail entreprise', 'Le service a domicile en toute confiance'),
        ('company.home.hero.subtitle', 'Company', 'Sous-titre hero portail entreprise', 'Inscrivez votre entreprise, faites valider vos prestataires et developpez vos missions a domicile.'),
        ('company.register.title', 'Company', 'Titre inscription entreprise', 'Demande d''inscription'),
        ('company.register.description', 'Company', 'Introduction formulaire inscription', 'Ce formulaire permet a votre entreprise de demander son acces ProxiPro. Notre equipe verifiera les informations et les pieces fournies.'),
        ('company.register.submit', 'Company', 'Bouton envoyer demande', 'Envoyer la demande'),
        ('company.register.success', 'Company', 'Confirmation demande envoyee', 'Demande envoyee. Notre equipe va verifier votre dossier.'),
        ('admin.dashboard.title', 'Admin', 'Titre dashboard admin', 'Centre de controle entreprise'),
        ('admin.companyApplications.title', 'Admin', 'Titre file demandes entreprise', 'Demandes entreprises'),
        ('admin.companyApplications.empty', 'Admin', 'Message liste vide', 'Aucune demande entreprise pour le moment.'),
        ('admin.localization.title', 'Admin', 'Titre page traductions', 'Pays & traductions'),
        ('admin.access.title', 'Admin', 'Titre acces roles', 'Acces & roles'),
        ('common.loading', 'Common', 'Message chargement generique', 'Chargement en cours...'),
        ('common.save', 'Common', 'Action sauvegarder', 'Sauver'),
        ('common.validate', 'Common', 'Action valider', 'Valider'),
        ('common.reject', 'Common', 'Action rejeter', 'Rejeter');

    FOR key_id IN
        INSERT INTO "TranslationKeys" ("Id", "Key", "Description", "Scope", "IsActive", "CreatedAt")
        SELECT gen_random_uuid(), st.key, st.description, st.scope, true, now()
        FROM seed_translations st
        ON CONFLICT ("Key") DO UPDATE SET "Description" = EXCLUDED."Description"
        RETURNING "Id"
    LOOP
        NULL;
    END LOOP;

    INSERT INTO "TranslationValues" ("Id", "TranslationKeyId", "LanguageId", "CountryId", "Value", "CreatedAt")
    SELECT gen_random_uuid(), tk."Id", fr_id, ci_id, st.value, now()
    FROM seed_translations st
    JOIN "TranslationKeys" tk ON tk."Key" = st.key
    ON CONFLICT ("TranslationKeyId", "LanguageId", "CountryId") DO UPDATE SET "Value" = EXCLUDED."Value";
END $$;
