-- Tenant DB: reservation notes (thread per reservation).
-- reservation_id MUST be the global integration id: reservations.zaaer_id when set (> 0),
-- otherwise reservations.reservation_id (same convention as discounts / companions).
-- No FK on reservation_id (integration ids may not be unique at DB level).

IF OBJECT_ID(N'dbo.reservation_notes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[reservation_notes] (
        [note_id]                    INT IDENTITY(1, 1) NOT NULL,
        [hotel_id]                   INT NOT NULL,
        [reservation_id]             INT NOT NULL,
        [note_type]                  NVARCHAR(20) NOT NULL,
        [note_text]                  NVARCHAR(2000) NOT NULL,
        [attachment_path]            NVARCHAR(500) NULL,
        [attachment_original_name]   NVARCHAR(255) NULL,
        [attachment_content_type]    NVARCHAR(100) NULL,
        [attachment_file_size]       BIGINT NULL,
        [created_by_user_id]         INT NULL,
        [created_by_name]            NVARCHAR(200) NULL,
        [created_at]                 DATETIME2(0) NOT NULL,
        [updated_at]                 DATETIME2(0) NULL,
        CONSTRAINT [PK_reservation_notes] PRIMARY KEY CLUSTERED ([note_id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_reservation_notes_reservation]
        ON [dbo].[reservation_notes] ([hotel_id] ASC, [reservation_id] ASC, [created_at] ASC);
END
GO
