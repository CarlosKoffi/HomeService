START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsComponentDefinitions" (
        "Id" uuid NOT NULL,
        "Key" character varying(120) NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Description" character varying(700),
        "SchemaVersion" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsComponentDefinitions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsMediaAssets" (
        "Id" uuid NOT NULL,
        "FileName" character varying(260) NOT NULL,
        "StoragePath" character varying(900) NOT NULL,
        "ContentType" character varying(120) NOT NULL,
        "SizeInBytes" bigint NOT NULL,
        "Width" integer,
        "Height" integer,
        "AltText" character varying(300),
        "Checksum" character varying(128),
        "Status" character varying(32) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsMediaAssets" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsSites" (
        "Id" uuid NOT NULL,
        "Code" character varying(80) NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Surface" character varying(40) NOT NULL,
        "Status" character varying(32) NOT NULL,
        "DefaultCountryId" uuid,
        "DefaultLanguageId" uuid NOT NULL,
        "HomePageCode" character varying(80),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsSites" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsSites_Countries_DefaultCountryId" FOREIGN KEY ("DefaultCountryId") REFERENCES "Countries" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CmsSites_Languages_DefaultLanguageId" FOREIGN KEY ("DefaultLanguageId") REFERENCES "Languages" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsMediaVariants" (
        "Id" uuid NOT NULL,
        "MediaAssetId" uuid NOT NULL,
        "VariantKey" character varying(80) NOT NULL,
        "StoragePath" character varying(900) NOT NULL,
        "ContentType" character varying(120) NOT NULL,
        "SizeInBytes" bigint NOT NULL,
        "Width" integer,
        "Height" integer,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsMediaVariants" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsMediaVariants_CmsMediaAssets_MediaAssetId" FOREIGN KEY ("MediaAssetId") REFERENCES "CmsMediaAssets" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsMenus" (
        "Id" uuid NOT NULL,
        "SiteId" uuid NOT NULL,
        "Code" character varying(80) NOT NULL,
        "Name" character varying(160) NOT NULL,
        "Placement" character varying(80) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsMenus" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsMenus_CmsSites_SiteId" FOREIGN KEY ("SiteId") REFERENCES "CmsSites" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsPages" (
        "Id" uuid NOT NULL,
        "SiteId" uuid NOT NULL,
        "Code" character varying(100) NOT NULL,
        "InternalName" character varying(180) NOT NULL,
        "TemplateKey" character varying(100) NOT NULL,
        "Status" character varying(32) NOT NULL,
        "RequiresAuthentication" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsPages" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsPages_CmsSites_SiteId" FOREIGN KEY ("SiteId") REFERENCES "CmsSites" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsMenuItems" (
        "Id" uuid NOT NULL,
        "MenuId" uuid NOT NULL,
        "ParentMenuItemId" uuid,
        "Label" character varying(160) NOT NULL,
        "PageId" uuid,
        "TargetValue" character varying(700),
        "TargetType" character varying(32) NOT NULL,
        "Position" integer NOT NULL,
        "IconName" character varying(80),
        "OpenInNewTab" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsMenuItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsMenuItems_CmsMenuItems_ParentMenuItemId" FOREIGN KEY ("ParentMenuItemId") REFERENCES "CmsMenuItems" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CmsMenuItems_CmsMenus_MenuId" FOREIGN KEY ("MenuId") REFERENCES "CmsMenus" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CmsMenuItems_CmsPages_PageId" FOREIGN KEY ("PageId") REFERENCES "CmsPages" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsPageTranslations" (
        "Id" uuid NOT NULL,
        "SiteId" uuid NOT NULL,
        "PageId" uuid NOT NULL,
        "LanguageId" uuid NOT NULL,
        "Slug" character varying(180) NOT NULL,
        "Title" character varying(220) NOT NULL,
        "SeoTitle" character varying(220),
        "MetaDescription" character varying(400),
        "TranslationStatus" character varying(32) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsPageTranslations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsPageTranslations_CmsPages_PageId" FOREIGN KEY ("PageId") REFERENCES "CmsPages" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CmsPageTranslations_CmsSites_SiteId" FOREIGN KEY ("SiteId") REFERENCES "CmsSites" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CmsPageTranslations_Languages_LanguageId" FOREIGN KEY ("LanguageId") REFERENCES "Languages" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsPageVersions" (
        "Id" uuid NOT NULL,
        "PageId" uuid NOT NULL,
        "VersionNumber" integer NOT NULL,
        "Status" character varying(32) NOT NULL,
        "PublishedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsPageVersions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsPageVersions_CmsPages_PageId" FOREIGN KEY ("PageId") REFERENCES "CmsPages" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsSections" (
        "Id" uuid NOT NULL,
        "PageVersionId" uuid NOT NULL,
        "ComponentDefinitionId" uuid NOT NULL,
        "InternalName" character varying(180) NOT NULL,
        "Zone" character varying(80) NOT NULL,
        "Position" integer NOT NULL,
        "Anchor" character varying(120),
        "Variant" character varying(120),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsSections" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsSections_CmsComponentDefinitions_ComponentDefinitionId" FOREIGN KEY ("ComponentDefinitionId") REFERENCES "CmsComponentDefinitions" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CmsSections_CmsPageVersions_PageVersionId" FOREIGN KEY ("PageVersionId") REFERENCES "CmsPageVersions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE TABLE "CmsContentValues" (
        "Id" uuid NOT NULL,
        "SectionId" uuid NOT NULL,
        "FieldKey" character varying(120) NOT NULL,
        "ValueType" character varying(32) NOT NULL,
        "LanguageId" uuid,
        "TextValue" character varying(4000),
        "DecimalValue" numeric,
        "BooleanValue" boolean,
        "DateTimeValue" timestamp with time zone,
        "MediaAssetId" uuid,
        "JsonValue" jsonb,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CmsContentValues" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CmsContentValues_CmsMediaAssets_MediaAssetId" FOREIGN KEY ("MediaAssetId") REFERENCES "CmsMediaAssets" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_CmsContentValues_CmsSections_SectionId" FOREIGN KEY ("SectionId") REFERENCES "CmsSections" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CmsContentValues_Languages_LanguageId" FOREIGN KEY ("LanguageId") REFERENCES "Languages" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsComponentDefinitions_Key_SchemaVersion" ON "CmsComponentDefinitions" ("Key", "SchemaVersion");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsContentValues_LanguageId" ON "CmsContentValues" ("LanguageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsContentValues_MediaAssetId" ON "CmsContentValues" ("MediaAssetId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsContentValues_SectionId_FieldKey_LanguageId" ON "CmsContentValues" ("SectionId", "FieldKey", "LanguageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsMediaAssets_Checksum" ON "CmsMediaAssets" ("Checksum");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsMediaAssets_Status" ON "CmsMediaAssets" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsMediaAssets_StoragePath" ON "CmsMediaAssets" ("StoragePath");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsMediaVariants_MediaAssetId_VariantKey" ON "CmsMediaVariants" ("MediaAssetId", "VariantKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsMediaVariants_StoragePath" ON "CmsMediaVariants" ("StoragePath");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsMenuItems_MenuId_ParentMenuItemId_Position" ON "CmsMenuItems" ("MenuId", "ParentMenuItemId", "Position");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsMenuItems_PageId" ON "CmsMenuItems" ("PageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsMenuItems_ParentMenuItemId" ON "CmsMenuItems" ("ParentMenuItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsMenus_SiteId_Code" ON "CmsMenus" ("SiteId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsMenus_SiteId_Placement" ON "CmsMenus" ("SiteId", "Placement");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsPages_SiteId_Code" ON "CmsPages" ("SiteId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsPages_SiteId_Status" ON "CmsPages" ("SiteId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsPageTranslations_LanguageId" ON "CmsPageTranslations" ("LanguageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsPageTranslations_PageId_LanguageId" ON "CmsPageTranslations" ("PageId", "LanguageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsPageTranslations_SiteId_LanguageId_Slug" ON "CmsPageTranslations" ("SiteId", "LanguageId", "Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsPageVersions_PageId_Status" ON "CmsPageVersions" ("PageId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsPageVersions_PageId_VersionNumber" ON "CmsPageVersions" ("PageId", "VersionNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsSections_ComponentDefinitionId_IsActive" ON "CmsSections" ("ComponentDefinitionId", "IsActive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsSections_PageVersionId_Zone_Position" ON "CmsSections" ("PageVersionId", "Zone", "Position");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE UNIQUE INDEX "IX_CmsSites_Code" ON "CmsSites" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsSites_DefaultCountryId" ON "CmsSites" ("DefaultCountryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsSites_DefaultLanguageId" ON "CmsSites" ("DefaultLanguageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    CREATE INDEX "IX_CmsSites_Surface_DefaultCountryId" ON "CmsSites" ("Surface", "DefaultCountryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714225041_AddCmsFoundation') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714225041_AddCmsFoundation', '9.0.0');
    END IF;
END $EF$;
COMMIT;

