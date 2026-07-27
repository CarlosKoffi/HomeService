-- Seed initial admin modules and roles.
-- This script mirrors the API DatabaseInitializer seed and is kept here
-- so the admin project documents the back-office structure.

INSERT INTO "AdminModules" ("Id", "Key", "Name", "Description", "DisplayOrder", "IsActive", "CreatedAt")
VALUES
    (gen_random_uuid(), 'Dashboard', 'Tableau de bord', 'Vue de synthese du back-office.', 10, true, now()),
    (gen_random_uuid(), 'CompanyApplications', 'Demandes entreprises', 'Validation des inscriptions, documents et activation des entreprises.', 20, true, now()),
    (gen_random_uuid(), 'CompanyManagement', 'Entreprises', 'Suivi des entreprises, prestataires, documents, missions et notifications.', 30, true, now()),
    (gen_random_uuid(), 'ProviderReview', 'Prestataires', 'Consultation, validation et suspension des prestataires.', 40, true, now()),
    (gen_random_uuid(), 'Services', 'Services et prestations', 'Gestion du catalogue, des propositions entreprises et des chiffres par service.', 50, true, now()),
    (gen_random_uuid(), 'Missions', 'Missions', 'Suivi des missions, affectations, litiges, annulations et journal.', 60, true, now()),
    (gen_random_uuid(), 'MissionSettings', 'Parametres missions', 'Configuration des commissions, delais et regles operationnelles des missions.', 70, true, now()),
    (gen_random_uuid(), 'Payments', 'Encaissements', 'Pilotage des paiements, commissions et reversements.', 80, true, now()),
    (gen_random_uuid(), 'Notifications', 'Notifications', 'Suivi des messages portail, application, email, WhatsApp et modeles.', 90, true, now()),
    (gen_random_uuid(), 'Cms', 'CMS', 'Edition des contenus, images et textes des sites publics.', 100, true, now()),
    (gen_random_uuid(), 'Localization', 'Traductions', 'Gestion des langues et textes traduisibles.', 110, true, now()),
    (gen_random_uuid(), 'ContactRequests', 'Demandes contact', 'Traitement des formulaires de contact publics.', 120, true, now()),
    (gen_random_uuid(), 'Audit', 'Journal', 'Consultation des actions sensibles et traces metier.', 130, true, now()),
    (gen_random_uuid(), 'AdminAccess', 'Acces et roles', 'Gestion des roles, modules et permissions admin.', 140, true, now())
ON CONFLICT ("Key") DO NOTHING;

INSERT INTO "AdminRoles" ("Id", "Name", "Description", "IsSystemRole", "IsActive", "CreatedAt")
VALUES
    (gen_random_uuid(), 'Super admin', 'Acces complet a tous les modules et aux permissions.', true, true, now()),
    (gen_random_uuid(), 'Validation entreprises', 'Peut traiter les demandes d''inscription entreprise.', true, true, now()),
    (gen_random_uuid(), 'Contenu et traduction', 'Peut gerer les textes et les langues.', true, true, now())
ON CONFLICT DO NOTHING;
