INSERT INTO "Services" (
    "Id",
    "Name",
    "NormalizedName",
    "Description",
    "IconName",
    "NormalPriceAmount",
    "PremiumPriceAmount",
    "Currency",
    "Status",
    "IsActive",
    "RequiresPortfolio",
    "MinimumPortfolioItems",
    "RequiresCompletionPhoto",
    "RequiresBeforeAfterPhotos",
    "RequiresDiploma",
    "RequiresAdminApprovalBeforeAssignment",
    "CreatedAt")
SELECT gen_random_uuid(),
       seed."Name",
       seed."NormalizedName",
       seed."Description",
       seed."IconName",
       seed."NormalPriceAmount",
       seed."PremiumPriceAmount",
       seed."Currency",
       'Approved',
       true,
       false,
       0,
       false,
       false,
       false,
       false,
       now()
FROM (
    VALUES
        ('Menage a domicile', 'menage a domicile', 'Entretien courant du domicile, nettoyage, rangement et aide ponctuelle.', 'sparkles', 3500, 5000, 'XOF'),
        ('Jardinage', 'jardinage', 'Entretien jardin, taille simple, arrosage et travaux exterieurs legers.', 'sprout', 4500, 6500, 'XOF'),
        ('Electricite', 'electricite', 'Petites interventions electriques, diagnostic simple et remise en service.', 'zap', 5000, 8000, 'XOF'),
        ('Blanchisserie', 'blanchisserie', 'Lavage, repassage et entretien du linge pour particuliers et familles.', 'shirt', 2500, 4500, 'XOF'),
        ('Depannage auto', 'depannage auto', 'Assistance auto de proximite pour les urgences simples et depannages courants.', 'car', 7000, 12000, 'XOF'),
        ('Nounou', 'nounou', 'Garde d''enfant a domicile par un prestataire recommande et rattache a une entreprise validee.', 'baby', 4000, 6500, 'XOF')
) AS seed("Name", "NormalizedName", "Description", "IconName", "NormalPriceAmount", "PremiumPriceAmount", "Currency")
WHERE NOT EXISTS (
    SELECT 1
    FROM "Services" service
    WHERE service."NormalizedName" = seed."NormalizedName"
);

UPDATE "Services" AS service
SET
    "Description" = seed."Description",
    "IconName" = seed."IconName",
    "NormalPriceAmount" = seed."NormalPriceAmount",
    "PremiumPriceAmount" = seed."PremiumPriceAmount",
    "Currency" = seed."Currency",
    "IsActive" = true
FROM (
    VALUES
        ('menage a domicile', 'Entretien courant du domicile, nettoyage, rangement et aide ponctuelle.', 'sparkles', 3500, 5000, 'XOF'),
        ('jardinage', 'Entretien jardin, taille simple, arrosage et travaux exterieurs legers.', 'sprout', 4500, 6500, 'XOF'),
        ('electricite', 'Petites interventions electriques, diagnostic simple et remise en service.', 'zap', 5000, 8000, 'XOF'),
        ('blanchisserie', 'Lavage, repassage et entretien du linge pour particuliers et familles.', 'shirt', 2500, 4500, 'XOF'),
        ('depannage auto', 'Assistance auto de proximite pour les urgences simples et depannages courants.', 'car', 7000, 12000, 'XOF'),
        ('nounou', 'Garde d''enfant a domicile par un prestataire recommande et rattache a une entreprise validee.', 'baby', 4000, 6500, 'XOF')
) AS seed("NormalizedName", "Description", "IconName", "NormalPriceAmount", "PremiumPriceAmount", "Currency")
WHERE service."NormalizedName" = seed."NormalizedName";

INSERT INTO "ServicePrestations" (
    "Id",
    "ServiceId",
    "Name",
    "NormalizedName",
    "Description",
    "SortOrder",
    "NormalPriceAmount",
    "PremiumPriceAmount",
    "Currency",
    "IsActive",
    "CreatedAt")
