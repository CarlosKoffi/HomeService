INSERT INTO "MissionWorkflowSettings"
    ("Id", "Key", "Label", "Description", "Unit", "Value", "MinimumValue", "MaximumValue", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
VALUES
    ('4b41a1fd-6e27-4d0a-a824-8d5d91470011', 'customer_completion_validation_minutes', 'Validation fin de mission', 'Temps laisse au client pour valider la fin de mission avant validation et liberation automatiques du paiement.', 'minutes', 120, 5, 10080, 65, true, now(), now())
ON CONFLICT ("Key") DO UPDATE
SET "Label" = EXCLUDED."Label",
    "Description" = EXCLUDED."Description",
    "Unit" = EXCLUDED."Unit",
    "MinimumValue" = EXCLUDED."MinimumValue",
    "MaximumValue" = EXCLUDED."MaximumValue",
    "SortOrder" = EXCLUDED."SortOrder",
    "IsActive" = true,
    "UpdatedAt" = now();
