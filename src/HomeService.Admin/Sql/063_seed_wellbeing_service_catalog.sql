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
    "DisplayCategory",
    "CreatedAt")
SELECT gen_random_uuid(),
       seed."Name",
       seed."NormalizedName",
       seed."Description",
       seed."IconName",
       seed."NormalPriceAmount",
       seed."PremiumPriceAmount",
       'XOF',
       'Approved',
       true,
       seed."RequiresPortfolio",
       seed."MinimumPortfolioItems",
       false,
       false,
       false,
       false,
       'Wellbeing',
       now()
FROM (
    VALUES
        ('Manucure et pedicure', 'manucure et pedicure', 'Soins des mains, des pieds et mise en beaute des ongles.', 'hand', 5000, 10000, true, 3),
        ('Estheticienne', 'estheticienne', 'Soins esthetiques du visage et du corps realises a domicile.', 'sparkles', 10000, 25000, true, 3),
        ('Coiffure', 'coiffure', 'Coiffure femme et enfant, tresses, soins capillaires et pose de perruque.', 'scissors', 5000, 15000, true, 3),
        ('Barbier', 'barbier', 'Coupe homme, entretien de la barbe et soins de finition.', 'scissors', 3000, 8000, true, 3),
        ('Massage et bien-etre', 'massage et bien etre', 'Massages de detente et soins de bien-etre a domicile.', 'heart-pulse', 15000, 30000, true, 3),
        ('Maquillage professionnel', 'maquillage professionnel', 'Maquillage adapte aux sorties, ceremonies et evenements.', 'palette', 10000, 30000, true, 3)
) AS seed(
    "Name",
    "NormalizedName",
    "Description",
    "IconName",
    "NormalPriceAmount",
    "PremiumPriceAmount",
    "RequiresPortfolio",
    "MinimumPortfolioItems")
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
    "Currency" = 'XOF',
    "Status" = 'Approved',
    "IsActive" = true,
    "RequiresPortfolio" = seed."RequiresPortfolio",
    "MinimumPortfolioItems" = seed."MinimumPortfolioItems",
    "DisplayCategory" = 'Wellbeing'
FROM (
    VALUES
        ('manucure et pedicure', 'Soins des mains, des pieds et mise en beaute des ongles.', 'hand', 5000, 10000, true, 3),
        ('estheticienne', 'Soins esthetiques du visage et du corps realises a domicile.', 'sparkles', 10000, 25000, true, 3),
        ('coiffure', 'Coiffure femme et enfant, tresses, soins capillaires et pose de perruque.', 'scissors', 5000, 15000, true, 3),
        ('barbier', 'Coupe homme, entretien de la barbe et soins de finition.', 'scissors', 3000, 8000, true, 3),
        ('massage et bien etre', 'Massages de detente et soins de bien-etre a domicile.', 'heart-pulse', 15000, 30000, true, 3),
        ('maquillage professionnel', 'Maquillage adapte aux sorties, ceremonies et evenements.', 'palette', 10000, 30000, true, 3)
) AS seed(
    "NormalizedName",
    "Description",
    "IconName",
    "NormalPriceAmount",
    "PremiumPriceAmount",
    "RequiresPortfolio",
    "MinimumPortfolioItems")
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
       'XOF',
       true,
       now()
