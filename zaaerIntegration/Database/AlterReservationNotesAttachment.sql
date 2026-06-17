-- Run on tenant DB if reservation_notes already exists without attachment columns.

IF OBJECT_ID(N'dbo.reservation_notes', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.reservation_notes', N'attachment_path') IS NULL
    BEGIN
        ALTER TABLE [dbo].[reservation_notes] ADD [attachment_path] NVARCHAR(500) NULL;
    END

    IF COL_LENGTH(N'dbo.reservation_notes', N'attachment_original_name') IS NULL
    BEGIN
        ALTER TABLE [dbo].[reservation_notes] ADD [attachment_original_name] NVARCHAR(255) NULL;
    END

    IF COL_LENGTH(N'dbo.reservation_notes', N'attachment_content_type') IS NULL
    BEGIN
        ALTER TABLE [dbo].[reservation_notes] ADD [attachment_content_type] NVARCHAR(100) NULL;
    END

    IF COL_LENGTH(N'dbo.reservation_notes', N'attachment_file_size') IS NULL
    BEGIN
        ALTER TABLE [dbo].[reservation_notes] ADD [attachment_file_size] BIGINT NULL;
    END
END
GO
