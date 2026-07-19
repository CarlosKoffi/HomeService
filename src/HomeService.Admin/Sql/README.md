# SQL reference manifest

These scripts are reference and deployment-support scripts for the Kaza PostgreSQL schema.

EF Core migrations remain the primary schema source in:

`src/HomeService.Infrastructure/Data/Migrations`

Use these SQL files to inspect, review, or manually apply controlled changes when production operations require it.

Normal Coolify deployments do not run these files manually: the API applies EF Core migrations automatically at startup through `DatabaseInitializer`.

## Rules

- Keep one unique numeric prefix per script.
- Add new scripts at the end of the sequence.
- Prefer idempotent SQL when possible.
- Never add destructive data changes without an explicit backup or rollback note.
- When an EF migration changes schema, add or update the matching SQL reference script.

## Current order

| Order | Script | Purpose |
| --- | --- | --- |
| 001 | `001_create_homeservice_schema.sql` | Initial schema reference |
| 002 | `002_seed_admin_modules_roles.sql` | Initial admin modules and roles |
| 003 | `003_seed_initial_translations.sql` | Initial translation values |
| 004 | `004_reset_company_application_tests.sql` | Local/staging reset helper for company application tests |
| 005 | `005_add_notification_outbox.sql` | Notification outbox |
| 006 | `006_add_company_portal_employee_workspace.sql` | Company portal employee workspace |
| 007 | `007_add_country_branding.sql` | Country branding configuration |
| 008 | `008_add_company_assignment_mode.sql` | Company assignment mode |
| 009 | `009_add_provider_gender.sql` | Provider gender field |
| 010 | `010_add_provider_mobile_workspace.sql` | Provider mobile workspace |
| 011 | `011_move_pricing_to_services.sql` | Service-level pricing |
| 012 | `012_add_provider_service_price_tier.sql` | Provider service price tier |
| 013 | `013_add_audit_log_entries.sql` | Audit log table and indexes |
| 014 | `014_add_service_icons.sql` | Service icon names |
| 015 | `015_add_mission_confirmation_contact_release.sql` | Mission confirmation contact release |
| 016 | `016_add_provider_interim_affiliation_workflow.sql` | Provider interim affiliation workflow |
| 017 | `017_add_provider_portal_password.sql` | Provider portal password hash |
| 018 | `018_add_cms_foundation.sql` | CMS multi-site foundation |
| 019 | `019_seed_premium_company_landing_cms.sql` | Premium company landing CMS seed/update |
| 020 | `020_add_service_prestations.sql` | Service prestations catalog |
| 021 | `021_seed_company_cms_editorial_content.sql` | Company CMS editorial seed/update |
| 022 | `022_add_service_prestation_pricing_provider_selection.sql` | Service prestation pricing and provider selection |
| 023 | `023_seed_ci_service_catalog.sql` | Cote d'Ivoire service catalog seed/update |
| 024 | `024_reset_company_portal_password_bruce.sql` | Operational reset helper for one company portal password |
| 025 | `025_add_company_portal_activities.sql` | Company portal activity feed table |
| 026 | `026_add_company_profile_details.sql` | Company profile details editable from the company portal |
| 027 | `027_add_provider_email_for_company_employees.sql` | Provider email field for company employee management |
| 028 | `028_add_service_price_ranges_and_mission_quotes.sql` | Service/prestation price ranges and company mission quote fields |
| 029 | `029_update_translation_admin_labels.sql` | Simplify admin localization wording to translations only |
| 030 | `030_add_company_portal_notifications.sql` | Company portal notifications |
| 031 | `031_add_company_application_service_prestation_match.sql` | Company requested service matching to services/prestations |
| 032 | `032_add_company_interim_applications_flag.sql` | Company opt-in flag for interim applications |
| 033 | `033_backfill_interim_affiliation_requests.sql` | Backfill interim affiliation requests |
| 034 | `034_update_provider_interim_cms_steps.sql` | Provider interim CMS steps update |
| 035 | `035_add_mission_finance_foundation.sql` | Mission quote, attachments, payment milestones and commission foundation |

## Notes

`013_add_audit_log_entries.sql` and `014_add_service_icons.sql` are split for operational readability, while the EF migration `20260711223950_AddMissionConfirmationContactRelease` also contains those schema changes in the current history.