FROM "Services" service
JOIN (
    VALUES
        ('manucure et pedicure', 'Manucure classique', 'manucure classique', 'Soin des mains, limage et pose de vernis classique.', 10, 5000, 9000),
        ('manucure et pedicure', 'Pedicure classique', 'pedicure classique', 'Soin des pieds, limage et pose de vernis classique.', 20, 6000, 11000),
        ('manucure et pedicure', 'Vernis semi-permanent', 'vernis semi permanent', 'Preparation des ongles et pose de vernis semi-permanent.', 30, 8000, 15000),
        ('manucure et pedicure', 'Pose de faux ongles', 'pose de faux ongles', 'Pose complete avec finition simple.', 40, 12000, 25000),
        ('manucure et pedicure', 'Nail art', 'nail art', 'Decoration personnalisee des ongles.', 50, 3000, 12000),

        ('estheticienne', 'Soin du visage', 'soin du visage', 'Nettoyage, soin et hydratation du visage.', 10, 10000, 25000),
        ('estheticienne', 'Nettoyage de peau', 'nettoyage de peau', 'Nettoyage approfondi adapte au type de peau.', 20, 12000, 28000),
        ('estheticienne', 'Soin anti-acne', 'soin anti acne', 'Soin cosmetique adapte aux peaux a imperfections.', 30, 15000, 35000),
        ('estheticienne', 'Epilation du visage', 'epilation du visage', 'Epilation des zones du visage selectionnees.', 40, 5000, 12000),
        ('estheticienne', 'Epilation du corps', 'epilation du corps', 'Epilation d''une ou plusieurs zones du corps.', 50, 10000, 30000),

        ('coiffure', 'Tresses simples', 'tresses simples', 'Realisation de tresses simples sans meches.', 10, 5000, 12000),
        ('coiffure', 'Tresses avec meches', 'tresses avec meches', 'Realisation de tresses avec meches fournies ou a fournir.', 20, 10000, 30000),
        ('coiffure', 'Pose de perruque', 'pose de perruque', 'Preparation et pose simple d''une perruque.', 30, 10000, 25000),
        ('coiffure', 'Defrisage', 'defrisage', 'Application du produit et soin de finition.', 40, 7000, 15000),
        ('coiffure', 'Shampoing et brushing', 'shampoing et brushing', 'Shampoing, soin et mise en forme des cheveux.', 50, 5000, 12000),
        ('coiffure', 'Coiffure enfant', 'coiffure enfant', 'Coiffure simple adaptee aux enfants.', 60, 4000, 10000),

        ('barbier', 'Coupe homme', 'coupe homme', 'Coupe classique ou moderne selon le modele demande.', 10, 3000, 8000),
        ('barbier', 'Taille de barbe', 'taille de barbe', 'Taille, contours et finition de la barbe.', 20, 2000, 5000),
        ('barbier', 'Coupe et barbe', 'coupe et barbe', 'Forfait coupe homme et entretien de la barbe.', 30, 5000, 12000),
        ('barbier', 'Contours', 'contours', 'Finition des contours des cheveux et de la barbe.', 40, 1500, 4000),
        ('barbier', 'Soin de la barbe', 'soin de la barbe', 'Nettoyage, hydratation et mise en forme de la barbe.', 50, 4000, 10000),
        ('barbier', 'Coupe enfant', 'coupe enfant homme', 'Coupe simple pour garcon.', 60, 2500, 6000),

        ('massage et bien etre', 'Massage relaxant', 'massage relaxant', 'Massage de detente du corps.', 10, 15000, 35000),
        ('massage et bien etre', 'Massage du dos', 'massage du dos', 'Massage cible du dos et des epaules.', 20, 10000, 25000),
        ('massage et bien etre', 'Massage sportif', 'massage sportif', 'Massage musculaire avant ou apres un effort.', 30, 20000, 40000),
        ('massage et bien etre', 'Massage aux huiles', 'massage aux huiles', 'Massage relaxant realise avec des huiles adaptees.', 40, 18000, 40000),
        ('massage et bien etre', 'Reflexologie plantaire', 'reflexologie plantaire', 'Massage et stimulation des zones des pieds.', 50, 12000, 30000),

        ('maquillage professionnel', 'Maquillage de jour', 'maquillage de jour', 'Maquillage naturel adapte a la journee.', 10, 10000, 25000),
        ('maquillage professionnel', 'Maquillage de soiree', 'maquillage de soiree', 'Maquillage soutenu pour sortie ou evenement.', 20, 15000, 35000),
        ('maquillage professionnel', 'Maquillage de mariage', 'maquillage de mariage', 'Maquillage longue tenue pour la mariee.', 30, 30000, 80000),
        ('maquillage professionnel', 'Pose de faux cils', 'pose de faux cils', 'Pose de faux cils adaptee au rendu souhaite.', 40, 5000, 15000),
        ('maquillage professionnel', 'Cours d''auto-maquillage', 'cours d auto maquillage', 'Accompagnement personnalise pour apprendre a se maquiller.', 50, 20000, 50000)
) AS seed(
    "ServiceNormalizedName",
    "Name",
    "NormalizedName",
    "Description",
    "SortOrder",
    "NormalPriceAmount",
    "PremiumPriceAmount")
    ON service."NormalizedName" = seed."ServiceNormalizedName"
WHERE NOT EXISTS (
    SELECT 1
    FROM "ServicePrestations" existing
    WHERE existing."ServiceId" = service."Id"
      AND existing."NormalizedName" = seed."NormalizedName"
);

UPDATE "ServicePrestations" AS prestation
SET
    "Name" = seed."Name",
    "Description" = seed."Description",
    "SortOrder" = seed."SortOrder",
    "NormalPriceAmount" = seed."NormalPriceAmount",
    "PremiumPriceAmount" = seed."PremiumPriceAmount",
    "Currency" = 'XOF',
    "IsActive" = true
