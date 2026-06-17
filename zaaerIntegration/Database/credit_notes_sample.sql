zaaer_id	credit_note_no	hotel_id	invoice_id	reservation_id	customer_id	credit_note_date	subtotal	credit_amount	vat_rate	vat_amount	lodging_tax_rate	lodging_tax_amount	original_invoice_amount	reason	credit_type	notes	is_sent_zatca	zatca_uuid	created_by	created_at	updated_at	order_id
275	CRED0001	24	43951	29235	25585	2026-03-27 00:00:00.000	636.27	750.00	15.0000	97.83	2.5000	15.91	NULL	قيمة الفاتورة خطأ حيث ان قيمة الفاتورة ب 3300 ريال	simplified	قيمة الفاتورة خطأ حيث ان قيمة الفاتورة ب 3300 ريال	1	CRED0001	24	2026-03-27 17:27:37.427	NULL	NULL
314	CRED0002	24	28130	30755	13761	2026-03-31 00:00:00.000	2120.89	2500.00	15.0000	326.09	2.5000	53.02	NULL	الغاء الفاتورة لعمل تعديلات على الحجز واضافة أيام تجديد على الايجار الشهري	simplified	الغاء الفاتورة لعمل تعديلات على الحجز واضافة أيام تجديد على الايجار الشهري	1	CRED0002	24	2026-03-31 17:59:44.290	NULL	NULL


credit_note_id	int	Unchecked
zaaer_id	int	Checked
credit_note_no	nvarchar(50)	Unchecked
hotel_id	int	Unchecked
invoice_id	int	Unchecked
reservation_id	int	Checked
customer_id	int	Checked
credit_note_date	datetime	Unchecked
credit_note_date_hijri	nvarchar(20)	Checked
subtotal	decimal(12, 2)	Checked
credit_amount	decimal(12, 2)	Unchecked
vat_rate	decimal(12, 4)	Checked
vat_amount	decimal(12, 2)	Checked
lodging_tax_rate	decimal(12, 4)	Checked
lodging_tax_amount	decimal(12, 2)	Checked
original_invoice_amount	decimal(12, 2)	Checked
reason	nvarchar(500)	Unchecked
credit_type	nvarchar(50)	Checked
notes	nvarchar(1000)	Checked
is_sent_zatca	bit	Unchecked
zatca_uuid	nvarchar(255)	Checked
created_by	int	Checked
created_at	datetime	Unchecked
updated_at	datetime2(7)	Checked
order_id	int	Checked
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
		Unchecked