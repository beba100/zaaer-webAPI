/*
*** لا تشغّل هذا السكربت ***

المرادفات (Synonyms) تكسر التطبيق القديم عندما يوجد جدول RBAC بنفس الاسم المنطقي (Roles/roles).

استخدم بدلاً منه:
  HybridRbac_RestoreLegacyTableNames.sql
*/

SET NOCOUNT ON;
RAISERROR(N'DEPRECATED: Use HybridRbac_RestoreLegacyTableNames.sql. Synonyms are not used.', 16, 1);
