debit_note_id	int	Unchecked
debit_note_no	nvarchar(50)	Unchecked
hotel_id	int	Unchecked
invoice_id	int	Unchecked
reservation_id	int	Checked
customer_id	int	Checked
order_id	int	Checked
debit_note_date	datetime2(7)	Unchecked
debit_note_date_hijri	nvarchar(20)	Checked
subtotal	decimal(12, 2)	Checked
vat_rate	decimal(12, 4)	Checked
vat_amount	decimal(12, 2)	Checked
lodging_tax_rate	decimal(12, 4)	Checked
lodging_tax_amount	decimal(12, 2)	Checked
debit_amount	decimal(12, 2)	Unchecked
original_invoice_amount	decimal(12, 2)	Checked
reason	nvarchar(500)	Unchecked
debit_type	nvarchar(50)	Unchecked
notes	nvarchar(1000)	Checked
is_sent_zatca	bit	Unchecked
zatca_uuid	nvarchar(255)	Checked
zatca_status	nvarchar(30)	Unchecked
zatca_icv	int	Checked
zatca_hash	nvarchar(512)	Checked
zatca_qr	nvarchar(MAX)	Checked
zatca_response	nvarchar(MAX)	Checked
zatca_profile	nvarchar(20)	Checked
zatca_submission_mode	nvarchar(20)	Checked
zatca_retry_count	int	Unchecked
zatca_last_error	nvarchar(MAX)	Checked
zatca_sent_at	datetime2(7)	Checked
is_compliance_sample	bit	Unchecked
created_by	int	Checked
created_at	datetime2(7)	Unchecked
updated_at	datetime2(7)	Checked
zaaer_id	int	Checked
		Unchecked