-- ============================================
-- Script: Normalize Bank Names in payment_receipts table
-- Description: Converts Arabic bank names to English equivalents
-- - "بنك البلاد" → "bilad"
-- - "بنك الرياض" → "riyad"
-- - "نظام المصروفات" → "expense"
-- ============================================

USE [YourDatabaseName];  -- ⚠️ Replace with your actual database name
GO

BEGIN TRANSACTION;

BEGIN TRY
    -- Update "بنك البلاد" → "bilad"
    UPDATE [dbo].[payment_receipts]
    SET [bank_name] = 'bilad'
    WHERE [bank_name] IS NOT NULL
      AND (
          [bank_name] = N'بنك البلاد'
          OR [bank_name] LIKE N'%بنك البلاد%'
          OR [bank_name] LIKE N'%البلاد%'
      )
      AND [bank_name] NOT IN ('bilad', 'riyad', 'expense');  -- Skip already normalized values

    PRINT N'✅ Updated "بنك البلاد" → "bilad": ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' rows';

    -- Update "بنك الرياض" → "riyad"
    UPDATE [dbo].[payment_receipts]
    SET [bank_name] = 'riyad'
    WHERE [bank_name] IS NOT NULL
      AND (
          [bank_name] = N'بنك الرياض'
          OR [bank_name] LIKE N'%بنك الرياض%'
          OR [bank_name] LIKE N'%الرياض%'
      )
      AND [bank_name] NOT IN ('bilad', 'riyad', 'expense');  -- Skip already normalized values

    PRINT N'✅ Updated "بنك الرياض" → "riyad": ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' rows';

    -- Update "نظام المصروفات" → "expense"
    UPDATE [dbo].[payment_receipts]
    SET [bank_name] = 'expense'
    WHERE [bank_name] IS NOT NULL
      AND (
          [bank_name] = N'نظام المصروفات'
          OR [bank_name] LIKE N'%نظام المصروفات%'
          OR [bank_name] LIKE N'%المصروفات%'
      )
      AND [bank_name] NOT IN ('bilad', 'riyad', 'expense');  -- Skip already normalized values

    PRINT N'✅ Updated "نظام المصروفات" → "expense": ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' rows';

    -- Show summary
    SELECT 
        [bank_name],
        COUNT(*) AS [Count]
    FROM [dbo].[payment_receipts]
    WHERE [bank_name] IS NOT NULL
    GROUP BY [bank_name]
    ORDER BY [Count] DESC;

    COMMIT TRANSACTION;
    PRINT N'✅ Transaction committed successfully.';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'❌ Transaction rolled back due to error.';
    END

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    PRINT N'❌ Error: ' + @ErrorMessage;
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;

GO

