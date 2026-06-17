IF COL_LENGTH('dbo.taxes', 'tax_included') IS NULL
BEGIN
    ALTER TABLE dbo.taxes
    ADD tax_included bit NOT NULL
        CONSTRAINT DF_taxes_tax_included DEFAULT (1);
END;
GO

UPDATE dbo.taxes
SET tax_included = 1
WHERE tax_included IS NULL;
GO
