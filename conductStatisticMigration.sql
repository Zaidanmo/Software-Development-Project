BEGIN TRANSACTION;

ALTER TABLE "Articles" ADD "ReadCount" INTEGER NOT NULL DEFAULT 0;

ALTER TABLE "ArticleFavorites" ADD "Timestamp" TEXT NULL;

CREATE TABLE "SearchCount" (
    "KeywordId" TEXT NOT NULL CONSTRAINT "PK_SearchCount" PRIMARY KEY,
    "Count" INTEGER NOT NULL
);

--INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
--VALUES ('20250425102547_ConductStatisticMigration', '7.0.10');

COMMIT;

