/*
  ZATCA e-invoicing — phase 1 schema
  Run on each tenant hotel database.
  Environments: sandbox | simulation | production
*/

SET NOCOUNT ON;

-- zatca_details — default environment sandbox for new installs
IF COL_LENGTH('dbo.zatca_details', 'api_environment') IS NULL
BEGIN
    ALTER TABLE dbo.zatca_details ADD api_environment NVARCHAR(20) NOT NULL
        CONSTRAINT DF_zatca_details_api_environment DEFAULT (N'sandbox');
END;

IF COL_LENGTH('dbo.zatca_details', 'device_uuid') IS NULL
BEGIN
    ALTER TABLE dbo.zatca_details ADD device_uuid NVARCHAR(100) NULL;
END;

-- invoices — ZATCA submission state
IF COL_LENGTH('dbo.invoices', 'zatca_status') IS NULL
BEGIN
    ALTER TABLE dbo.invoices ADD zatca_status NVARCHAR(30) NOT NULL
        CONSTRAINT DF_invoices_zatca_status DEFAULT (N'pending');
END;

IF COL_LENGTH('dbo.invoices', 'zatca_icv') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_icv INT NULL;

IF COL_LENGTH('dbo.invoices', 'zatca_hash') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_hash NVARCHAR(512) NULL;

IF COL_LENGTH('dbo.invoices', 'zatca_qr') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_qr NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.invoices', 'zatca_response') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_response NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.invoices', 'zatca_profile') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_profile NVARCHAR(20) NULL;

IF COL_LENGTH('dbo.invoices', 'zatca_submission_mode') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_submission_mode NVARCHAR(20) NULL;

IF COL_LENGTH('dbo.invoices', 'zatca_retry_count') IS NULL
BEGIN
    ALTER TABLE dbo.invoices ADD zatca_retry_count INT NOT NULL
        CONSTRAINT DF_invoices_zatca_retry_count DEFAULT (0);
END;

IF COL_LENGTH('dbo.invoices', 'zatca_last_error') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_last_error NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.invoices', 'zatca_sent_at') IS NULL
    ALTER TABLE dbo.invoices ADD zatca_sent_at DATETIME2 NULL;

-- credit_notes — same ZATCA columns
IF COL_LENGTH('dbo.credit_notes', 'zatca_status') IS NULL
BEGIN
    ALTER TABLE dbo.credit_notes ADD zatca_status NVARCHAR(30) NOT NULL
        CONSTRAINT DF_credit_notes_zatca_status DEFAULT (N'pending');
END;

IF COL_LENGTH('dbo.credit_notes', 'zatca_icv') IS NULL
    ALTER TABLE dbo.credit_notes ADD zatca_icv INT NULL;

IF COL_LENGTH('dbo.credit_notes', 'zatca_hash') IS NULL
    ALTER TABLE dbo.credit_notes ADD zatca_hash NVARCHAR(512) NULL;

IF COL_LENGTH('dbo.credit_notes', 'zatca_qr') IS NULL
    ALTER TABLE dbo.credit_notes ADD zatca_qr NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.credit_notes', 'zatca_response') IS NULL
    ALTER TABLE dbo.credit_notes ADD zatca_response NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.credit_notes', 'zatca_profile') IS NULL
    ALTER TABLE dbo.credit_notes ADD zatca_profile NVARCHAR(20) NULL;

IF COL_LENGTH('dbo.credit_notes', 'zatca_submission_mode') IS NULL
    ALTER TABLE dbo.credit_notes ADD zatca_submission_mode NVARCHAR(20) NULL;

IF COL_LENGTH('dbo.credit_notes', 'zatca_retry_count') IS NULL
BEGIN
    ALTER TABLE dbo.credit_notes ADD zatca_retry_count INT NOT NULL
        CONSTRAINT DF_credit_notes_zatca_retry_count DEFAULT (0);
END;

