DO $$
DECLARE
    target_user_id uuid;
BEGIN
    SELECT "Id"
    INTO target_user_id
    FROM "CompanyPortalUsers"
    WHERE lower("Email") = 'bruce.carl@gmail.com'
    LIMIT 1;

    IF target_user_id IS NULL THEN
        RAISE EXCEPTION 'Aucun utilisateur portail entreprise trouve pour bruce.carl@gmail.com';
    END IF;

    UPDATE "CompanyPortalUsers"
    SET
        "PasswordHash" = 'sha256:C30298F471059178851309A9E569F6CE:8B859F3174F395AE2ADAE315BF67033C3FE8D30654C37E7540093A0E5EB81A8C',
        "IsActive" = true,
        "UpdatedAt" = now()
    WHERE "Id" = target_user_id;

    DELETE FROM "CompanyPortalSessions"
    WHERE "CompanyPortalUserId" = target_user_id;
END $$;
