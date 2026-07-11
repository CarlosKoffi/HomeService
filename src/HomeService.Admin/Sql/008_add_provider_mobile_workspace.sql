ALTER TABLE "Services"
ADD COLUMN IF NOT EXISTS "RequiresPortfolio" boolean NOT NULL DEFAULT false,
ADD COLUMN IF NOT EXISTS "MinimumPortfolioItems" integer NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS "RequiresCompletionPhoto" boolean NOT NULL DEFAULT false,
ADD COLUMN IF NOT EXISTS "RequiresBeforeAfterPhotos" boolean NOT NULL DEFAULT false,
ADD COLUMN IF NOT EXISTS "RequiresDiploma" boolean NOT NULL DEFAULT false,
ADD COLUMN IF NOT EXISTS "RequiresAdminApprovalBeforeAssignment" boolean NOT NULL DEFAULT false;

ALTER TABLE "Missions"
ADD COLUMN IF NOT EXISTS "ServiceAddress" character varying(360) NULL,
ADD COLUMN IF NOT EXISTS "ServiceLatitude" numeric(9,6) NULL,
ADD COLUMN IF NOT EXISTS "ServiceLongitude" numeric(9,6) NULL,
ADD COLUMN IF NOT EXISTS "ArrivalToleranceMeters" integer NOT NULL DEFAULT 250;

