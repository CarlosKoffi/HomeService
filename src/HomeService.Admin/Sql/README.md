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

## Notes

`013_add_audit_log_entries.sql` and `014_add_service_icons.sql` are split for operational readability, while the EF migration `20260711223950_AddMissionConfirmationContactRelease` also contains those schema changes in the current history.
