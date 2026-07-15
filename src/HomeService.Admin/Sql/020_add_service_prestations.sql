CREATE TABLE IF NOT EXISTS "ServicePrestations" (
    "Id" uuid NOT NULL,
    "ServiceId" uuid NOT NULL,
    "Name" character varying(140) NOT NULL,
    "NormalizedName" character varying(140) NOT NULL,
    "Description" character varying(800),
    "SortOrder" integer NOT NULL DEFAULT 0,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_ServicePrestations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ServicePrestations_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ServicePrestations_ServiceId_IsActive_SortOrder"
    ON "ServicePrestations" ("ServiceId", "IsActive", "SortOrder");

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ServicePrestations_ServiceId_NormalizedName"
    ON "ServicePrestations" ("ServiceId", "NormalizedName");

INSERT INTO "ServicePrestations" ("Id", "ServiceId", "Name", "NormalizedName", "Description", "SortOrder", "IsActive", "CreatedAt")
SELECT gen_random_uuid(), service."Id", seed."Name", seed."NormalizedName", seed."Description", seed."SortOrder", true, now()
FROM "Services" service
JOIN (
    VALUES
        ('jardinage', 'Tondre le gazon', 'tondre le gazon', 'Coupe et entretien simple de pelouse.', 10),
        ('jardinage', 'Tailler une haie', 'tailler une haie', 'Taille legere et remise en forme des haies.', 20),
        ('jardinage', 'Desherbage', 'desherbage', 'Nettoyage des mauvaises herbes sur les zones indiquees.', 30),
        ('menage a domicile', 'Menage regulier', 'menage regulier', 'Entretien courant du domicile.', 10),
        ('menage a domicile', 'Nettoyage apres travaux', 'nettoyage apres travaux', 'Nettoyage renforce apres petits travaux ou renovation.', 20),
        ('menage a domicile', 'Nettoyage vitres', 'nettoyage vitres', 'Nettoyage simple des vitres accessibles.', 30),
        ('nounou', 'Garde ponctuelle', 'garde ponctuelle', 'Garde d''enfant sur une plage horaire courte.', 10),
        ('nounou', 'Garde apres ecole', 'garde apres ecole', 'Presence et accompagnement apres l''ecole.', 20)
) AS seed("ServiceNormalizedName", "Name", "NormalizedName", "Description", "SortOrder")
    ON service."NormalizedName" = seed."ServiceNormalizedName"
WHERE NOT EXISTS (
    SELECT 1
    FROM "ServicePrestations" existing
    WHERE existing."ServiceId" = service."Id"
      AND existing."NormalizedName" = seed."NormalizedName"
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260715083555_AddServicePrestations', '9.0.0'
WHERE EXISTS (SELECT 1 FROM "__EFMigrationsHistory")
  AND NOT EXISTS (
      SELECT 1 FROM "__EFMigrationsHistory"
      WHERE "MigrationId" = '20260715083555_AddServicePrestations'
  );
