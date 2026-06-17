-- Add corporate number (cor_no) to corporate_customers
-- Safe to run multiple times

IF COL_LENGTH('dbo.corporate_customers', 'cor_no') IS NULL
BEGIN
    ALTER TABLE dbo.corporate_customers
    ADD cor_no NVARCHAR(50) NULL;
END