SELECT gen_random_uuid(),
       service."Id",
       seed."Name",
       seed."NormalizedName",
       seed."Description",
       seed."SortOrder",
       seed."NormalPriceAmount",
       seed."PremiumPriceAmount",
       seed."Currency",
       true,
       now()
FROM "Services" service
JOIN (
    VALUES
        ('jardinage', 'Tondre le gazon', 'tondre le gazon', 'Coupe et entretien simple de pelouse.', 10, 4500, 6500, 'XOF'),
        ('jardinage', 'Tailler une haie', 'tailler une haie', 'Taille legere et remise en forme des haies.', 20, 5500, 7500, 'XOF'),
        ('jardinage', 'Desherbage', 'desherbage', 'Nettoyage des mauvaises herbes sur les zones indiquees.', 30, 3500, 5000, 'XOF'),
        ('jardinage', 'Arrosage et entretien plantes', 'arrosage et entretien plantes', 'Arrosage, controle visuel et entretien leger des plantes.', 40, 3000, 4500, 'XOF'),
        ('jardinage', 'Ramassage feuilles', 'ramassage feuilles', 'Ramassage des feuilles et nettoyage leger des allees.', 50, 3000, 4500, 'XOF'),
        ('jardinage', 'Nettoyage terrasse exterieure', 'nettoyage terrasse exterieure', 'Balayage et nettoyage simple de terrasse ou cour.', 60, 4500, 6500, 'XOF'),
        ('menage a domicile', 'Menage regulier', 'menage regulier', 'Entretien courant du domicile.', 10, 3500, 5000, 'XOF'),
        ('menage a domicile', 'Grand nettoyage', 'grand nettoyage', 'Nettoyage complet d''un logement ou d''une grande piece.', 15, 6000, 8500, 'XOF'),
        ('menage a domicile', 'Nettoyage apres travaux', 'nettoyage apres travaux', 'Nettoyage renforce apres petits travaux ou renovation.', 20, 5000, 7000, 'XOF'),
        ('menage a domicile', 'Nettoyage vitres', 'nettoyage vitres', 'Nettoyage simple des vitres accessibles.', 30, 3000, 4500, 'XOF'),
        ('menage a domicile', 'Nettoyage cuisine', 'nettoyage cuisine', 'Nettoyage detaille de cuisine, plans de travail et surfaces.', 40, 4000, 6000, 'XOF'),
        ('menage a domicile', 'Nettoyage sanitaires', 'nettoyage sanitaires', 'Nettoyage detaille salle d''eau, WC et surfaces sanitaires.', 50, 4000, 6000, 'XOF'),
        ('nounou', 'Garde ponctuelle', 'garde ponctuelle', 'Garde d''enfant sur une plage horaire courte.', 10, 4000, 6500, 'XOF'),
        ('nounou', 'Garde apres ecole', 'garde apres ecole', 'Presence et accompagnement apres l''ecole.', 20, 4500, 7000, 'XOF'),
        ('electricite', 'Diagnostic panne electrique', 'diagnostic panne electrique', 'Recherche simple de panne et conseil d''intervention.', 10, 6000, 9000, 'XOF'),
        ('electricite', 'Remplacement prise ou interrupteur', 'remplacement prise ou interrupteur', 'Remplacement d''une prise, interrupteur ou point simple.', 20, 5000, 7500, 'XOF'),
        ('electricite', 'Installation luminaire', 'installation luminaire', 'Pose ou remplacement d''un luminaire existant.', 30, 6000, 9000, 'XOF'),
        ('electricite', 'Remise en service disjoncteur', 'remise en service disjoncteur', 'Controle et remise en service simple apres coupure.', 40, 5000, 8000, 'XOF'),
        ('electricite', 'Depannage court-circuit simple', 'depannage court-circuit simple', 'Intervention sur panne courte et localisee.', 50, 8000, 12000, 'XOF'),
        ('electricite', 'Installation ventilateur plafond', 'installation ventilateur plafond', 'Pose simple d''un ventilateur sur attente electrique existante.', 60, 10000, 15000, 'XOF'),
        ('blanchisserie', 'Lavage et pliage', 'lavage et pliage', 'Lavage, sechage et pliage du linge courant.', 10, 2500, 4000, 'XOF'),
        ('blanchisserie', 'Repassage', 'repassage', 'Repassage de vetements courants.', 20, 3000, 4500, 'XOF'),
        ('blanchisserie', 'Linge de maison', 'linge de maison', 'Entretien draps, serviettes et linge de maison.', 30, 3500, 5500, 'XOF'),
        ('blanchisserie', 'Pressing tenue', 'pressing tenue', 'Entretien de tenue, robe, chemise ou costume selon disponibilite.', 40, 5000, 8000, 'XOF'),
        ('blanchisserie', 'Detache simple', 'detache simple', 'Traitement simple de tache avant lavage.', 50, 3000, 5000, 'XOF'),
        ('depannage auto', 'Changement batterie', 'changement batterie', 'Remplacement ou assistance batterie sur place.', 10, 7000, 12000, 'XOF'),
        ('depannage auto', 'Aide crevaison', 'aide crevaison', 'Aide au changement de roue ou pose de roue de secours.', 20, 6000, 10000, 'XOF'),
        ('depannage auto', 'Demarrage avec cables', 'demarrage avec cables', 'Assistance demarrage avec cables ou booster.', 30, 6000, 9000, 'XOF'),
        ('depannage auto', 'Diagnostic panne demarrage', 'diagnostic panne demarrage', 'Controle simple quand le vehicule ne demarre pas.', 40, 8000, 12000, 'XOF'),
        ('depannage auto', 'Carburant urgence', 'carburant urgence', 'Assistance en cas de panne seche dans la zone couverte.', 50, 6000, 10000, 'XOF'),
        ('depannage auto', 'Remorquage partenaire', 'remorquage partenaire', 'Mise en relation ou assistance remorquage selon disponibilite.', 60, 15000, 25000, 'XOF')
) AS seed("ServiceNormalizedName", "Name", "NormalizedName", "Description", "SortOrder", "NormalPriceAmount", "PremiumPriceAmount", "Currency")
    ON service."NormalizedName" = seed."ServiceNormalizedName"
