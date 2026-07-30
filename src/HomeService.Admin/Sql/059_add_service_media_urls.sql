ALTER TABLE "Services"
    ADD COLUMN IF NOT EXISTS "IconUrl" character varying(600),
    ADD COLUMN IF NOT EXISTS "ImageUrl" character varying(600);

UPDATE "Services"
SET "IconUrl" = CASE
    WHEN "NormalizedName" IN ('menage', 'menage a domicile', 'nettoyage') THEN '/assets/services/menage.svg'
    WHEN "NormalizedName" = 'jardinage' THEN '/assets/services/jardinage.svg'
    WHEN "NormalizedName" = 'electricite' THEN '/assets/services/electricite.svg'
    WHEN "NormalizedName" IN ('blanchisserie', 'pressing', 'repassage') THEN '/assets/services/blanchisserie.svg'
    WHEN "NormalizedName" IN ('depannage auto', 'assistance auto') THEN '/assets/services/depannage-auto.svg'
    WHEN "NormalizedName" IN ('nounou', 'garde enfants', 'garde d enfant') THEN '/assets/services/nounou.svg'
    WHEN "NormalizedName" = 'plomberie' THEN '/assets/services/plomberie.svg'
    WHEN "NormalizedName" = 'climatisation' THEN '/assets/services/climatisation.svg'
    WHEN "NormalizedName" = 'serrurerie' THEN '/assets/services/serrurerie.svg'
    WHEN "NormalizedName" = 'peinture' THEN '/assets/services/peinture.svg'
    WHEN "NormalizedName" IN ('anti nuisibles', 'anti-nuisibles') THEN '/assets/services/anti-nuisibles.svg'
    WHEN "NormalizedName" = 'electromenager' THEN '/assets/services/electromenager.svg'
    ELSE "IconUrl"
END
WHERE "IconUrl" IS NULL
   OR trim("IconUrl") = '';
