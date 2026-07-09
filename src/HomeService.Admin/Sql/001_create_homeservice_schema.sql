CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE TABLE "Companies" (
        "Id" uuid NOT NULL,
        "Name" character varying(160) NOT NULL,
        "PhoneNumber" character varying(32) NOT NULL,
        "Email" character varying(256),
        "Status" character varying(32) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Companies" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE TABLE "CompanyApplications" (
        "Id" uuid NOT NULL,
        "CompanyName" character varying(180) NOT NULL,
        "RegistrationNumber" character varying(80),
        "City" character varying(120) NOT NULL,
        "Address" character varying(240),
        "ContactName" character varying(160) NOT NULL,
        "Email" character varying(256) NOT NULL,
        "PhoneNumber" character varying(32) NOT NULL,
        "PlannedServices" character varying(1000),
        "EstimatedProviderCount" integer,
        "Status" character varying(40) NOT NULL,
        "SubmittedAt" timestamp with time zone,
        "ReviewedAt" timestamp with time zone,
        "LastReminderSentAt" timestamp with time zone,
        "ActivationEmailSentAt" timestamp with time zone,
        "ActivatedAt" timestamp with time zone,
        "ReviewNote" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CompanyApplications" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE TABLE "Customers" (
        "Id" uuid NOT NULL,
        "FirstName" character varying(120) NOT NULL,
        "LastName" character varying(120) NOT NULL,
        "PhoneNumber" character varying(32) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Customers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE TABLE "Missions" (
        "Id" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "ServiceId" uuid NOT NULL,
        "ProviderId" uuid,
        "CompanyId" uuid,
        "Mode" character varying(32) NOT NULL,
        "Status" character varying(32) NOT NULL,
        "PaymentMethod" character varying(32) NOT NULL,
        "PaymentStatus" character varying(32) NOT NULL,
        "ScheduledFor" timestamp with time zone,
        "EstimatedDurationMinutes" integer NOT NULL,
        "ActualDurationMinutes" integer,
        "HourlyRateAmount" integer,
        "EstimatedTotalAmount" integer,
        "FinalTotalAmount" integer,
        "Currency" character varying(3) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Missions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE TABLE "Services" (
        "Id" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(800),
        "CreatedByCompanyId" uuid,
        "Status" character varying(32) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Services" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE TABLE "Providers" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "FirstName" character varying(120) NOT NULL,
        "LastName" character varying(120) NOT NULL,
        "PhoneNumber" character varying(32) NOT NULL,
        "Status" character varying(32) NOT NULL,
        "IsAvailable" boolean NOT NULL,
        "CurrentLatitude" numeric(9,6),
        "CurrentLongitude" numeric(9,6),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Providers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Providers_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE TABLE "CompanyApplicationDocuments" (
        "Id" uuid NOT NULL,
        "CompanyApplicationId" uuid NOT NULL,
        "DocumentType" character varying(48) NOT NULL,
        "OriginalFileName" character varying(260) NOT NULL,
        "StoragePath" character varying(500) NOT NULL,
        "ContentType" character varying(120) NOT NULL,
        "ReviewStatus" character varying(40) NOT NULL,
        "ReviewNote" character varying(800),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CompanyApplicationDocuments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CompanyApplicationDocuments_CompanyApplications_CompanyAppl~" FOREIGN KEY ("CompanyApplicationId") REFERENCES "CompanyApplications" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE TABLE "ProviderServices" (
        "Id" uuid NOT NULL,
        "ProviderId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ServiceId" uuid NOT NULL,
        "ExperienceLevel" character varying(32) NOT NULL,
        "YearsOfExperience" integer NOT NULL,
        "HourlyRateAmount" integer NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "PricingUnit" character varying(32) NOT NULL,
        "CompanyValidatedAt" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        "ProviderProfileId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProviderServices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProviderServices_Providers_ProviderProfileId" FOREIGN KEY ("ProviderProfileId") REFERENCES "Providers" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE INDEX "IX_CompanyApplicationDocuments_CompanyApplicationId" ON "CompanyApplicationDocuments" ("CompanyApplicationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE INDEX "IX_CompanyApplications_Status_SubmittedAt" ON "CompanyApplications" ("Status", "SubmittedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE INDEX "IX_Missions_ServiceId_Status" ON "Missions" ("ServiceId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE INDEX "IX_Providers_CompanyId" ON "Providers" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_ProviderServices_ProviderId_ServiceId" ON "ProviderServices" ("ProviderId", "ServiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE INDEX "IX_ProviderServices_ProviderProfileId" ON "ProviderServices" ("ProviderProfileId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    CREATE INDEX "IX_Services_Name" ON "Services" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709110550_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260709110550_InitialCreate', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE TABLE "Countries" (
        "Id" uuid NOT NULL,
        "IsoCode" character varying(2) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "CurrencyCode" character varying(3) NOT NULL,
        "IsActive" boolean NOT NULL,
        "IsLaunchCountry" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Countries" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE TABLE "Languages" (
        "Id" uuid NOT NULL,
        "Code" character varying(12) NOT NULL,
        "Name" character varying(80) NOT NULL,
        "IsDefault" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Languages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE TABLE "TranslationKeys" (
        "Id" uuid NOT NULL,
        "Key" character varying(180) NOT NULL,
        "Description" character varying(500) NOT NULL,
        "Scope" character varying(80) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_TranslationKeys" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE TABLE "TranslationValues" (
        "Id" uuid NOT NULL,
        "TranslationKeyId" uuid NOT NULL,
        "LanguageId" uuid NOT NULL,
        "CountryId" uuid,
        "Value" character varying(4000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_TranslationValues" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TranslationValues_Countries_CountryId" FOREIGN KEY ("CountryId") REFERENCES "Countries" ("Id"),
        CONSTRAINT "FK_TranslationValues_Languages_LanguageId" FOREIGN KEY ("LanguageId") REFERENCES "Languages" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TranslationValues_TranslationKeys_TranslationKeyId" FOREIGN KEY ("TranslationKeyId") REFERENCES "TranslationKeys" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE UNIQUE INDEX "IX_Countries_IsoCode" ON "Countries" ("IsoCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE UNIQUE INDEX "IX_Languages_Code" ON "Languages" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE UNIQUE INDEX "IX_TranslationKeys_Key" ON "TranslationKeys" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE INDEX "IX_TranslationValues_CountryId" ON "TranslationValues" ("CountryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE INDEX "IX_TranslationValues_LanguageId" ON "TranslationValues" ("LanguageId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    CREATE UNIQUE INDEX "IX_TranslationValues_TranslationKeyId_LanguageId_CountryId" ON "TranslationValues" ("TranslationKeyId", "LanguageId", "CountryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709114915_AddLocalization') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260709114915_AddLocalization', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE TABLE "AdminModules" (
        "Id" uuid NOT NULL,
        "Key" character varying(80) NOT NULL,
        "Name" character varying(140) NOT NULL,
        "Description" character varying(500) NOT NULL,
        "DisplayOrder" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AdminModules" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE TABLE "AdminRoles" (
        "Id" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(500) NOT NULL,
        "IsSystemRole" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AdminRoles" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE TABLE "AdminUsers" (
        "Id" uuid NOT NULL,
        "FullName" character varying(160) NOT NULL,
        "Email" character varying(256) NOT NULL,
        "IsSuperAdmin" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "LastLoginAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AdminUsers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE TABLE "AdminRolePermissions" (
        "Id" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        "ModuleId" uuid NOT NULL,
        "Action" character varying(80) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AdminRolePermissions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AdminRolePermissions_AdminModules_ModuleId" FOREIGN KEY ("ModuleId") REFERENCES "AdminModules" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AdminRolePermissions_AdminRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AdminRoles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE TABLE "AdminUserRoles" (
        "Id" uuid NOT NULL,
        "AdminUserId" uuid NOT NULL,
        "RoleId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AdminUserRoles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AdminUserRoles_AdminRoles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "AdminRoles" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_AdminUserRoles_AdminUsers_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES "AdminUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE UNIQUE INDEX "IX_AdminModules_Key" ON "AdminModules" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE INDEX "IX_AdminRolePermissions_ModuleId" ON "AdminRolePermissions" ("ModuleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE UNIQUE INDEX "IX_AdminRolePermissions_RoleId_ModuleId_Action" ON "AdminRolePermissions" ("RoleId", "ModuleId", "Action");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE UNIQUE INDEX "IX_AdminRoles_Name" ON "AdminRoles" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE UNIQUE INDEX "IX_AdminUserRoles_AdminUserId_RoleId" ON "AdminUserRoles" ("AdminUserId", "RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE INDEX "IX_AdminUserRoles_RoleId" ON "AdminUserRoles" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    CREATE UNIQUE INDEX "IX_AdminUsers_Email" ON "AdminUsers" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709115520_AddAdminAccessControl') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260709115520_AddAdminAccessControl', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709122817_AddDfeCompanyDocumentType') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260709122817_AddDfeCompanyDocumentType', '9.0.0');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709123812_AddCompanyApplicationRequestedServices') THEN
    ALTER TABLE "Services" ADD "NormalizedName" character varying(120) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709123812_AddCompanyApplicationRequestedServices') THEN
    CREATE TABLE "CompanyApplicationServices" (
        "Id" uuid NOT NULL,
        "CompanyApplicationId" uuid NOT NULL,
        "MatchedServiceId" uuid,
        "RawName" character varying(160) NOT NULL,
        "NormalizedName" character varying(160) NOT NULL,
        "MatchScore" integer,
        "MatchStatus" character varying(40) NOT NULL,
        "ReviewNote" character varying(800),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_CompanyApplicationServices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CompanyApplicationServices_CompanyApplications_CompanyAppli~" FOREIGN KEY ("CompanyApplicationId") REFERENCES "CompanyApplications" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_CompanyApplicationServices_Services_MatchedServiceId" FOREIGN KEY ("MatchedServiceId") REFERENCES "Services" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709123812_AddCompanyApplicationRequestedServices') THEN
    CREATE UNIQUE INDEX "IX_Services_NormalizedName" ON "Services" ("NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709123812_AddCompanyApplicationRequestedServices') THEN
    CREATE UNIQUE INDEX "IX_CompanyApplicationServices_CompanyApplicationId_NormalizedN~" ON "CompanyApplicationServices" ("CompanyApplicationId", "NormalizedName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709123812_AddCompanyApplicationRequestedServices') THEN
    CREATE INDEX "IX_CompanyApplicationServices_MatchedServiceId" ON "CompanyApplicationServices" ("MatchedServiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260709123812_AddCompanyApplicationRequestedServices') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260709123812_AddCompanyApplicationRequestedServices', '9.0.0');
    END IF;
END $EF$;
COMMIT;

