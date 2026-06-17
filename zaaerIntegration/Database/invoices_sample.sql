zaaer_id	invoice_no	hotel_id	reservation_id	customer_id	invoice_date	invoice_type	subtotal	vat_rate	vat_amount	lodging_tax_rate	lodging_tax_amount	total_amount	payment_status	amount_paid	is_sent_zatca	zatca_uuid	created_by	created_at	period_from	period_to	notes	updated_at	order_id	revenue_category
15183	INVO-24-0001	24	19063	1349	2026-02-01 00:00:00.0000000	sales_invoice	110.63	15.00	16.60	2.50	2.77	130.00	paid	0.00	1	INVO-24-0001	101	2026-02-01 16:25:47.6374167	2026-01-31	2026-02-01		NULL	NULL	NULL
15215	INVO-24-0002	24	NULL	NULL	2026-02-01 00:00:00.0000000	sales_invoice	26.09	15.00	3.91	0.00	0.00	30.00	paid	0.00	1	INVO-24-0002	101	2026-02-01 18:13:51.9818477	2026-02-01	2026-02-01		NULL	667	other
15217	INVO-24-0003	24	19242	2816	2026-02-01 00:00:00.0000000	sales_invoice	2808.51	15.00	421.28	2.50	70.21	3300.00	paid	0.00	1	INVO-24-0003	101	2026-02-01 18:26:14.2540422	2026-01-29	2026-02-28		NULL	NULL	NULL
15546	INVO-24-0004	24	19320	1349	2026-02-02 00:00:00.0000000	sales_invoice	110.63	15.00	16.60	2.50	2.77	130.00	paid	0.00	1	INVO-24-0004	101	2026-02-02 16:56:56.4331686	2026-02-01	2026-02-02		NULL	NULL	NULL
15547	INVO-24-0005	24	19581	17628	2026-02-02 00:00:00.0000000	sales_invoice	85.10	15.00	12.77	2.50	2.13	100.00	paid	0.00	1	INVO-24-0005	101	2026-02-02 16:57:39.7396493	2026-02-01	2026-02-02		NULL	NULL	NULL
15549	INVO-24-0006	24	19067	14994	2026-02-02 00:00:00.0000000	sales_invoice	204.25	15.00	30.64	2.50	5.11	240.00	paid	0.00	1	INVO-24-0006	101	2026-02-02 16:59:40.2151708	2026-01-31	2026-02-02		NULL	NULL	NULL
15551	INVO-24-0007	24	19015	16049	2026-02-02 00:00:00.0000000	sales_invoice	204.25	15.00	30.64	2.50	5.11	240.00	paid	0.00	1	INVO-24-0007	101	2026-02-02 17:09:30.9913232	2026-02-02	2026-02-04		NULL	NULL	NULL
15556	INVO-24-0008	24	19015	16049	2026-02-02 00:00:00.0000000	sales_invoice	204.25	15.00	30.64	2.50	5.11	240.00	paid	0.00	1	INVO-24-0008	101	2026-02-02 17:12:33.0859112	2026-01-31	2026-02-04		NULL	NULL	NULL
15740	INVO-24-0009	24	19946	17882	2026-02-03 00:00:00.0000000	sales_invoice	102.13	15.00	15.32	2.50	2.55	120.00	paid	0.00	1	INVO-24-0009	101	2026-02-03 11:10:25.5572765	2026-02-02	2026-02-03		NULL	NULL	NULL
15767	INVO-24-0010	24	NULL	NULL	2026-02-03 00:00:00.0000000	sales_invoice	26.09	15.00	3.91	0.00	0.00	30.00	paid	0.00	1	INVO-24-0010	101	2026-02-03 12:14:11.3919212	2026-02-03	2026-02-03		NULL	719	other




invoice_id	int	Unchecked
zaaer_id	int	Checked
invoice_no	nvarchar(50)	Unchecked
hotel_id	int	Unchecked
reservation_id	int	Checked
customer_id	int	Checked
invoice_date	datetime2(7)	Unchecked
invoice_date_hijri	nvarchar(20)	Checked
invoice_type	nvarchar(50)	Unchecked
subtotal	decimal(12, 2)	Checked
vat_rate	decimal(5, 2)	Checked
vat_amount	decimal(12, 2)	Checked
lodging_tax_rate	decimal(5, 2)	Checked
lodging_tax_amount	decimal(12, 2)	Checked
total_amount	decimal(12, 2)	Checked
payment_status	nvarchar(20)	Unchecked
amount_paid	decimal(12, 2)	Unchecked
amount_remaining	decimal(12, 2)	Checked
amount_refunded	decimal(12, 2)	Checked
is_sent_zatca	bit	Unchecked
zatca_uuid	nvarchar(255)	Unchecked
created_by	int	Checked
created_at	datetime2(7)	Unchecked
period_from	date	Checked
period_to	date	Checked
notes	nvarchar(1000)	Checked
updated_at	datetime2(7)	Checked
order_id	int	Checked
revenue_category	nvarchar(50)	Checked
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