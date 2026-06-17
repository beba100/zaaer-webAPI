# 📋 VoM Journal Entries API Structure

## 🎯 **Simple Request Format**

**Endpoint:** `POST /api/accounting/journal-entries`

**Base URL:** `https://kimoo.getvom.com` (or your subdomain)

**Full URL:** `https://kimoo.getvom.com/api/accounting/journal-entries`

---

## 📦 **Request Body Structure**

```json
{
  "journal_date": "10-12-2025",
  "code": "INV001",
  "memo": "قيد فاتورة رقم INV001 - الدمام 1",
  "accounts": [
    {
      "id": 51,
      "debit": 100.00,
      "credit": 0,
      "cost_center_id": 3,
      "tax_status": 2,
      "description": "فاتورة"
    },
    {
      "id": 175,
      "debit": 0,
      "credit": 100.00,
      "tax_status": 2,
      "description": "صندوق"
    }
  ]
}
```

---

## 📝 **Field Descriptions**

### **Top Level Fields:**

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `journal_date` | string | ✅ Yes | Date in format `dd-MM-yyyy` | `"10-12-2025"` |
| `code` | string | ✅ Yes | Unique journal entry code | `"INV001"` |
| `memo` | string | ✅ Yes | Description/memo for the entry | `"قيد فاتورة رقم INV001"` |
| `accounts` | array | ✅ Yes | Array of account line items | See below |

### **Account Line Item Fields:**

| Field | Type | Required | Description | Example |
|-------|------|----------|-------------|---------|
| `id` | integer | ✅ Yes | Account ID from Chart of Accounts | `51` |
| `debit` | decimal | ✅ Yes | Debit amount (0 if credit) | `100.00` |
| `credit` | decimal | ✅ Yes | Credit amount (0 if debit) | `0` |
| `cost_center_id` | integer | ❌ Optional | Cost Center ID | `3` |
| `tax_status` | integer | ❌ Optional | Tax status: `0`=No Tax, `1`=Exclusive, `2`=Inclusive | `2` |
| `tax_id` | integer | ❌ Optional | Tax ID (if applicable) | `null` |
| `description` | string | ❌ Optional | Line item description | `"فاتورة"` |

---

## 🔢 **Tax Status Values**

| Value | Arabic | English | Meaning |
|-------|--------|---------|---------|
| `0` | بدون ضريبة | No Tax | No tax applied |
| `1` | غير شامل | Tax Exclusive | Tax not included in amount |
| `2` | شامل الضريبة | Tax Inclusive | Tax included in amount |

---

## ✅ **Validation Rules**

1. **Date Format:** Must be `dd-MM-yyyy` (e.g., `"10-12-2025"`)
2. **Balance:** Total Debit must equal Total Credit
3. **Accounts:** At least 1 account line item required
4. **Account ID:** Must exist in Chart of Accounts
5. **Code:** Must be unique (VoM will validate)

---

## 📊 **Real Examples**

### **Example 1: Invoice Journal Entry**

```json
{
  "journal_date": "10-12-2025",
  "code": "INV001",
  "memo": "قيد فاتورة رقم INV001 - الدمام 1",
  "accounts": [
    {
      "id": 51,
      "debit": 1000.00,
      "credit": 0,
      "cost_center_id": 3,
      "tax_status": 2,
      "description": "فاتورة"
    },
    {
      "id": 175,
      "debit": 0,
      "credit": 1000.00,
      "tax_status": 2,
      "description": "صندوق"
    }
  ]
}
```

### **Example 2: Payment Receipt Journal Entry**

```json
{
  "journal_date": "10-12-2025",
  "code": "REC001",
  "memo": "قيد سند قبض رقم REC001 - الدمام 1",
  "accounts": [
    {
      "id": 51,
      "debit": 0,
      "credit": 100.00,
      "cost_center_id": 3,
      "tax_status": 2,
      "description": "سند قبض"
    },
    {
      "id": 175,
      "debit": 100.00,
      "credit": 0,
      "tax_status": 2,
      "description": "صندوق"
    }
  ]
}
```

### **Example 3: Credit Note (Reverse Entry)**

```json
{
  "journal_date": "10-12-2025",
  "code": "CN001",
  "memo": "قيد إرجاع فاتورة رقم CN001 - الدمام 1",
  "accounts": [
    {
      "id": 51,
      "debit": 0,
      "credit": 500.00,
      "cost_center_id": 3,
      "tax_status": 2,
      "description": "إرجاع فاتورة"
    },
    {
      "id": 175,
      "debit": 500.00,
      "credit": 0,
      "tax_status": 2,
      "description": "صندوق"
    }
  ]
}
```

---

## 🔐 **Authentication**

**Required Headers:**

```
Authorization: Bearer {your_token}
Content-Type: application/json
Api-Agent: zapier
Accept-Language: en (or ar)
```

---

## 📤 **Complete cURL Example**

```bash
curl -X POST "https://kimoo.getvom.com/api/accounting/journal-entries" \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -H "Content-Type: application/json" \
  -H "Api-Agent: zapier" \
  -H "Accept-Language: ar" \
  -d '{
    "journal_date": "10-12-2025",
    "code": "INV001",
    "memo": "قيد فاتورة رقم INV001 - الدمام 1",
    "accounts": [
      {
        "id": 51,
        "debit": 100.00,
        "credit": 0,
        "cost_center_id": 3,
        "tax_status": 2,
        "description": "فاتورة"
      },
      {
        "id": 175,
        "debit": 0,
        "credit": 100.00,
        "tax_status": 2,
        "description": "صندوق"
      }
    ]
  }'
```

---

## 📥 **Response Structure**

### **Success Response (200 OK):**

```json
{
  "status": 200,
  "success": true,
  "data": {
    "id": 12345,
    "code": "INV001",
    "journal_date": "10-12-2025",
    "memo": "قيد فاتورة رقم INV001 - الدمام 1",
    "created_at": "2025-12-10T10:30:00Z"
  },
  "errors": null,
  "message": null
}
```

### **Error Response (400 Bad Request):**

```json
{
  "status": 400,
  "success": false,
  "data": null,
  "errors": {
    "journal_date": ["Invalid date format"],
    "accounts": ["Debit and Credit must balance"]
  },
  "message": "Validation failed"
}
```

---

## 🎯 **Key Points**

1. ✅ **Date Format:** Always use `dd-MM-yyyy` (e.g., `"10-12-2025"`)
2. ✅ **Balance:** Total Debit = Total Credit (required!)
3. ✅ **Code:** Must be unique per journal entry
4. ✅ **Accounts:** Minimum 2 accounts (one debit, one credit)
5. ✅ **Tax Status:** Use `2` for "شامل الضريبة" (Tax Inclusive)
6. ✅ **Cost Center:** Optional but recommended for proper tracking

---

## 📁 **Where It's Used in Your Project**

| File | Purpose |
|------|---------|
| `DTOs/VoM/VoMJournalEntryDto.cs` | Request/Response DTOs |
| `Services/VoM/VoMJournalEntryService.cs` | Service that calls VoM API |
| `Services/InvoiceJournalEntryService.cs` | Creates invoice journal entries |
| `Services/PaymentReceiptJournalEntryService.cs` | Creates payment receipt journal entries |
| `Services/CreditNoteJournalEntryService.cs` | Creates credit note journal entries |

---

**Date:** December 20, 2025  
**API Version:** VoM v1  
**Status:** ✅ Production Ready

