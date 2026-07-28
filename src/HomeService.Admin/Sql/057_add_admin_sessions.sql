CREATE TABLE IF NOT EXISTS "AdminSessions" (
    "Id" uuid NOT NULL,
    "AdminUserId" uuid NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone NULL,
    "LastSeenAt" timestamp with time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_AdminSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AdminSessions_AdminUsers_AdminUserId" FOREIGN KEY ("AdminUserId") REFERENCES "AdminUsers" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AdminSessions_TokenHash"
    ON "AdminSessions" ("TokenHash");

CREATE INDEX IF NOT EXISTS "IX_AdminSessions_AdminUserId_ExpiresAt"
    ON "AdminSessions" ("AdminUserId", "ExpiresAt");
