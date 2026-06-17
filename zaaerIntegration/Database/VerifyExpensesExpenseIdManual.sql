/*
  Verify tenant dbo.expenses identity columns vs PMS / integration design.

  Expected (target state):
    - expense_id       BIGINT NOT NULL, NOT IDENTITY — manual Master ZaaerId
    - expense_no       NVARCHAR(20) from GetNextBusinessIdentity (e.g. EXP_0001)
    - expense_seq      INT document counter per hotel
    - local_expense_id INT manual Max+1 (NOT IDENTITY)
    - old_expense_id   INT legacy — often still IDENTITY on tenant DBs (DB-generated; app must NOT insert)

  Run on each tenant DB (e.g. db54636_Dammam1, db54638_Dammam3).
  Each hotelCode points to its own database — verify ALL production tenant DBs.
*/

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.expenses', N'U') IS NULL
BEGIN
    RAISERROR(N'Table dbo.expenses not found.', 16, 1);
    RETURN;
END

-- All identity columns on expenses (usually only old_expense_id)
SELECT
    column_name = c.name,
    is_identity = c.is_identity,
    is_nullable = c.is_nullable,
    data_type = ty.name
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
INNER JOIN sys.tables t ON c.object_id = t.object_id
WHERE t.name = N'expenses'
  AND c.is_identity = 1
ORDER BY c.column_id;

DECLARE @ExpenseIdIdentity BIT;
DECLARE @OldExpenseIdIdentity BIT;
DECLARE @ExpenseIdNullable BIT;
DECLARE @ExpenseIdType NVARCHAR(128);

SELECT
    @ExpenseIdIdentity = MAX(CASE WHEN c.name = N'expense_id' THEN c.is_identity END),
    @OldExpenseIdIdentity = MAX(CASE WHEN c.name = N'old_expense_id' THEN c.is_identity END),
    @ExpenseIdNullable = MAX(CASE WHEN c.name = N'expense_id' THEN c.is_nullable END),
    @ExpenseIdType = MAX(CASE WHEN c.name = N'expense_id' THEN ty.name END)
FROM sys.columns c
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
INNER JOIN sys.tables t ON c.object_id = t.object_id
WHERE t.name = N'expenses'
  AND c.name IN (N'expense_id', N'old_expense_id');

IF @ExpenseIdIdentity IS NULL
BEGIN
    RAISERROR(N'Column expense_id not found on dbo.expenses.', 16, 1);
    RETURN;
END

SELECT
    expense_id_is_identity = @ExpenseIdIdentity,
    old_expense_id_is_identity = ISNULL(@OldExpenseIdIdentity, 0),
    expense_id_is_nullable = @ExpenseIdNullable,
    expense_id_type = @ExpenseIdType,
    expense_id_status = CASE
        WHEN @ExpenseIdIdentity = 1 THEN N'FAIL — expense_id must NOT be IDENTITY (Master ZaaerId is inserted manually)'
        WHEN @ExpenseIdNullable = 1 THEN N'WARN — expense_id should be NOT NULL'
        WHEN @ExpenseIdType <> N'bigint' THEN N'WARN — expense_id should be bigint'
        ELSE N'OK — expense_id ready for manual Master ZaaerId'
    END,
    old_expense_id_status = CASE
        WHEN @OldExpenseIdIdentity = 1 THEN N'OK — old_expense_id is IDENTITY (app must not set OldExpenseId on insert)'
        ELSE N'INFO — old_expense_id is not IDENTITY on this tenant'
    END;

IF @ExpenseIdIdentity = 1
BEGIN
    RAISERROR(
        N'expense_id is IDENTITY. Legacy tenants may need IDENTITY_INSERT; prefer removing IDENTITY from expense_id.',
        16, 1);
END

GO
