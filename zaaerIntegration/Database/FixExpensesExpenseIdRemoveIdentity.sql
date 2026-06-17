/*
  TARGET STATE (all tenant DBs — one database per hotel, e.g. Dammam1, Dammam3, Jizan3):

    expenses.expense_id  BIGINT NOT NULL   — NOT IDENTITY (manual Master ZaaerId)
    expenses.expense_no  NVARCHAR(20)      — EXP_0001 from Master numbering
    expenses.expense_seq INT               — numeric counter per hotel

  WHY:
    PMS create assigns expense_id from Master GetNextBusinessIdentity (ZaaerId).
    If expense_id is IDENTITY, SQL Server error 544 blocks explicit inserts.

  STEP 1 — Run VerifyExpensesExpenseIdManual.sql on EACH tenant database.
           status must be OK before you rely on manual ids without IDENTITY_INSERT.

  STEP 2 — If is_identity = 1, remove IDENTITY (SSMS recommended for production):

    A) Table Designer (safest for DBAs):
       - Right-click dbo.expenses > Design
       - Select expense_id > Column Properties > Identity Specification = No
       - Save (SSMS may rebuild table — backup first)

    B) Or script rebuild (outline — adjust FK names per tenant):

       -- BACKUP FIRST
       -- 1) Script all FKs referencing expenses.expense_id and drop them
       -- 2) Create expenses_new with same columns but expense_id BIGINT NOT NULL without IDENTITY
       -- 3) SET IDENTITY_INSERT dbo.expenses ON;
       --    INSERT INTO dbo.expenses_new (expense_id, ...) SELECT expense_id, ... FROM dbo.expenses;
       --    SET IDENTITY_INSERT dbo.expenses OFF;
       -- 4) Drop dbo.expenses; EXEC sp_rename 'expenses_new', 'expenses';
       -- 5) Recreate PK on expense_id and all FKs

  STEP 3 — Re-run VerifyExpensesExpenseIdManual.sql → status OK.

  NOTE: Each hotelCode (Dammam1, Dammam3, …) has its OWN tenant database.
        Fixing Dammam1 does NOT fix Dammam3.
*/

SET NOCOUNT ON;

SELECT
    DB_NAME() AS tenant_database,
    c.is_identity AS expense_id_is_identity,
    CASE c.is_identity
        WHEN 1 THEN N'ACTION REQUIRED — remove IDENTITY from expense_id'
        ELSE N'OK — manual expense_id / Master ZaaerId'
    END AS recommendation
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
WHERE t.name = N'expenses'
  AND c.name = N'expense_id';

GO
