ALTER TABLE "Companies"
ADD COLUMN IF NOT EXISTS "AssignmentMode" character varying(32) NOT NULL DEFAULT 'SelfManaged';
