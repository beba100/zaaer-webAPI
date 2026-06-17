-- ============================================
-- Script: Add Account 234 (بنك البلاد-حوالات بنكية) to ChartOfAccounts
-- Description: Adds Account 234 for Bank Transfer payment method
-- Account: بنك البلاد-حوالات بنكية (Bank Transfer)
-- ============================================

USE [db32357_MasterDB];  -- ⚠️ Replace with your actual master database name
GO

BEGIN TRANSACTION;

BEGIN TRY
    -- Check if Account 234 already exists
    IF EXISTS (SELECT 1 FROM [dbo].[ChartOfAccounts] WHERE [id] = 234)
    BEGIN
        PRINT N'⚠️ Account 234 already exists in ChartOfAccounts. Skipping insertion.';
        
        -- Optionally update it if needed
        UPDATE [dbo].[ChartOfAccounts]
        SET 
            [code] = '012-2-1-1-4',
            [name_ar] = N'بنك البلاد-حوالات بنكية',
            [name_en] = 'Bilad - Bank Transfer',
            [parent_id] = 28,
            [subcategory_id] = 9,
            [level] = 2,
            [path] = '012-2-1-1-4',
            [account_type] = 'Revenue',
            [default_transaction_type] = 'debit',
            [currency_code] = 'SAR',
            [is_main] = 0,
            [is_active] = 1,
            [is_system] = 0,
            [used_in_payment] = 0,
            [current_total_debit] = 0.00,
            [current_total_credit] = 0.00,
            [current_balance] = 0.00,
            [updated_by] = 1,
            [updated_at] = GETDATE()
        WHERE [id] = 234;
        
        PRINT N'✅ Account 234 updated successfully.';
    END
    ELSE
    BEGIN
        -- Insert Account 234
        SET IDENTITY_INSERT [dbo].[ChartOfAccounts] ON;

        INSERT INTO [dbo].[ChartOfAccounts]
        (
            [id],
            [code],
            [name_ar],
            [name_en],
            [parent_id],
            [subcategory_id],
            [level],
            [path],
            [account_type],
            [default_transaction_type],
            [currency_code],
            [is_main],
            [is_active],
            [is_system],
            [used_in_payment],
            [current_total_debit],
            [current_total_credit],
            [current_balance],
            [description],
            [notes],
            [created_by],
            [updated_by],
            [created_at],
            [updated_at],
            [deleted_at]
        )
        VALUES
        (
            234,
            '012-2-1-1-4',
            N'بنك البلاد-حوالات بنكية',
            'Bilad - Bank Transfer',
            28,
            9,
            2,
            '012-2-1-1-4',
            'Revenue',
            'debit',
            'SAR',
            0,
            1,
            0,
            0,
            0.00,
            0.00,
            0.00,
            NULL,
            NULL,
            3,
            1,
            GETDATE(),
            GETDATE(),
            NULL
        );

        SET IDENTITY_INSERT [dbo].[ChartOfAccounts] OFF;
        
        PRINT N'✅ Account 234 inserted successfully.';
    END

    -- Verify the account was added/updated
    SELECT 
        [id],
        [code],
        [name_ar],
        [name_en],
        [is_active],
        [account_type]
    FROM [dbo].[ChartOfAccounts]
    WHERE [id] = 234;

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

