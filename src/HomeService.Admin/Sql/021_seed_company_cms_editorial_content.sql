WITH french AS (
    SELECT "Id"
    FROM "Languages"
    WHERE "Code" = 'fr'
    LIMIT 1
),
company_home_sections AS (
    SELECT
        section."Id" AS "SectionId",
        component."Key" AS "ComponentKey"
    FROM "CmsSites" site
    JOIN "CmsPages" page ON page."SiteId" = site."Id"
    JOIN "CmsPageVersions" version ON version."PageId" = page."Id"
    JOIN "CmsSections" section ON section."PageVersionId" = version."Id"
    JOIN "CmsComponentDefinitions" component ON component."Id" = section."ComponentDefinitionId"
    WHERE site."Code" = 'company-public'
      AND page."Code" = 'home'
      AND version."VersionNumber" = (
          SELECT max(v2."VersionNumber")
          FROM "CmsPageVersions" v2
          WHERE v2."PageId" = page."Id"
      )
),
text_seed AS (
    SELECT *
    FROM (
        VALUES
            ('HeroStandard', 'label', 'ShortText', 'Plateforme partenaire'),
            ('HeroStandard', 'headline', 'ShortText', 'Recevez plus de missions. Developpez votre entreprise.'),
            ('HeroStandard', 'subtitle', 'LongText', 'wélé connecte les clients aux entreprises de services a domicile verifiees. Vous gardez le controle de vos equipes, de vos demandes et de vos interventions.'),
            ('HeroStandard', 'primaryCta.label', 'ShortText', 'Commencer'),
            ('HeroStandard', 'primaryCta.url', 'InternalLink', 'register'),
            ('HeroStandard', 'secondaryCta.label', 'ShortText', 'Voir le fonctionnement'),
            ('HeroStandard', 'secondaryCta.url', 'InternalLink', '#how'),
            ('HeroStandard', 'image.url', 'Media', 'images/kaza-premium-hero.png'),
            ('HeroStandard', 'image.alt', 'ShortText', 'Equipe wélé en intervention chez un client'),
            ('StepsTimeline', 'label', 'ShortText', 'Comment ca marche'),
            ('StepsTimeline', 'headline', 'ShortText', 'Trois etapes, puis votre portail est pret.'),
            ('StepsTimeline', 'subtitle', 'LongText', 'Un parcours court pour verifier l''entreprise et demarrer avec une base claire.'),
            ('TrustedLogos', 'headline', 'ShortText', 'Ils font confiance a wélé'),
            ('DashboardPreview', 'label', 'ShortText', 'Dashboard'),
            ('DashboardPreview', 'headline', 'ShortText', 'Tout ce qui compte, lisible en un coup d''oeil.'),
            ('DashboardPreview', 'subtitle', 'LongText', 'Demandes, equipe, missions, documents et paiements restent au meme endroit.'),
            ('FaqAccordion', 'label', 'ShortText', 'FAQ'),
            ('FaqAccordion', 'headline', 'ShortText', 'Foire aux questions'),
            ('ContactForm', 'label', 'ShortText', 'Contact'),
            ('ContactForm', 'headline', 'ShortText', 'Vous voulez en parler avant de vous inscrire ?'),
            ('ContactForm', 'subtitle', 'LongText', 'Laissez vos coordonnees. Nous vous rappelons pour voir comment wélé peut aider votre entreprise.'),
            ('FooterLinks', 'brandText', 'LongText', 'La plateforme B2B pour connecter clients, entreprises et professionnels de confiance.'),
            ('FooterLinks', 'copyright', 'ShortText', '© 2026 wélé Technologies. Tous droits reserves.'),
            ('FooterLinks', 'baseline', 'ShortText', 'Concu pour l''Afrique de l''Ouest')
    ) AS seed("ComponentKey", "FieldKey", "ValueType", "TextValue")
),
json_seed AS (
    SELECT *
    FROM (
        VALUES
            ('HeroStandard', 'proofItems', '["Inscription gratuite","Validation dossier","Portail entreprise"]'),
            ('StepsTimeline', 'steps', '[{"number":"01","label":"Compte","title":"Creez votre compte","text":"Renseignez votre entreprise, vos services et le contact responsable.","image":"images/kaza-how-step-1.png"},{"number":"02","label":"Verification","title":"Nous verifions votre dossier","text":"wélé controle les informations pour securiser les clients et les missions.","image":"images/kaza-how-step-2.png"},{"number":"03","label":"Portail","title":"Travaillez depuis votre portail","text":"Ajoutez vos prestataires, recevez des demandes et suivez vos interventions.","image":"images/kaza-how-step-3.png"}]'),
            ('TrustedLogos', 'items', '["Services verifies","Entreprises locales","Prestataires suivis","Paiements traces","Support partenaire"]'),
            ('DashboardPreview', 'stats', '[{"label":"Demandes","value":"12","help":"+4 cette semaine"},{"label":"Assignees","value":"8","help":"Equipe mobilisee"},{"label":"Paiements","value":"185k","help":"XOF suivis"}]'),
            ('DashboardPreview', 'requests', '["Menage a Cocody Riviera","Jardinage a Marcory","Nounou aux Deux Plateaux"]'),
            ('DashboardPreview', 'providers', '["Awa K. - Menage","Jean M. - Jardinage","Fatou C. - Nounou"]'),
            ('FaqAccordion', 'questions', '[{"question":"Comment sont verifiees les entreprises sur wélé ?","answer":"Nous verifions les informations de l''entreprise, les documents essentiels et le contact responsable avant l''activation complete."},{"question":"L''inscription est-elle gratuite ?","answer":"Oui. L''inscription est gratuite. wélé applique ensuite une commission uniquement sur les missions realisees."},{"question":"Puis-je refuser une demande client ?","answer":"Oui. Votre entreprise reste libre d''accepter les demandes qui correspondent a son equipe, sa zone et ses disponibilites."},{"question":"Qui choisit le prestataire ?","answer":"Vous pouvez affecter vous-meme un prestataire depuis le portail ou laisser wélé vous accompagner selon le mode choisi."},{"question":"Comment sont suivis les paiements ?","answer":"Le portail permet de suivre les paiements Mobile Money, les encaissements terrain et les commissions."},{"question":"Combien de temps prend la validation ?","answer":"Elle depend de la qualite du dossier. Plus les informations sont claires, plus la validation est rapide."}]'),
            ('ContactForm', 'tags', '["Abidjan","Services a domicile","Partenariat entreprise"]'),
            ('FooterLinks', 'columns', '[{"title":"Produit","links":["Plateforme","Fonctionnement","Tarifs","Securite","Integrations","Changelog"]},{"title":"Entreprise","links":["A propos","Blog","Carrieres","Presse","Partenaires"]},{"title":"Ressources","links":["Documentation","Centre d''aide","Communaute","Dashboard","Etudes de cas"]},{"title":"Legal","links":["CGU","Confidentialite","Cookies","Mentions legales","Conditions partenaires"]}]')
    ) AS seed("ComponentKey", "FieldKey", "JsonValue")
)
INSERT INTO "CmsContentValues" ("Id", "SectionId", "FieldKey", "ValueType", "LanguageId", "TextValue", "CreatedAt")
SELECT gen_random_uuid(), section."SectionId", seed."FieldKey", seed."ValueType", french."Id", seed."TextValue", now()
FROM text_seed seed
JOIN company_home_sections section ON section."ComponentKey" = seed."ComponentKey"
CROSS JOIN french
WHERE NOT EXISTS (
    SELECT 1
    FROM "CmsContentValues" existing
    WHERE existing."SectionId" = section."SectionId"
      AND existing."FieldKey" = seed."FieldKey"
      AND existing."LanguageId" = french."Id"
);

