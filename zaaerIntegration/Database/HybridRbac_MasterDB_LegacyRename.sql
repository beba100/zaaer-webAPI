/*
*** لا تشغّل هذا السكربت على الإنتاج ***

كان يعيد تسمية Roles / UserRoles إلى *_Legacy لصالح RBAC.
المطلوب الآن: الإبقاء على الأسماء الأصلية لـ myMainProject وتشغيل:

  HybridRbac_MasterDB.sql          (ينشئ rbac_roles و user_roles منفصلة)
  HybridRbac_RestoreLegacyTableNames.sql   (إذا سبق تشغيل LegacyRename أو Synonyms)

إذا سبق تشغيل هذا الملف بالفعل، شغّل Restore فقط — لا تعِد LegacyRename.
*/

SET NOCOUNT ON;
RAISERROR(N'DEPRECATED: Use HybridRbac_RestoreLegacyTableNames.sql instead of LegacyRename. Script not executed.', 16, 1);
