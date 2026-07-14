ALTER TABLE "Services"
ADD COLUMN IF NOT EXISTS "IconName" character varying(80) NOT NULL DEFAULT 'sparkles';

UPDATE "Services"
SET "IconName" = CASE
    WHEN "NormalizedName" IN ('menage a domicile', 'menage', 'nettoyage') THEN 'sparkles'
    WHEN "NormalizedName" = 'jardinage' THEN 'sprout'
    WHEN "NormalizedName" = 'nounou' THEN 'baby'
    WHEN "NormalizedName" IN ('coiffure', 'beaute', 'esthetique') THEN 'scissors'
    WHEN "NormalizedName" = 'plomberie' THEN 'droplets'
    WHEN "NormalizedName" = 'electricite' THEN 'zap'
    ELSE COALESCE(NULLIF("IconName", ''), 'sparkles')
END;