WITH french AS (
    SELECT "Id"
    FROM "Languages"
    WHERE "Code" = 'fr'
    LIMIT 1
),
company_home_sections AS (
    SELECT
        section."Id" AS "SectionId",
        component."Key" AS "ComponentKey"
    FROM "CmsSites" site
    JOIN "CmsPages" page ON page."SiteId" = site."Id"
    JOIN "CmsPageVersions" version ON version."PageId" = page."Id"
    JOIN "CmsSections" section ON section."PageVersionId" = version."Id"
    JOIN "CmsComponentDefinitions" component ON component."Id" = section."ComponentDefinitionId"
    WHERE site."Code" = 'company-public'
      AND page."Code" = 'home'
      AND version."VersionNumber" = (
          SELECT max(v2."VersionNumber")
          FROM "CmsPageVersions" v2
          WHERE v2."PageId" = page."Id"
      )
),
json_seed AS (
    SELECT *
    FROM (
        VALUES
            ('HeroStandard', 'proofItems', '["Inscription gratuite","Validation dossier","Portail entreprise"]'),
            ('StepsTimeline', 'steps', '[{"number":"01","label":"Compte","title":"Creez votre compte","text":"Renseignez votre entreprise, vos services et le contact responsable.","image":"images/kaza-how-step-1.png"},{"number":"02","label":"Verification","title":"Nous verifions votre dossier","text":"wélé controle les informations pour securiser les clients et les missions.","image":"images/kaza-how-step-2.png"},{"number":"03","label":"Portail","title":"Travaillez depuis votre portail","text":"Ajoutez vos prestataires, recevez des demandes et suivez vos interventions.","image":"images/kaza-how-step-3.png"}]'),
            ('TrustedLogos', 'items', '["Services verifies","Entreprises locales","Prestataires suivis","Paiements traces","Support partenaire"]'),
            ('DashboardPreview', 'stats', '[{"label":"Demandes","value":"12","help":"+4 cette semaine"},{"label":"Assignees","value":"8","help":"Equipe mobilisee"},{"label":"Paiements","value":"185k","help":"XOF suivis"}]'),
            ('DashboardPreview', 'requests', '["Menage a Cocody Riviera","Jardinage a Marcory","Nounou aux Deux Plateaux"]'),
            ('DashboardPreview', 'providers', '["Awa K. - Menage","Jean M. - Jardinage","Fatou C. - Nounou"]'),
            ('FaqAccordion', 'questions', '[{"question":"Comment sont verifiees les entreprises sur wélé ?","answer":"Nous verifions les informations de l''entreprise, les documents essentiels et le contact responsable avant l''activation complete."},{"question":"L''inscription est-elle gratuite ?","answer":"Oui. L''inscription est gratuite. wélé applique ensuite une commission uniquement sur les missions realisees."},{"question":"Puis-je refuser une demande client ?","answer":"Oui. Votre entreprise reste libre d''accepter les demandes qui correspondent a son equipe, sa zone et ses disponibilites."},{"question":"Qui choisit le prestataire ?","answer":"Vous pouvez affecter vous-meme un prestataire depuis le portail ou laisser wélé vous accompagner selon le mode choisi."},{"question":"Comment sont suivis les paiements ?","answer":"Le portail permet de suivre les paiements Mobile Money, les encaissements terrain et les commissions."},{"question":"Combien de temps prend la validation ?","answer":"Elle depend de la qualite du dossier. Plus les informations sont claires, plus la validation est rapide."}]'),
            ('ContactForm', 'tags', '["Abidjan","Services a domicile","Partenariat entreprise"]'),
            ('FooterLinks', 'columns', '[{"title":"Produit","links":["Plateforme","Fonctionnement","Tarifs","Securite","Integrations","Changelog"]},{"title":"Entreprise","links":["A propos","Blog","Carrieres","Presse","Partenaires"]},{"title":"Ressources","links":["Documentation","Centre d''aide","Communaute","Dashboard","Etudes de cas"]},{"title":"Legal","links":["CGU","Confidentialite","Cookies","Mentions legales","Conditions partenaires"]}]')
    ) AS seed("ComponentKey", "FieldKey", "JsonValue")
)
INSERT INTO "CmsContentValues" ("Id", "SectionId", "FieldKey", "ValueType", "LanguageId", "JsonValue", "CreatedAt")
SELECT gen_random_uuid(), section."SectionId", seed."FieldKey", 'Json', french."Id", seed."JsonValue"::jsonb, now()
FROM json_seed seed
JOIN company_home_sections section ON section."ComponentKey" = seed."ComponentKey"
CROSS JOIN french
WHERE NOT EXISTS (
    SELECT 1
    FROM "CmsContentValues" existing
    WHERE existing."SectionId" = section."SectionId"
      AND existing."FieldKey" = seed."FieldKey"
      AND existing."LanguageId" = french."Id"
);
