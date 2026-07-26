WITH missing_rules AS (
    SELECT
        template."EventKey",
        max(template."Label") AS "Label",
        max(template."Audience") AS "Audience",
        bool_or(template."Channel" = 'Email') AS "EmailEnabled",
        bool_or(template."Channel" = 'WhatsApp') AS "WhatsAppEnabled",
        max(template."SubjectTemplate") AS "SubjectTemplate",
        max(template."BodyTemplate") AS "BodyTemplate"
    FROM "NotificationTemplates" AS template
    LEFT JOIN "NotificationDeliveryRules" AS rule ON rule."EventKey" = template."EventKey"
    WHERE rule."Id" IS NULL
    GROUP BY template."EventKey"
),
missing_rules_with_id AS (
    SELECT
        (
            substr(md5("EventKey"), 1, 8) || '-' ||
            substr(md5("EventKey"), 9, 4) || '-' ||
            substr(md5("EventKey"), 13, 4) || '-' ||
            substr(md5("EventKey"), 17, 4) || '-' ||
            substr(md5("EventKey"), 21, 12)
        )::uuid AS "Id",
        *
    FROM missing_rules
)
INSERT INTO "NotificationDeliveryRules"
    ("Id", "EventKey", "Label", "Audience", "PortalEnabled", "MobileAppEnabled", "EmailEnabled", "WhatsAppEnabled", "SubjectTemplate", "BodyTemplate", "CreatedAt", "UpdatedAt")
SELECT
    "Id",
    "EventKey",
    "Label",
    "Audience",
    "Audience" IN ('Company', 'Mixed'),
    "Audience" IN ('Provider', 'Customer', 'Mixed'),
    "EmailEnabled",
    "WhatsAppEnabled",
    "SubjectTemplate",
    "BodyTemplate",
    now(),
    now()
FROM missing_rules_with_id
ON CONFLICT ("EventKey") DO NOTHING;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_name = 'AK_NotificationDeliveryRules_EventKey'
          AND table_name = 'NotificationDeliveryRules'
    ) THEN
        ALTER TABLE "NotificationDeliveryRules"
            ADD CONSTRAINT "AK_NotificationDeliveryRules_EventKey"
            UNIQUE ("EventKey");
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.table_constraints
        WHERE constraint_name = 'FK_NotificationTemplates_NotificationDeliveryRules_EventKey'
          AND table_name = 'NotificationTemplates'
    ) THEN
        ALTER TABLE "NotificationTemplates"
            ADD CONSTRAINT "FK_NotificationTemplates_NotificationDeliveryRules_EventKey"
            FOREIGN KEY ("EventKey")
            REFERENCES "NotificationDeliveryRules" ("EventKey")
            ON DELETE CASCADE;
    END IF;
END $$;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
SELECT '20260726013927_LinkNotificationTemplatesToRules', '9.0.0'
WHERE NOT EXISTS (
    SELECT 1
    FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20260726013927_LinkNotificationTemplatesToRules'
);