WHERE NOT EXISTS (
    SELECT 1
    FROM "ServicePrestations" existing
    WHERE existing."ServiceId" = service."Id"
      AND existing."NormalizedName" = seed."NormalizedName"
);

UPDATE "ServicePrestations" AS prestation
SET
    "Description" = seed."Description",
    "SortOrder" = seed."SortOrder",
    "NormalPriceAmount" = seed."NormalPriceAmount",
    "PremiumPriceAmount" = seed."PremiumPriceAmount",
    "Currency" = seed."Currency",
    "IsActive" = true
FROM "Services" service
JOIN (
    VALUES
        ('jardinage', 'tondre le gazon', 'Coupe et entretien simple de pelouse.', 10, 4500, 6500, 'XOF'),
        ('jardinage', 'tailler une haie', 'Taille legere et remise en forme des haies.', 20, 5500, 7500, 'XOF'),
        ('jardinage', 'desherbage', 'Nettoyage des mauvaises herbes sur les zones indiquees.', 30, 3500, 5000, 'XOF'),
        ('jardinage', 'arrosage et entretien plantes', 'Arrosage, controle visuel et entretien leger des plantes.', 40, 3000, 4500, 'XOF'),
        ('jardinage', 'ramassage feuilles', 'Ramassage des feuilles et nettoyage leger des allees.', 50, 3000, 4500, 'XOF'),
        ('jardinage', 'nettoyage terrasse exterieure', 'Balayage et nettoyage simple de terrasse ou cour.', 60, 4500, 6500, 'XOF'),
        ('menage a domicile', 'menage regulier', 'Entretien courant du domicile.', 10, 3500, 5000, 'XOF'),
        ('menage a domicile', 'grand nettoyage', 'Nettoyage complet d''un logement ou d''une grande piece.', 15, 6000, 8500, 'XOF'),
        ('menage a domicile', 'nettoyage apres travaux', 'Nettoyage renforce apres petits travaux ou renovation.', 20, 5000, 7000, 'XOF'),
        ('menage a domicile', 'nettoyage vitres', 'Nettoyage simple des vitres accessibles.', 30, 3000, 4500, 'XOF'),
        ('menage a domicile', 'nettoyage cuisine', 'Nettoyage detaille de cuisine, plans de travail et surfaces.', 40, 4000, 6000, 'XOF'),
        ('menage a domicile', 'nettoyage sanitaires', 'Nettoyage detaille salle d''eau, WC et surfaces sanitaires.', 50, 4000, 6000, 'XOF'),
        ('nounou', 'garde ponctuelle', 'Garde d''enfant sur une plage horaire courte.', 10, 4000, 6500, 'XOF'),
        ('nounou', 'garde apres ecole', 'Presence et accompagnement apres l''ecole.', 20, 4500, 7000, 'XOF'),
        ('electricite', 'diagnostic panne electrique', 'Recherche simple de panne et conseil d''intervention.', 10, 6000, 9000, 'XOF'),
        ('electricite', 'remplacement prise ou interrupteur', 'Remplacement d''une prise, interrupteur ou point simple.', 20, 5000, 7500, 'XOF'),
        ('electricite', 'installation luminaire', 'Pose ou remplacement d''un luminaire existant.', 30, 6000, 9000, 'XOF'),
        ('electricite', 'remise en service disjoncteur', 'Controle et remise en service simple apres coupure.', 40, 5000, 8000, 'XOF'),
        ('electricite', 'depannage court-circuit simple', 'Intervention sur panne courte et localisee.', 50, 8000, 12000, 'XOF'),
        ('electricite', 'installation ventilateur plafond', 'Pose simple d''un ventilateur sur attente electrique existante.', 60, 10000, 15000, 'XOF'),
        ('blanchisserie', 'lavage et pliage', 'Lavage, sechage et pliage du linge courant.', 10, 2500, 4000, 'XOF'),
        ('blanchisserie', 'repassage', 'Repassage de vetements courants.', 20, 3000, 4500, 'XOF'),
        ('blanchisserie', 'linge de maison', 'Entretien draps, serviettes et linge de maison.', 30, 3500, 5500, 'XOF'),
        ('blanchisserie', 'pressing tenue', 'Entretien de tenue, robe, chemise ou costume selon disponibilite.', 40, 5000, 8000, 'XOF'),
        ('blanchisserie', 'detache simple', 'Traitement simple de tache avant lavage.', 50, 3000, 5000, 'XOF'),
        ('depannage auto', 'changement batterie', 'Remplacement ou assistance batterie sur place.', 10, 7000, 12000, 'XOF'),
        ('depannage auto', 'aide crevaison', 'Aide au changement de roue ou pose de roue de secours.', 20, 6000, 10000, 'XOF'),
        ('depannage auto', 'demarrage avec cables', 'Assistance demarrage avec cables ou booster.', 30, 6000, 9000, 'XOF'),
        ('depannage auto', 'diagnostic panne demarrage', 'Controle simple quand le vehicule ne demarre pas.', 40, 8000, 12000, 'XOF'),
        ('depannage auto', 'carburant urgence', 'Assistance en cas de panne seche dans la zone couverte.', 50, 6000, 10000, 'XOF'),
        ('depannage auto', 'remorquage partenaire', 'Mise en relation ou assistance remorquage selon disponibilite.', 60, 15000, 25000, 'XOF')
) AS seed("ServiceNormalizedName", "PrestationNormalizedName", "Description", "SortOrder", "NormalPriceAmount", "PremiumPriceAmount", "Currency")
    ON service."NormalizedName" = seed."ServiceNormalizedName"
WHERE prestation."ServiceId" = service."Id"
  AND prestation."NormalizedName" = seed."PrestationNormalizedName";
