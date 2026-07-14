CREATE TABLE IF NOT EXISTS "CountryBrandings" (
    "Id" uuid NOT NULL,
    "CountryId" uuid NOT NULL,
    "BrandName" character varying(120) NOT NULL,
    "PrimaryColor" character varying(16) NOT NULL,
    "SecondaryColor" character varying(16) NOT NULL,
    "AccentColor" character varying(16) NOT NULL,
    "HeroTitle" character varying(220) NOT NULL,
    "HeroSubtitle" character varying(600) NOT NULL,
    "HeroImageUrl" character varying(1000),
    "MotifStyle" character varying(80) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_CountryBrandings" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CountryBrandings_Countries_CountryId" FOREIGN KEY ("CountryId") REFERENCES "Countries" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_CountryBrandings_CountryId"
    ON "CountryBrandings" ("CountryId");
