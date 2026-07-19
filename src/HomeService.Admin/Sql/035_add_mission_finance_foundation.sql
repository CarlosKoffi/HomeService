START TRANSACTION;
ALTER TABLE "Missions" ALTER COLUMN "CompanyQuoteJustification" TYPE character varying(1200);

ALTER TABLE "Missions" ADD "AssignmentSource" character varying(32) NOT NULL DEFAULT 'Company';

ALTER TABLE "Missions" ADD "CompanyPayoutAmount" integer NOT NULL DEFAULT 0;

ALTER TABLE "Missions" ADD "Description" character varying(1200);

ALTER TABLE "Missions" ADD "IsInterimProviderSnapshot" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Missions" ADD "KazaAssignmentCommissionRateBasisPoints" integer NOT NULL DEFAULT 0;

ALTER TABLE "Missions" ADD "PartsDescription" character varying(1200);

ALTER TABLE "Missions" ADD "PartsEstimateAmount" integer;

ALTER TABLE "Missions" ADD "PlatformCommissionRateBasisPoints" integer NOT NULL DEFAULT 0;

ALTER TABLE "Missions" ADD "QuoteStatus" character varying(32) NOT NULL DEFAULT 'NotRequired';

ALTER TABLE "Missions" ADD "RequiresCompanyQuote" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Missions" ADD "ServicePrestationId" uuid;

CREATE TABLE "CommissionRules" (
    "Id" uuid NOT NULL,
    "Name" character varying(160) NOT NULL,
    "Target" character varying(40) NOT NULL,
    "ServiceId" uuid,
    "ServicePrestationId" uuid,
    "CompanyId" uuid,
    "AssignmentSource" character varying(32),
    "RateBasisPoints" integer NOT NULL,
    "FixedAmount" integer NOT NULL,
    "Currency" character varying(8) NOT NULL,
    "EffectiveFrom" timestamp with time zone NOT NULL,
    "EffectiveUntil" timestamp with time zone,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_CommissionRules" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CommissionRules_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_CommissionRules_ServicePrestations_ServicePrestationId" FOREIGN KEY ("ServicePrestationId") REFERENCES "ServicePrestations" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_CommissionRules_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("Id") ON DELETE SET NULL
);

CREATE TABLE "MissionAttachments" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "AttachmentType" character varying(40) NOT NULL,
    "OriginalFileName" character varying(260) NOT NULL,
    "StoragePath" character varying(720) NOT NULL,
    "ContentType" character varying(120) NOT NULL,
    "FileSizeBytes" bigint NOT NULL,
    "Caption" character varying(500),
    "IsDeleted" boolean NOT NULL DEFAULT FALSE,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_MissionAttachments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionAttachments_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "MissionFinancialBreakdowns" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "LineType" character varying(40) NOT NULL,
    "Label" character varying(160) NOT NULL,
    "Amount" integer NOT NULL,
    "Currency" character varying(8) NOT NULL,
    "SortOrder" integer NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_MissionFinancialBreakdowns" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionFinancialBreakdowns_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "MissionPaymentMilestones" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "Trigger" character varying(40) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "Amount" integer NOT NULL,
    "Currency" character varying(8) NOT NULL,
    "Label" character varying(160) NOT NULL,
    "SortOrder" integer NOT NULL,
    "DueAt" timestamp with time zone,
    "PaidAt" timestamp with time zone,
    "ExternalPaymentReference" character varying(160),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_MissionPaymentMilestones" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionPaymentMilestones_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE
);

CREATE TABLE "MissionStatusHistories" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "FromStatus" character varying(32) NOT NULL,
    "ToStatus" character varying(32) NOT NULL,
    "ActorType" character varying(40) NOT NULL,
    "ActorId" uuid,
    "Note" character varying(1000),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_MissionStatusHistories" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionStatusHistories_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Missions_CompanyId_Status" ON "Missions" ("CompanyId", "Status");

CREATE INDEX "IX_Missions_QuoteStatus_PaymentStatus" ON "Missions" ("QuoteStatus", "PaymentStatus");

CREATE INDEX "IX_Missions_ServicePrestationId_Status" ON "Missions" ("ServicePrestationId", "Status");

CREATE INDEX "IX_CommissionRules_CompanyId" ON "CommissionRules" ("CompanyId");

CREATE INDEX "IX_CommissionRules_ServiceId_ServicePrestationId_CompanyId_Ass~" ON "CommissionRules" ("ServiceId", "ServicePrestationId", "CompanyId", "AssignmentSource");

CREATE INDEX "IX_CommissionRules_ServicePrestationId" ON "CommissionRules" ("ServicePrestationId");

CREATE INDEX "IX_CommissionRules_Target_IsActive_EffectiveFrom" ON "CommissionRules" ("Target", "IsActive", "EffectiveFrom");

CREATE INDEX "IX_MissionAttachments_MissionId_AttachmentType_IsDeleted" ON "MissionAttachments" ("MissionId", "AttachmentType", "IsDeleted");

CREATE INDEX "IX_MissionFinancialBreakdowns_MissionId_LineType_SortOrder" ON "MissionFinancialBreakdowns" ("MissionId", "LineType", "SortOrder");

CREATE INDEX "IX_MissionPaymentMilestones_MissionId_Trigger" ON "MissionPaymentMilestones" ("MissionId", "Trigger");

CREATE INDEX "IX_MissionPaymentMilestones_Status_DueAt" ON "MissionPaymentMilestones" ("Status", "DueAt");

CREATE INDEX "IX_MissionStatusHistories_MissionId_CreatedAt" ON "MissionStatusHistories" ("MissionId", "CreatedAt");

ALTER TABLE "Missions" ADD CONSTRAINT "FK_Missions_ServicePrestations_ServicePrestationId" FOREIGN KEY ("ServicePrestationId") REFERENCES "ServicePrestations" ("Id") ON DELETE SET NULL;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260719122253_AddMissionFinanceFoundation', '9.0.0');

COMMIT;