IF COL_LENGTH('dbo.credit_notes', 'zatca_last_error') IS NULL
    ALTER TABLE dbo.credit_notes ADD zatca_last_error NVARCHAR(MAX) NULL;

IF COL_LENGTH('dbo.credit_notes', 'zatca_sent_at') IS NULL
    ALTER TABLE dbo.credit_notes ADD zatca_sent_at DATETIME2 NULL;

IF OBJECT_ID(N'dbo.zatca_devices', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.zatca_devices', 'compliance_request_id') IS NULL
    ALTER TABLE dbo.zatca_devices ADD compliance_request_id NVARCHAR(100) NULL;

-- zatca_devices — EGS unit per hotel (and environment)
IF OBJECT_ID(N'dbo.zatca_devices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.zatca_devices (
        device_id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        hotel_id            INT NOT NULL,
        device_uuid         NVARCHAR(100) NOT NULL,
        environment         NVARCHAR(20) NOT NULL CONSTRAINT DF_zatca_devices_environment DEFAULT (N'sandbox'),
        device_status       NVARCHAR(30) NOT NULL CONSTRAINT DF_zatca_devices_status DEFAULT (N'pending_onboarding'),
        csr_pem             NVARCHAR(MAX) NULL,
        private_key_encrypted VARBINARY(MAX) NULL,
        certificate_pem     NVARCHAR(MAX) NULL,
        compliance_request_id NVARCHAR(100) NULL,
        compliance_csid     NVARCHAR(MAX) NULL,
        compliance_secret   NVARCHAR(1000) NULL,
        production_csid     NVARCHAR(MAX) NULL,
        production_secret   NVARCHAR(1000) NULL,
        last_invoice_hash   NVARCHAR(512) NULL,
        last_icv            INT NOT NULL CONSTRAINT DF_zatca_devices_last_icv DEFAULT (0),
        created_at          DATETIME2 NOT NULL CONSTRAINT DF_zatca_devices_created DEFAULT (SYSUTCDATETIME()),
        updated_at          DATETIME2 NULL,
        CONSTRAINT UQ_zatca_devices_hotel_env_uuid UNIQUE (hotel_id, environment, device_uuid)
    );

    CREATE NONCLUSTERED INDEX IX_zatca_devices_hotel_env
        ON dbo.zatca_devices (hotel_id, environment);
END;

-- zatca_invoice_hash_history — PIH chain per device
IF OBJECT_ID(N'dbo.zatca_invoice_hash_history', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.zatca_invoice_hash_history (
        history_id      INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        device_id       INT NOT NULL,
        hotel_id        INT NOT NULL,
        document_kind   NVARCHAR(30) NOT NULL,
        document_id     INT NOT NULL,
        document_no     NVARCHAR(50) NOT NULL,
        icv             INT NOT NULL,
        invoice_hash    NVARCHAR(512) NOT NULL,
        zatca_uuid      NVARCHAR(100) NOT NULL,
        created_at      DATETIME2 NOT NULL CONSTRAINT DF_zatca_hash_history_created DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT FK_zatca_hash_history_device FOREIGN KEY (device_id)
            REFERENCES dbo.zatca_devices (device_id)
    );

    CREATE NONCLUSTERED INDEX IX_zatca_hash_history_device_icv
        ON dbo.zatca_invoice_hash_history (device_id, icv DESC);
END;

-- debit_notes
IF OBJECT_ID(N'dbo.debit_notes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.debit_notes (
        debit_note_id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        debit_note_no           NVARCHAR(50) NOT NULL,
        hotel_id                INT NOT NULL,
        invoice_id              INT NOT NULL,
        reservation_id          INT NULL,
        customer_id             INT NULL,
        order_id                INT NULL,
        debit_note_date         DATETIME2 NOT NULL,
        debit_note_date_hijri   NVARCHAR(20) NULL,
        subtotal                DECIMAL(12,2) NULL,
        vat_rate                DECIMAL(12,4) NULL,
        vat_amount              DECIMAL(12,2) NULL,
        lodging_tax_rate        DECIMAL(12,4) NULL,
        lodging_tax_amount      DECIMAL(12,2) NULL,
        debit_amount            DECIMAL(12,2) NOT NULL,
        original_invoice_amount DECIMAL(12,2) NULL,
        reason                  NVARCHAR(500) NOT NULL,
        debit_type              NVARCHAR(50) NOT NULL CONSTRAINT DF_debit_notes_type DEFAULT (N'adjustment'),
        notes                   NVARCHAR(1000) NULL,
        is_sent_zatca           BIT NOT NULL CONSTRAINT DF_debit_notes_is_sent DEFAULT (0),
        zatca_uuid              NVARCHAR(255) NULL,
        zatca_status            NVARCHAR(30) NOT NULL CONSTRAINT DF_debit_notes_zatca_status DEFAULT (N'pending'),
        zatca_icv               INT NULL,
        zatca_hash              NVARCHAR(512) NULL,
        zatca_qr                NVARCHAR(MAX) NULL,
        zatca_response          NVARCHAR(MAX) NULL,
        zatca_profile           NVARCHAR(20) NULL,
        zatca_submission_mode   NVARCHAR(20) NULL,
        zatca_retry_count       INT NOT NULL CONSTRAINT DF_debit_notes_zatca_retry DEFAULT (0),
        zatca_last_error        NVARCHAR(MAX) NULL,
        zatca_sent_at           DATETIME2 NULL,
        is_compliance_sample    BIT NOT NULL CONSTRAINT DF_debit_notes_compliance DEFAULT (0),
        created_by              INT NULL,
        created_at              DATETIME2 NOT NULL CONSTRAINT DF_debit_notes_created DEFAULT (SYSUTCDATETIME()),
        updated_at              DATETIME2 NULL,
        zaaer_id                INT NULL,
        CONSTRAINT UQ_debit_notes_no UNIQUE (debit_note_no, hotel_id)
    );

    CREATE NONCLUSTERED INDEX IX_debit_notes_hotel_status
        ON dbo.debit_notes (hotel_id, zatca_status);
END;

-- Backfill: legacy is_sent_zatca=1 without zatca_status
UPDATE dbo.invoices
SET zatca_status = N'reported'
WHERE is_sent_zatca = 1
  AND (zatca_status IS NULL OR zatca_status = N'pending');

UPDATE dbo.credit_notes
SET zatca_status = N'reported'
WHERE is_sent_zatca = 1
  AND (zatca_status IS NULL OR zatca_status = N'pending');

-- Widen token columns (ZATCA binarySecurityToken is a full base64 certificate)
IF OBJECT_ID(N'dbo.zatca_devices', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH('dbo.zatca_devices', 'compliance_csid') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN compliance_csid NVARCHAR(MAX) NULL;
    IF COL_LENGTH('dbo.zatca_devices', 'production_csid') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN production_csid NVARCHAR(MAX) NULL;
    IF COL_LENGTH('dbo.zatca_devices', 'compliance_secret') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN compliance_secret NVARCHAR(1000) NULL;
    IF COL_LENGTH('dbo.zatca_devices', 'production_secret') IS NOT NULL
        ALTER TABLE dbo.zatca_devices ALTER COLUMN production_secret NVARCHAR(1000) NULL;
END;

IF OBJECT_ID(N'dbo.zatca_details', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.zatca_details', 'device_common_name') IS NULL
    ALTER TABLE dbo.zatca_details ADD device_common_name NVARCHAR(200) NULL;

IF OBJECT_ID(N'dbo.zatca_details', N'U') IS NOT NULL
   AND COL_LENGTH('dbo.zatca_details', 'is_active') IS NULL
    ALTER TABLE dbo.zatca_details ADD is_active BIT NOT NULL
        CONSTRAINT DF_zatca_details_is_active DEFAULT (1);

PRINT N'ZatcaIntegration_Phase1.sql completed.';
