UPDATE "Services"
SET "IconUrl" = CASE
    WHEN "NormalizedName" IN ('menage', 'menage a domicile', 'nettoyage') THEN '/assets/services/menage.png'
    WHEN "NormalizedName" = 'jardinage' THEN '/assets/services/jardinage.png'
    WHEN "NormalizedName" = 'electricite' THEN '/assets/services/electricite.png'
    WHEN "NormalizedName" IN ('blanchisserie', 'pressing', 'repassage') THEN '/assets/services/blanchisserie.png'
    WHEN "NormalizedName" IN ('depannage auto', 'assistance auto') THEN '/assets/services/depannage-auto.png'
    WHEN "NormalizedName" IN ('nounou', 'garde enfants', 'garde d enfant') THEN '/assets/services/nounou.png'
    WHEN "NormalizedName" = 'plomberie' THEN '/assets/services/plomberie.png'
    WHEN "NormalizedName" = 'climatisation' THEN '/assets/services/climatisation.png'
    WHEN "NormalizedName" = 'serrurerie' THEN '/assets/services/serrurerie.png'
    WHEN "NormalizedName" = 'peinture' THEN '/assets/services/peinture.png'
    WHEN "NormalizedName" IN ('anti nuisibles', 'anti-nuisibles') THEN '/assets/services/anti-nuisibles.png'
    WHEN "NormalizedName" = 'electromenager' THEN '/assets/services/electromenager.png'
    ELSE "IconUrl"
END
WHERE "NormalizedName" IN (
    'menage',
    'menage a domicile',
    'nettoyage',
    'jardinage',
    'electricite',
    'blanchisserie',
    'pressing',
    'repassage',
    'depannage auto',
    'assistance auto',
    'nounou',
    'garde enfants',
    'garde d enfant',
    'plomberie',
    'climatisation',
    'serrurerie',
    'peinture',
    'anti nuisibles',
    'anti-nuisibles',
    'electromenager'
);
