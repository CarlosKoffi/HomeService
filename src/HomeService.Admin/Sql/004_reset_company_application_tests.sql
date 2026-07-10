-- Reset des demandes entreprises de test.
-- A utiliser uniquement si les fichiers physiques ont ete perdus apres redeploy sans volume persistant.
-- Le catalogue services, les traductions, les roles admin et les pays ne sont pas touches.

DELETE FROM "CompanyApplicationServices";
DELETE FROM "CompanyApplicationDocuments";
DELETE FROM "CompanyApplications";
