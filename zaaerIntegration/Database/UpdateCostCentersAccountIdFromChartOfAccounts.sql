-- =============================================
-- Update AccountId in CostCenters based on name_ar matching with ChartOfAccounts
-- تحديث AccountId في CostCenters بناءً على مطابقة name_ar مع ChartOfAccounts
-- Purpose: Link each CostCenter to its cash box account by matching Arabic names
-- Author: System
-- Date: 2025-12-XX
-- =============================================
-- ⚠️ IMPORTANT: Run this on MASTER DATABASE ONLY (NOT tenant databases)
-- =============================================

-- USE [db32357_MasterDB]; -- Replace with your Master DB name
-- DO NOT run on tenant databases!
GO

PRINT '========================================';
PRINT 'Updating AccountId in CostCenters based on name_ar matching...';
PRINT 'Database: ' + DB_NAME();
PRINT '========================================';
GO

BEGIN TRY
    -- Step 1: Show current status before update
    PRINT '';
    PRINT 'Current CostCenters status (before update):';
    SELECT 
        cc.[id] AS CostCenterId,
        cc.[name_ar] AS CostCenterName,
        cc.[hotel_id] AS HotelId,
        cc.[AccountId] AS CurrentAccountId,
        CASE 
            WHEN cc.[AccountId] IS NULL THEN '❌ Not Set'
            ELSE '✅ Set'
        END AS Status
    FROM [dbo].[CostCenters] cc
    WHERE cc.[is_active] = 1
    ORDER BY cc.[hotel_id];
    
    PRINT '';
    PRINT 'Available Cash Box Accounts in ChartOfAccounts:';
    SELECT 
        coa.[id] AS AccountId,
        coa.[code] AS AccountCode,
        coa.[name_ar] AS AccountNameAr,
        coa.[name_en] AS AccountNameEn,
        coa.[used_in_payment],
        coa.[is_active]
    FROM [dbo].[ChartOfAccounts] coa
    WHERE coa.[name_ar] LIKE '%صندوق%'
    AND coa.[is_active] = 1
    ORDER BY coa.[name_ar];
    
    PRINT '';
    PRINT '========================================';
    PRINT 'Starting update process...';
    PRINT '========================================';
    
    -- Debug: Show sample matching attempts
    PRINT '';
    PRINT 'Sample matching attempts (first 5 CostCenters):';
    SELECT TOP 5
        cc.[id] AS CostCenterId,
        cc.[name_ar] AS CostCenterName,
        N'صندوق ' + cc.[name_ar] AS ExpectedExactMatch,
        (
            SELECT TOP 1 coa.[name_ar] + ' (ID: ' + CAST(coa.[id] AS NVARCHAR(10)) + ')'
            FROM [dbo].[ChartOfAccounts] coa
            WHERE coa.[name_ar] = N'صندوق ' + cc.[name_ar]
            AND coa.[is_active] = 1
            AND coa.[used_in_payment] = 1
        ) AS FoundExactMatch,
        (
            SELECT TOP 1 coa.[name_ar] + ' (ID: ' + CAST(coa.[id] AS NVARCHAR(10)) + ')'
            FROM [dbo].[ChartOfAccounts] coa
            WHERE coa.[name_ar] LIKE N'صندوق%' + cc.[name_ar] + '%'
            AND coa.[is_active] = 1
            AND coa.[used_in_payment] = 1
        ) AS FoundLikeMatch,
        (
            SELECT COUNT(*)
            FROM [dbo].[ChartOfAccounts] coa
            WHERE coa.[name_ar] LIKE '%صندوق%'
            AND coa.[name_ar] LIKE '%' + cc.[name_ar] + '%'
            AND coa.[is_active] = 1
            AND coa.[used_in_payment] = 1
        ) AS TotalPossibleMatches
    FROM [dbo].[CostCenters] cc
    WHERE cc.[is_active] = 1
    ORDER BY cc.[id];
    
    DECLARE @UpdatedRows INT = 0;
    
    -- Strategy 1: Exact match - "صندوق " + CostCenter.name_ar
    -- Example: "الدمام 1" → "صندوق الدمام 1"
    UPDATE cc
    SET cc.[AccountId] = coa.[id]
    FROM [dbo].[CostCenters] cc
    INNER JOIN [dbo].[ChartOfAccounts] coa 
        ON (
            coa.[name_ar] = N'صندوق ' + cc.[name_ar]
            OR coa.[name_ar] = N'صندوق' + cc.[name_ar]  -- Without space
        )
        AND coa.[name_ar] LIKE '%صندوق%'
        AND coa.[is_active] = 1
        AND coa.[used_in_payment] = 1
    WHERE cc.[is_active] = 1
    AND (cc.[AccountId] IS NULL OR cc.[AccountId] != coa.[id]);
    
    SET @UpdatedRows = @@ROWCOUNT;
    PRINT '✅ Updated ' + CAST(@UpdatedRows AS NVARCHAR(10)) + ' CostCenters with exact match (صندوق + name_ar)';
    
    -- Strategy 2: LIKE match - "صندوق%" + CostCenter.name_ar + "%"
    -- Example: "الدمام 1" → matches "صندوق الدمام 1" or "صندوق الدمام ٤"
    DECLARE @LikeMatchRows INT = 0;
    
    UPDATE cc
    SET cc.[AccountId] = (
        SELECT TOP 1 coa.[id]
        FROM [dbo].[ChartOfAccounts] coa
        WHERE (
            coa.[name_ar] LIKE N'صندوق%' + cc.[name_ar] + '%'
            OR coa.[name_ar] LIKE N'صندوق%' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                cc.[name_ar], '0', '٠'), '1', '١'), '2', '٢'), '3', '٣'), '4', '٤'), '5', '٥'), '6', '٦'), '7', '٧'), '8', '٨'), '9', '٩') + '%'
            OR coa.[name_ar] LIKE N'صندوق%' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                cc.[name_ar], '٠', '0'), '١', '1'), '٢', '2'), '٣', '3'), '٤', '4'), '٥', '5'), '٦', '6'), '٧', '7'), '٨', '8'), '٩', '9') + '%'
        )
        AND coa.[name_ar] LIKE '%صندوق%'
        AND coa.[is_active] = 1
        AND coa.[used_in_payment] = 1
        ORDER BY 
            -- Priority 1: Exact match (already handled above, but check again)
            CASE WHEN coa.[name_ar] = N'صندوق ' + cc.[name_ar] OR coa.[name_ar] = N'صندوق' + cc.[name_ar] THEN 1
                 -- Priority 2: Starts with "صندوق " + name_ar
                 WHEN coa.[name_ar] LIKE N'صندوق ' + cc.[name_ar] + '%' THEN 2
                 -- Priority 3: Contains name_ar
                 WHEN coa.[name_ar] LIKE N'صندوق%' + cc.[name_ar] + '%' THEN 3
                 ELSE 4
            END,
            coa.[id]
    )
    FROM [dbo].[CostCenters] cc
    WHERE cc.[is_active] = 1
    AND cc.[AccountId] IS NULL
    AND EXISTS (
        SELECT 1
        FROM [dbo].[ChartOfAccounts] coa
        WHERE (
            coa.[name_ar] LIKE N'صندوق%' + cc.[name_ar] + '%'
            OR coa.[name_ar] LIKE N'صندوق%' + REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                cc.[name_ar], '0', '٠'), '1', '١'), '2', '٢'), '3', '٣'), '4', '٤'), '5', '٥'), '6', '٦'), '7', '٧'), '8', '٨'), '9', '٩') + '%'
        )
        AND coa.[name_ar] LIKE '%صندوق%'
        AND coa.[is_active] = 1
        AND coa.[used_in_payment] = 1
    );
    
    SET @LikeMatchRows = @@ROWCOUNT;
    PRINT '✅ Updated ' + CAST(@LikeMatchRows AS NVARCHAR(10)) + ' CostCenters with LIKE match (صندوق% + name_ar + %)';
    
    -- Strategy 3: Match by city name - extract city name and find closest match
    -- Extract city name from CostCenter (remove trailing numbers) and match
    -- Example: "الدمام 1" → extract "الدمام" → match "صندوق الدمام 1" or "صندوق الدمام ٤"
    
    DECLARE @CityMatchRows INT = 0;
    
    -- Use CTE to extract city names and find best matches
    WITH CostCenterCities AS (
        SELECT 
            cc.[id] AS CostCenterId,
            cc.[name_ar] AS CostCenterName,
            cc.[hotel_id],
            -- Extract city name: remove trailing space and number
            -- Example: "الدمام 1" → "الدمام"
            LTRIM(RTRIM(
                CASE 
                    -- If name ends with space + number (e.g., "الدمام 1")
                    WHEN cc.[name_ar] LIKE '% [0-9]' OR cc.[name_ar] LIKE '% [٠-٩]'
                    THEN LEFT(cc.[name_ar], LEN(cc.[name_ar]) - CHARINDEX(' ', REVERSE(cc.[name_ar])) + 1)
                    -- If name ends with number without space (e.g., "الدمام1")
                    WHEN RIGHT(cc.[name_ar], 1) LIKE '[0-9]' OR RIGHT(cc.[name_ar], 1) LIKE '[٠-٩]'
                    THEN LEFT(cc.[name_ar], LEN(cc.[name_ar]) - 1)
                    ELSE cc.[name_ar]
                END
            )) AS CityName,
            cc.[name_ar] AS FullName
        FROM [dbo].[CostCenters] cc
        WHERE cc.[is_active] = 1
        AND cc.[AccountId] IS NULL
    ),
    BestMatches AS (
        SELECT 
            ccc.CostCenterId,
            ccc.CostCenterName,
            ccc.hotel_id,
            ccc.CityName,
            ccc.FullName,
            (
                SELECT TOP 1 coa.[id]
                FROM [dbo].[ChartOfAccounts] coa
                WHERE coa.[name_ar] LIKE N'صندوق%' + ccc.CityName + '%'
                AND coa.[name_ar] LIKE '%صندوق%'
                AND coa.[is_active] = 1
                AND coa.[used_in_payment] = 1
                ORDER BY 
                    -- Priority 1: Exact match with full name (صندوق + FullName)
                    CASE WHEN coa.[name_ar] = N'صندوق ' + ccc.FullName THEN 1
                         -- Priority 2: Contains full name (may have different number)
                         WHEN coa.[name_ar] LIKE N'صندوق%' + ccc.FullName + '%' THEN 2
                         -- Priority 3: Contains city name (closest match by city)
                         WHEN coa.[name_ar] LIKE N'صندوق%' + ccc.CityName + '%' THEN 3
                         ELSE 4
                    END,
                    coa.[id]
            ) AS MatchedAccountId
        FROM CostCenterCities ccc
    )
    UPDATE cc
    SET cc.[AccountId] = bm.MatchedAccountId
    FROM [dbo].[CostCenters] cc
    INNER JOIN BestMatches bm ON cc.[id] = bm.CostCenterId
    WHERE bm.MatchedAccountId IS NOT NULL;
    
    SET @CityMatchRows = @@ROWCOUNT;
    PRINT '✅ Updated ' + CAST(@CityMatchRows AS NVARCHAR(10)) + ' CostCenters with city name matching';
    
    -- Step 3: Show final results
    PRINT '';
    PRINT '========================================';
    PRINT 'Final Results:';
    PRINT '========================================';
    
    SELECT 
        cc.[id] AS CostCenterId,
        cc.[name_ar] AS CostCenterName,
        cc.[hotel_id] AS HotelId,
        cc.[AccountId] AS AccountId,
        coa.[name_ar] AS CashBoxAccountName,
        coa.[code] AS AccountCode,
        CASE 
            WHEN cc.[AccountId] IS NULL THEN '❌ Not Set'
            WHEN coa.[id] IS NULL THEN '⚠️ Account Not Found'
            ELSE '✅ Set'
        END AS Status
    FROM [dbo].[CostCenters] cc
    LEFT JOIN [dbo].[ChartOfAccounts] coa ON cc.[AccountId] = coa.[id]
    WHERE cc.[is_active] = 1
    ORDER BY cc.[hotel_id];
    
    -- Summary
    DECLARE @TotalRows INT;
    DECLARE @SetRows INT;
    DECLARE @NotSetRows INT;
    
    SELECT @TotalRows = COUNT(*) FROM [dbo].[CostCenters] WHERE [is_active] = 1;
    SELECT @SetRows = COUNT(*) FROM [dbo].[CostCenters] WHERE [is_active] = 1 AND [AccountId] IS NOT NULL;
    SELECT @NotSetRows = COUNT(*) FROM [dbo].[CostCenters] WHERE [is_active] = 1 AND [AccountId] IS NULL;
    
    PRINT '';
    PRINT '========================================';
    PRINT 'Summary:';
    PRINT '   Total Active CostCenters: ' + CAST(@TotalRows AS NVARCHAR(10));
    PRINT '   AccountId Set: ' + CAST(@SetRows AS NVARCHAR(10)) + ' ✅';
    PRINT '   AccountId Not Set: ' + CAST(@NotSetRows AS NVARCHAR(10)) + ' ❌';
    PRINT '========================================';
    
    IF @NotSetRows > 0
    BEGIN
        PRINT '';
        PRINT '⚠️ WARNING: Some CostCenters still do not have AccountId set.';
        PRINT 'Please manually update them or check if matching cash box accounts exist in ChartOfAccounts.';
        PRINT '';
        PRINT 'CostCenters without AccountId:';
        SELECT 
            cc.[id] AS CostCenterId,
            cc.[name_ar] AS CostCenterName,
            cc.[hotel_id] AS HotelId,
            'Suggested matches:' AS Suggestion,
            (
                SELECT TOP 3 coa.[id] AS AccountId, coa.[name_ar] AS AccountName
                FROM [dbo].[ChartOfAccounts] coa
                WHERE coa.[name_ar] LIKE '%صندوق%'
                AND coa.[is_active] = 1
                AND coa.[used_in_payment] = 1
                ORDER BY 
                    CASE WHEN coa.[name_ar] LIKE N'%' + cc.[name_ar] + '%' THEN 1 ELSE 2 END,
                    coa.[name_ar]
                FOR JSON PATH
            ) AS SuggestedAccounts
        FROM [dbo].[CostCenters] cc
        WHERE cc.[is_active] = 1
        AND cc.[AccountId] IS NULL
        ORDER BY cc.[hotel_id];
    END
    
    PRINT '';
    PRINT '✅ Update process completed successfully!';
    PRINT '========================================';
    
END TRY
BEGIN CATCH
    PRINT '';
    PRINT '========================================';
    PRINT '❌ ERROR occurred:';
    PRINT '   Error Message: ' + ERROR_MESSAGE();
    PRINT '   Error Number: ' + CAST(ERROR_NUMBER() AS NVARCHAR(10));
    PRINT '   Error Line: ' + CAST(ERROR_LINE() AS NVARCHAR(10));
    PRINT '========================================';
END CATCH
GO

-- =============================================
-- USAGE NOTES:
-- =============================================
-- 1. ⚠️ Run this script on MASTER DATABASE ONLY
-- 2. ⚠️ DO NOT run on tenant databases!
-- 3. This script matches CostCenters.name_ar with ChartOfAccounts.name_ar
-- 4. Matching logic:
--    - Exact match: "الدمام 1" → "صندوق الدمام 1"
--    - City match: "الدمام 1" → "صندوق الدمام ٤" (same city, different number)
-- 5. After running, verify results and manually update any unmatched CostCenters
-- =============================================
