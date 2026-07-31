WITH media("NormalizedName", "IllustrationUrl") AS (
    VALUES
        ('tondre le gazon', '/catalog/prestations/tondre-le-gazon.jpg'),
        ('tailler une haie', '/catalog/prestations/tailler-une-haie.jpg'),
        ('desherbage', '/catalog/prestations/desherbage.jpg'),
        ('arrosage et entretien plantes', '/catalog/prestations/arrosage-entretien-plantes.jpg'),
        ('ramassage feuilles', '/catalog/prestations/ramassage-feuilles.jpg'),
        ('nettoyage terrasse exterieure', '/catalog/prestations/nettoyage-terrasse-exterieure.jpg'),
        ('menage regulier', '/catalog/prestations/menage-regulier.jpg'),
        ('grand nettoyage', '/catalog/prestations/grand-nettoyage.jpg'),
        ('nettoyage apres travaux', '/catalog/prestations/nettoyage-apres-travaux.jpg'),
        ('nettoyage vitres', '/catalog/prestations/nettoyage-vitres.jpg'),
        ('nettoyage cuisine', '/catalog/prestations/nettoyage-cuisine.jpg'),
        ('nettoyage sanitaires', '/catalog/prestations/nettoyage-sanitaires.jpg'),
        ('garde ponctuelle', '/catalog/prestations/garde-ponctuelle.jpg'),
        ('garde apres ecole', '/catalog/prestations/garde-apres-ecole.jpg'),
        ('lavage et pliage', '/catalog/prestations/lavage-et-pliage.jpg'),
        ('repassage', '/catalog/prestations/repassage.jpg'),
        ('linge de maison', '/catalog/prestations/linge-de-maison.jpg'),
        ('pressing tenue', '/catalog/prestations/pressing-tenue.jpg'),
        ('detache simple', '/catalog/prestations/detache-simple.jpg'),
        ('diagnostic panne electrique', '/catalog/prestations/diagnostic-panne-electrique.jpg'),
        ('remplacement prise ou interrupteur', '/catalog/prestations/remplacement-prise-interrupteur.jpg'),
        ('installation luminaire', '/catalog/prestations/installation-luminaire.jpg'),
        ('remise en service disjoncteur', '/catalog/prestations/remise-en-service-disjoncteur.jpg'),
        ('depannage court circuit simple', '/catalog/prestations/depannage-court-circuit-simple.jpg'),
        ('installation ventilateur plafond', '/catalog/prestations/installation-ventilateur-plafond.jpg'),
        ('deboucher un evier', '/catalog/prestations/deboucher-evier.jpg'),
        ('reparer une fuite', '/catalog/prestations/reparer-fuite.jpg'),
        ('deboucher un wc', '/catalog/prestations/deboucher-wc.jpg'),
        ('installer un equipement sanitaire', '/catalog/prestations/installer-equipement-sanitaire.jpg'),
        ('reparer un chauffe eau', '/catalog/prestations/reparer-chauffe-eau.jpg'),
        ('remplacer un robinet', '/catalog/prestations/remplacer-robinet.jpg'),
        ('changement batterie', '/catalog/prestations/changement-batterie.jpg'),
        ('aide crevaison', '/catalog/prestations/aide-crevaison.jpg'),
        ('demarrage avec cables', '/catalog/prestations/demarrage-avec-cables.jpg'),
        ('diagnostic panne demarrage', '/catalog/prestations/diagnostic-panne-demarrage.jpg'),
        ('carburant urgence', '/catalog/prestations/carburant-urgence.jpg'),
        ('remorquage partenaire', '/catalog/prestations/remorquage-partenaire.jpg'),
        ('manucure classique', '/catalog/prestations/manucure-classique.jpg'),
        ('pedicure classique', '/catalog/prestations/pedicure-classique.jpg'),
        ('vernis semi permanent', '/catalog/prestations/vernis-semi-permanent.jpg'),
        ('pose de faux ongles', '/catalog/prestations/pose-faux-ongles.jpg'),
        ('nail art', '/catalog/prestations/nail-art.jpg'),
        ('soin du visage', '/catalog/prestations/soin-visage.jpg'),
        ('nettoyage de peau', '/catalog/prestations/nettoyage-peau.jpg'),
        ('soin anti acne', '/catalog/prestations/soin-anti-acne.jpg'),
        ('epilation du visage', '/catalog/prestations/epilation-visage.jpg'),
        ('epilation du corps', '/catalog/prestations/epilation-corps.jpg'),
        ('tresses simples', '/catalog/prestations/tresses-simples.jpg'),
        ('tresses avec meches', '/catalog/prestations/tresses-avec-meches.jpg'),
        ('pose de perruque', '/catalog/prestations/pose-perruque.jpg'),
        ('defrisage', '/catalog/prestations/defrisage.jpg'),
        ('shampoing et brushing', '/catalog/prestations/shampoing-brushing.jpg'),
        ('coiffure enfant', '/catalog/prestations/coiffure-enfant.jpg'),
        ('coupe homme', '/catalog/prestations/coupe-homme.jpg'),
        ('taille de barbe', '/catalog/prestations/taille-barbe.jpg'),
        ('coupe et barbe', '/catalog/prestations/coupe-et-barbe.jpg'),
        ('contours', '/catalog/prestations/contours.jpg'),
        ('soin de la barbe', '/catalog/prestations/soin-barbe.jpg'),
        ('coupe enfant homme', '/catalog/prestations/coupe-enfant.jpg'),
        ('massage relaxant', '/catalog/prestations/massage-relaxant.jpg'),
        ('massage du dos', '/catalog/prestations/massage-dos.jpg'),
        ('massage sportif', '/catalog/prestations/massage-sportif.jpg'),
        ('massage aux huiles', '/catalog/prestations/massage-huiles.jpg'),
        ('reflexologie plantaire', '/catalog/prestations/reflexologie-plantaire.jpg'),
        ('maquillage de jour', '/catalog/prestations/maquillage-jour.jpg'),
        ('maquillage de soiree', '/catalog/prestations/maquillage-soiree.jpg'),
        ('maquillage de mariage', '/catalog/prestations/maquillage-mariage.jpg'),
        ('pose de faux cils', '/catalog/prestations/pose-faux-cils.jpg'),
        ('cours d auto maquillage', '/catalog/prestations/cours-auto-maquillage.jpg')
)
UPDATE "ServicePrestations" AS prestation
SET "IllustrationUrl" = media."IllustrationUrl"
FROM media
WHERE prestation."NormalizedName" = media."NormalizedName"
  AND COALESCE(BTRIM(prestation."IllustrationUrl"), '') = '';

UPDATE "ServicePrestations" AS prestation
SET "IllustrationUrl" = '/catalog/prestations/coupe-enfant.jpg'
FROM "Services" AS service
WHERE prestation."ServiceId" = service."Id"
  AND service."NormalizedName" = 'barbier'
  AND LOWER(BTRIM(prestation."Name")) = 'coupe enfant'
  AND COALESCE(BTRIM(prestation."IllustrationUrl"), '') = '';
