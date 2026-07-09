-- Seed initial admin modules and roles.
-- This script mirrors the API DatabaseInitializer seed and is kept here
-- so the admin project documents the back-office structure.

INSERT INTO "AdminModules" ("Id", "Key", "Name", "Description", "DisplayOrder", "IsActive", "CreatedAt")
VALUES
    (gen_random_uuid(), 'Dashboard', 'Tableau de bord', 'Vue de synthese du back-office entreprise.', 10, true, now()),
    (gen_random_uuid(), 'CompanyApplications', 'Demandes entreprises', 'Validation des inscriptions, documents et activation des entreprises.', 20, true, now()),
    (gen_random_uuid(), 'Services', 'Services', 'Gestion du catalogue plat et des services proposes par les entreprises.', 30, true, now()),
    (gen_random_uuid(), 'Localization', 'Pays et traductions', 'Gestion des pays, langues et textes traduisibles.', 40, true, now()),
    (gen_random_uuid(), 'AdminAccess', 'Acces et roles', 'Gestion des roles, modules et permissions admin.', 50, true, now())
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AdminRoles" ("Id", "Name", "Description", "IsSystemRole", "IsActive", "CreatedAt")
VALUES
    (gen_random_uuid(), 'Super admin', 'Acces complet a tous les modules et aux permissions.', true, true, now()),
    (gen_random_uuid(), 'Validation entreprises', 'Peut traiter les demandes d''inscription entreprise.', true, true, now()),
    (gen_random_uuid(), 'Contenu et traduction', 'Peut gerer les textes, pays et langues.', true, true, now())
ON CONFLICT DO NOTHING;
