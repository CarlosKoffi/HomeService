using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomeService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCmsFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CmsComponentDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsComponentDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CmsMediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(900)", maxLength: 900, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Checksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsMediaAssets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CmsSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Surface = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DefaultCountryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultLanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    HomePageCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsSites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsSites_Countries_DefaultCountryId",
                        column: x => x.DefaultCountryId,
                        principalTable: "Countries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CmsSites_Languages_DefaultLanguageId",
                        column: x => x.DefaultLanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CmsMediaVariants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariantKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(900)", maxLength: 900, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsMediaVariants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsMediaVariants_CmsMediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "CmsMediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CmsMenus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Placement = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsMenus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsMenus_CmsSites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "CmsSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CmsPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InternalName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiresAuthentication = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsPages_CmsSites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "CmsSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CmsMenuItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentMenuItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetValue = table.Column<string>(type: "character varying(700)", maxLength: 700, nullable: true),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IconName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    OpenInNewTab = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsMenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsMenuItems_CmsMenuItems_ParentMenuItemId",
                        column: x => x.ParentMenuItemId,
                        principalTable: "CmsMenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CmsMenuItems_CmsMenus_MenuId",
                        column: x => x.MenuId,
                        principalTable: "CmsMenus",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CmsMenuItems_CmsPages_PageId",
                        column: x => x.PageId,
                        principalTable: "CmsPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CmsPageTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    SeoTitle = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true),
                    MetaDescription = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    TranslationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsPageTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsPageTranslations_CmsPages_PageId",
                        column: x => x.PageId,
                        principalTable: "CmsPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CmsPageTranslations_CmsSites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "CmsSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CmsPageTranslations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CmsPageVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsPageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsPageVersions_CmsPages_PageId",
                        column: x => x.PageId,
                        principalTable: "CmsPages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CmsSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PageVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InternalName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Zone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Anchor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Variant = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsSections_CmsComponentDefinitions_ComponentDefinitionId",
                        column: x => x.ComponentDefinitionId,
                        principalTable: "CmsComponentDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CmsSections_CmsPageVersions_PageVersionId",
                        column: x => x.PageVersionId,
                        principalTable: "CmsPageVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CmsContentValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ValueType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LanguageId = table.Column<Guid>(type: "uuid", nullable: true),
                    TextValue = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    DecimalValue = table.Column<decimal>(type: "numeric", nullable: true),
                    BooleanValue = table.Column<bool>(type: "boolean", nullable: true),
                    DateTimeValue = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    JsonValue = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CmsContentValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CmsContentValues_CmsMediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "CmsMediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CmsContentValues_CmsSections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "CmsSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CmsContentValues_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CmsComponentDefinitions_Key_SchemaVersion",
                table: "CmsComponentDefinitions",
                columns: new[] { "Key", "SchemaVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsContentValues_LanguageId",
                table: "CmsContentValues",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsContentValues_MediaAssetId",
                table: "CmsContentValues",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsContentValues_SectionId_FieldKey_LanguageId",
                table: "CmsContentValues",
                columns: new[] { "SectionId", "FieldKey", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsMediaAssets_Checksum",
                table: "CmsMediaAssets",
                column: "Checksum");

            migrationBuilder.CreateIndex(
                name: "IX_CmsMediaAssets_Status",
                table: "CmsMediaAssets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CmsMediaAssets_StoragePath",
                table: "CmsMediaAssets",
                column: "StoragePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsMediaVariants_MediaAssetId_VariantKey",
                table: "CmsMediaVariants",
                columns: new[] { "MediaAssetId", "VariantKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsMediaVariants_StoragePath",
                table: "CmsMediaVariants",
                column: "StoragePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsMenuItems_MenuId_ParentMenuItemId_Position",
                table: "CmsMenuItems",
                columns: new[] { "MenuId", "ParentMenuItemId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsMenuItems_PageId",
                table: "CmsMenuItems",
                column: "PageId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsMenuItems_ParentMenuItemId",
                table: "CmsMenuItems",
                column: "ParentMenuItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsMenus_SiteId_Code",
                table: "CmsMenus",
                columns: new[] { "SiteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsMenus_SiteId_Placement",
                table: "CmsMenus",
                columns: new[] { "SiteId", "Placement" });

            migrationBuilder.CreateIndex(
                name: "IX_CmsPages_SiteId_Code",
                table: "CmsPages",
                columns: new[] { "SiteId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsPages_SiteId_Status",
                table: "CmsPages",
                columns: new[] { "SiteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CmsPageTranslations_LanguageId",
                table: "CmsPageTranslations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsPageTranslations_PageId_LanguageId",
                table: "CmsPageTranslations",
                columns: new[] { "PageId", "LanguageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsPageTranslations_SiteId_LanguageId_Slug",
                table: "CmsPageTranslations",
                columns: new[] { "SiteId", "LanguageId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsPageVersions_PageId_Status",
                table: "CmsPageVersions",
                columns: new[] { "PageId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CmsPageVersions_PageId_VersionNumber",
                table: "CmsPageVersions",
                columns: new[] { "PageId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsSections_ComponentDefinitionId_IsActive",
                table: "CmsSections",
                columns: new[] { "ComponentDefinitionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CmsSections_PageVersionId_Zone_Position",
                table: "CmsSections",
                columns: new[] { "PageVersionId", "Zone", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsSites_Code",
                table: "CmsSites",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CmsSites_DefaultCountryId",
                table: "CmsSites",
                column: "DefaultCountryId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsSites_DefaultLanguageId",
                table: "CmsSites",
                column: "DefaultLanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_CmsSites_Surface_DefaultCountryId",
                table: "CmsSites",
                columns: new[] { "Surface", "DefaultCountryId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CmsContentValues");

            migrationBuilder.DropTable(
                name: "CmsMediaVariants");

            migrationBuilder.DropTable(
                name: "CmsMenuItems");

            migrationBuilder.DropTable(
                name: "CmsPageTranslations");

            migrationBuilder.DropTable(
                name: "CmsSections");

            migrationBuilder.DropTable(
                name: "CmsMediaAssets");

            migrationBuilder.DropTable(
                name: "CmsMenus");

            migrationBuilder.DropTable(
                name: "CmsComponentDefinitions");

            migrationBuilder.DropTable(
                name: "CmsPageVersions");

            migrationBuilder.DropTable(
                name: "CmsPages");

            migrationBuilder.DropTable(
                name: "CmsSites");
        }
    }
}
