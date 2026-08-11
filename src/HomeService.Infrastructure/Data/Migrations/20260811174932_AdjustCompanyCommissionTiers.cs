using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdjustCompanyCommissionTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "CompanyCommissionTiers"
                SET "IsActive" = false,
                    "UpdatedAt" = now();

                INSERT INTO "CompanyCommissionTiers"
                    ("Id", "Name", "MinimumMissionCount", "RateBasisPoints", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ('9c5d9517-ec35-4e52-84a7-100000000101', 'Lancement', 1, 1500, 10, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000102', 'Palier 50', 50, 1450, 20, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000103', 'Palier 100', 100, 1400, 30, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000104', 'Palier 150', 150, 1350, 40, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000105', 'Palier 200', 200, 1300, 50, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000106', 'Palier 250', 250, 1250, 60, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000107', 'Palier 300', 300, 1200, 70, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000108', 'Palier 350', 350, 1150, 80, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000109', 'Palier 400', 400, 1100, 90, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000110', 'Palier 450', 450, 1050, 100, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000111', 'Elite', 500, 1000, 110, true, now(), now())
                ON CONFLICT ("MinimumMissionCount") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "RateBasisPoints" = EXCLUDED."RateBasisPoints",
                    "SortOrder" = EXCLUDED."SortOrder",
                    "IsActive" = true,
                    "UpdatedAt" = now();

                UPDATE "Companies"
                SET "CurrentCommissionTierName" = CASE
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 500 THEN 'Elite'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 450 THEN 'Palier 450'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 400 THEN 'Palier 400'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 350 THEN 'Palier 350'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 300 THEN 'Palier 300'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 250 THEN 'Palier 250'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 200 THEN 'Palier 200'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 150 THEN 'Palier 150'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 100 THEN 'Palier 100'
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 50 THEN 'Palier 50'
                        ELSE 'Lancement'
                    END,
                    "CurrentCommissionTierMinimumMissionCount" = CASE
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 500 THEN 500
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 450 THEN 450
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 400 THEN 400
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 350 THEN 350
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 300 THEN 300
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 250 THEN 250
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 200 THEN 200
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 150 THEN 150
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 100 THEN 100
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 50 THEN 50
                        ELSE 1
                    END,
                    "CurrentCommissionRateBasisPoints" = CASE
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 500 THEN 1000
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 450 THEN 1050
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 400 THEN 1100
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 350 THEN 1150
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 300 THEN 1200
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 250 THEN 1250
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 200 THEN 1300
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 150 THEN 1350
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 100 THEN 1400
                        WHEN "CurrentCommissionTierMinimumMissionCount" >= 50 THEN 1450
                        ELSE 1500
                    END,
                    "UpdatedAt" = now();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "CompanyCommissionTiers"
                SET "IsActive" = false,
                    "UpdatedAt" = now();

                INSERT INTO "CompanyCommissionTiers"
                    ("Id", "Name", "MinimumMissionCount", "RateBasisPoints", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
                VALUES
                    ('9c5d9517-ec35-4e52-84a7-100000000001', 'Lancement', 1, 1500, 10, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000002', 'Essor', 50, 1400, 20, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000003', 'Croissance', 150, 1300, 30, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000004', 'Performance', 300, 1200, 40, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000005', 'Excellence', 600, 1100, 50, true, now(), now()),
                    ('9c5d9517-ec35-4e52-84a7-100000000006', 'Elite', 1000, 1000, 60, true, now(), now())
                ON CONFLICT ("MinimumMissionCount") DO UPDATE
                SET "Name" = EXCLUDED."Name",
                    "RateBasisPoints" = EXCLUDED."RateBasisPoints",
                    "SortOrder" = EXCLUDED."SortOrder",
                    "IsActive" = true,
                    "UpdatedAt" = now();
                """);
        }
    }
}
