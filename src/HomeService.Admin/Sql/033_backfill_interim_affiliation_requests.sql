WITH compatible_companies AS (
    SELECT
        provider."Id" AS "ProviderId",
        company."Id" AS "CompanyId",
        MIN(CASE WHEN provider_service."CompanyId" IS NOT NULL THEN 0 ELSE 1 END) AS "Priority",
        company."Name" AS "CompanyName"
    FROM "Providers" provider
    JOIN "ProviderCandidateServices" candidate_service
        ON candidate_service."ProviderId" = provider."Id"
        AND candidate_service."IsActive" = true
    JOIN "Companies" company
        ON company."Status" = 'Approved'
        AND company."AcceptsInterimApplications" = true
    LEFT JOIN "ProviderServices" provider_service
        ON provider_service."CompanyId" = company."Id"
        AND provider_service."ServiceId" = candidate_service."ServiceId"
        AND provider_service."IsActive" = true
    LEFT JOIN "CompanyApplications" application
        ON application."CompanyId" = company."Id"
    LEFT JOIN "CompanyApplicationServices" application_service
        ON application_service."CompanyApplicationId" = application."Id"
        AND application_service."MatchedServiceId" = candidate_service."ServiceId"
    WHERE provider."Status" = 'InterimCandidate'
        AND provider."CompanyId" IS NULL
        AND (provider_service."CompanyId" IS NOT NULL OR application_service."Id" IS NOT NULL)
        AND NOT EXISTS (
            SELECT 1
            FROM "ProviderAffiliationRequests" existing
            WHERE existing."ProviderId" = provider."Id"
                AND existing."CompanyId" = company."Id"
        )
    GROUP BY provider."Id", company."Id", company."Name"
),
compatible_requests AS (
    SELECT
        "ProviderId",
        "CompanyId",
        ROW_NUMBER() OVER (
            PARTITION BY "ProviderId"
            ORDER BY "Priority", random()
        ) AS "Rank"
    FROM compatible_companies
)
INSERT INTO "ProviderAffiliationRequests" (
    "Id",
    "ProviderId",
    "CompanyId",
    "Status",
    "Message",
    "ReviewNote",
    "RequestedAt",
    "ReviewedAt",
    "CreatedAt",
    "UpdatedAt"
)
SELECT
    gen_random_uuid(),
    "ProviderId",
    "CompanyId",
    'Pending',
    'Candidature rattachee automatiquement depuis les services declares.',
    NULL,
    now(),
    NULL,
    now(),
    NULL
FROM compatible_requests
WHERE "Rank" <= 1;