FROM "Services" service
JOIN (
    VALUES
        ('manucure et pedicure', 'Manucure classique', 'manucure classique', 'Soin des mains, limage et pose de vernis classique.', 10, 5000, 9000),
        ('manucure et pedicure', 'Pedicure classique', 'pedicure classique', 'Soin des pieds, limage et pose de vernis classique.', 20, 6000, 11000),
        ('manucure et pedicure', 'Vernis semi-permanent', 'vernis semi permanent', 'Preparation des ongles et pose de vernis semi-permanent.', 30, 8000, 15000),
        ('manucure et pedicure', 'Pose de faux ongles', 'pose de faux ongles', 'Pose complete avec finition simple.', 40, 12000, 25000),
        ('manucure et pedicure', 'Nail art', 'nail art', 'Decoration personnalisee des ongles.', 50, 3000, 12000),
        ('estheticienne', 'Soin du visage', 'soin du visage', 'Nettoyage, soin et hydratation du visage.', 10, 10000, 25000),
        ('estheticienne', 'Nettoyage de peau', 'nettoyage de peau', 'Nettoyage approfondi adapte au type de peau.', 20, 12000, 28000),
        ('estheticienne', 'Soin anti-acne', 'soin anti acne', 'Soin cosmetique adapte aux peaux a imperfections.', 30, 15000, 35000),
        ('estheticienne', 'Epilation du visage', 'epilation du visage', 'Epilation des zones du visage selectionnees.', 40, 5000, 12000),
        ('estheticienne', 'Epilation du corps', 'epilation du corps', 'Epilation d''une ou plusieurs zones du corps.', 50, 10000, 30000),
        ('coiffure', 'Tresses simples', 'tresses simples', 'Realisation de tresses simples sans meches.', 10, 5000, 12000),
        ('coiffure', 'Tresses avec meches', 'tresses avec meches', 'Realisation de tresses avec meches fournies ou a fournir.', 20, 10000, 30000),
        ('coiffure', 'Pose de perruque', 'pose de perruque', 'Preparation et pose simple d''une perruque.', 30, 10000, 25000),
        ('coiffure', 'Defrisage', 'defrisage', 'Application du produit et soin de finition.', 40, 7000, 15000),
        ('coiffure', 'Shampoing et brushing', 'shampoing et brushing', 'Shampoing, soin et mise en forme des cheveux.', 50, 5000, 12000),
        ('coiffure', 'Coiffure enfant', 'coiffure enfant', 'Coiffure simple adaptee aux enfants.', 60, 4000, 10000),
        ('barbier', 'Coupe homme', 'coupe homme', 'Coupe classique ou moderne selon le modele demande.', 10, 3000, 8000),
        ('barbier', 'Taille de barbe', 'taille de barbe', 'Taille, contours et finition de la barbe.', 20, 2000, 5000),
        ('barbier', 'Coupe et barbe', 'coupe et barbe', 'Forfait coupe homme et entretien de la barbe.', 30, 5000, 12000),
        ('barbier', 'Contours', 'contours', 'Finition des contours des cheveux et de la barbe.', 40, 1500, 4000),
        ('barbier', 'Soin de la barbe', 'soin de la barbe', 'Nettoyage, hydratation et mise en forme de la barbe.', 50, 4000, 10000),
        ('barbier', 'Coupe enfant', 'coupe enfant homme', 'Coupe simple pour garcon.', 60, 2500, 6000),
        ('massage et bien etre', 'Massage relaxant', 'massage relaxant', 'Massage de detente du corps.', 10, 15000, 35000),
        ('massage et bien etre', 'Massage du dos', 'massage du dos', 'Massage cible du dos et des epaules.', 20, 10000, 25000),
        ('massage et bien etre', 'Massage sportif', 'massage sportif', 'Massage musculaire avant ou apres un effort.', 30, 20000, 40000),
        ('massage et bien etre', 'Massage aux huiles', 'massage aux huiles', 'Massage relaxant realise avec des huiles adaptees.', 40, 18000, 40000),
        ('massage et bien etre', 'Reflexologie plantaire', 'reflexologie plantaire', 'Massage et stimulation des zones des pieds.', 50, 12000, 30000),
        ('maquillage professionnel', 'Maquillage de jour', 'maquillage de jour', 'Maquillage naturel adapte a la journee.', 10, 10000, 25000),
        ('maquillage professionnel', 'Maquillage de soiree', 'maquillage de soiree', 'Maquillage soutenu pour sortie ou evenement.', 20, 15000, 35000),
        ('maquillage professionnel', 'Maquillage de mariage', 'maquillage de mariage', 'Maquillage longue tenue pour la mariee.', 30, 30000, 80000),
        ('maquillage professionnel', 'Pose de faux cils', 'pose de faux cils', 'Pose de faux cils adaptee au rendu souhaite.', 40, 5000, 15000),
        ('maquillage professionnel', 'Cours d''auto-maquillage', 'cours d auto maquillage', 'Accompagnement personnalise pour apprendre a se maquiller.', 50, 20000, 50000)
) AS seed(
    "ServiceNormalizedName",
    "Name",
    "PrestationNormalizedName",
    "Description",
    "SortOrder",
    "NormalPriceAmount",
    "PremiumPriceAmount")
    ON service."NormalizedName" = seed."ServiceNormalizedName"
WHERE prestation."ServiceId" = service."Id"
  AND prestation."NormalizedName" = seed."PrestationNormalizedName";