CREATE TABLE IF NOT EXISTS "ProviderInvitations" (
    "Id" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "Code" character varying(16) NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "Status" character varying(32) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "AcceptedAt" timestamp with time zone NULL,
    "RevokedAt" timestamp with time zone NULL,
    "InvitationLink" character varying(1000) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ProviderInvitations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProviderInvitations_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProviderInvitations_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProviderInvitations_Code" ON "ProviderInvitations" ("Code");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProviderInvitations_TokenHash" ON "ProviderInvitations" ("TokenHash");
CREATE INDEX IF NOT EXISTS "IX_ProviderInvitations_ProviderId_Status" ON "ProviderInvitations" ("ProviderId", "Status");
CREATE INDEX IF NOT EXISTS "IX_ProviderInvitations_CompanyId" ON "ProviderInvitations" ("CompanyId");

CREATE TABLE IF NOT EXISTS "ProviderPortalSessions" (
    "Id" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ProviderPortalSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProviderPortalSessions_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProviderPortalSessions_TokenHash" ON "ProviderPortalSessions" ("TokenHash");
CREATE INDEX IF NOT EXISTS "IX_ProviderPortalSessions_ProviderId_ExpiresAt" ON "ProviderPortalSessions" ("ProviderId", "ExpiresAt");

CREATE TABLE IF NOT EXISTS "ProviderServicePortfolioItems" (
    "Id" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "ServiceId" uuid NOT NULL,
    "OriginalFileName" character varying(260) NOT NULL,
    "StoragePath" character varying(640) NOT NULL,
    "ContentType" character varying(120) NOT NULL,
    "DisplayOrder" integer NOT NULL,
    "Status" character varying(32) NOT NULL,
    "RejectionReason" character varying(600) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ProviderServicePortfolioItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProviderServicePortfolioItems_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProviderServicePortfolioItems_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ProviderServicePortfolioItems_ProviderId_ServiceId_DisplayOrder" ON "ProviderServicePortfolioItems" ("ProviderId", "ServiceId", "DisplayOrder");
CREATE INDEX IF NOT EXISTS "IX_ProviderServicePortfolioItems_ServiceId" ON "ProviderServicePortfolioItems" ("ServiceId");

CREATE TABLE IF NOT EXISTS "ProviderMissionAssignments" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "ProviderId" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "Status" character varying(32) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RespondedAt" timestamp with time zone NULL,
    "StartedAt" timestamp with time zone NULL,
    "CompletedAt" timestamp with time zone NULL,
    "RefusalReason" character varying(32) NULL,
    "RefusalComment" character varying(600) NULL,
    "CompletionNote" character varying(1000) NULL,
    "CompletionPhotoPath" character varying(640) NULL,
    "OfferedLatitude" numeric(9,6) NULL,
    "OfferedLongitude" numeric(9,6) NULL,
    "OfferedAccuracyMeters" integer NULL,
    "AcceptedLatitude" numeric(9,6) NULL,
    "AcceptedLongitude" numeric(9,6) NULL,
    "AcceptedAccuracyMeters" integer NULL,
    "ArrivalLatitude" numeric(9,6) NULL,
    "ArrivalLongitude" numeric(9,6) NULL,
    "ArrivalAccuracyMeters" integer NULL,
    "ArrivalDistanceMeters" integer NULL,
    "ArrivalToleranceMeters" integer NOT NULL DEFAULT 250,
    "ArrivalVerificationStatus" character varying(32) NOT NULL DEFAULT 'NotChecked',
    "ArrivalVerifiedAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_ProviderMissionAssignments" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_ProviderMissionAssignments_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProviderMissionAssignments_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProviderMissionAssignments_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_ProviderMissionAssignments_ProviderId_Status" ON "ProviderMissionAssignments" ("ProviderId", "Status");
CREATE INDEX IF NOT EXISTS "IX_ProviderMissionAssignments_MissionId_ProviderId" ON "ProviderMissionAssignments" ("MissionId", "ProviderId");
CREATE INDEX IF NOT EXISTS "IX_ProviderMissionAssignments_CompanyId" ON "ProviderMissionAssignments" ("CompanyId");

ALTER TABLE "ProviderMissionAssignments"
ADD COLUMN IF NOT EXISTS "OfferedLatitude" numeric(9,6) NULL,
ADD COLUMN IF NOT EXISTS "OfferedLongitude" numeric(9,6) NULL,
ADD COLUMN IF NOT EXISTS "OfferedAccuracyMeters" integer NULL,
ADD COLUMN IF NOT EXISTS "AcceptedLatitude" numeric(9,6) NULL,
ADD COLUMN IF NOT EXISTS "AcceptedLongitude" numeric(9,6) NULL,
ADD COLUMN IF NOT EXISTS "AcceptedAccuracyMeters" integer NULL,
ADD COLUMN IF NOT EXISTS "ArrivalLatitude" numeric(9,6) NULL,
ADD COLUMN IF NOT EXISTS "ArrivalLongitude" numeric(9,6) NULL,
ADD COLUMN IF NOT EXISTS "ArrivalAccuracyMeters" integer NULL,
ADD COLUMN IF NOT EXISTS "ArrivalDistanceMeters" integer NULL,
ADD COLUMN IF NOT EXISTS "ArrivalToleranceMeters" integer NOT NULL DEFAULT 250,
ADD COLUMN IF NOT EXISTS "ArrivalVerificationStatus" character varying(32) NOT NULL DEFAULT 'NotChecked',
ADD COLUMN IF NOT EXISTS "ArrivalVerifiedAt" timestamp with time zone NULL;

CREATE INDEX IF NOT EXISTS "IX_ProviderMissionAssignments_ProviderId_ArrivalVerificationStatus" ON "ProviderMissionAssignments" ("ProviderId", "ArrivalVerificationStatus");

CREATE TABLE IF NOT EXISTS "MissionConversations" (
    "Id" uuid NOT NULL,
    "MissionId" uuid NOT NULL,
    "ProviderId" uuid NULL,
    "CompanyId" uuid NULL,
    "CustomerId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_MissionConversations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionConversations_Missions_MissionId" FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_MissionConversations_Providers_ProviderId" FOREIGN KEY ("ProviderId") REFERENCES "Providers" ("Id"),
    CONSTRAINT "FK_MissionConversations_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_MissionConversations_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MissionConversations_MissionId" ON "MissionConversations" ("MissionId");
CREATE INDEX IF NOT EXISTS "IX_MissionConversations_ProviderId" ON "MissionConversations" ("ProviderId");
CREATE INDEX IF NOT EXISTS "IX_MissionConversations_CompanyId" ON "MissionConversations" ("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_MissionConversations_CustomerId" ON "MissionConversations" ("CustomerId");

CREATE TABLE IF NOT EXISTS "MissionMessages" (
    "Id" uuid NOT NULL,
    "ConversationId" uuid NOT NULL,
    "SenderType" character varying(32) NOT NULL,
    "SenderId" uuid NULL,
    "Body" character varying(2000) NOT NULL,
    "AttachmentPath" character varying(640) NULL,
    "AttachmentContentType" character varying(120) NULL,
    "ReadAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_MissionMessages" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_MissionMessages_MissionConversations_ConversationId" FOREIGN KEY ("ConversationId") REFERENCES "MissionConversations" ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_MissionMessages_ConversationId_CreatedAt" ON "MissionMessages" ("ConversationId", "CreatedAt");
