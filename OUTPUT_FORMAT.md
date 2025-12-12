# Detailed Response Output Format

Last updated: 2025-12-12

Changelog:
- 2025-12-12: Clarified output fields and added notes about per-gate timing and workerStats returned by the benchmark API.

This document shows the detailed output format for the orchestrator system, demonstrating how gates passed/failed, per-item fee breakdown, total fees calculated, and audit trail of decisions made.

## Success Scenario - All Gates Pass

### Input Request
```json
{
  "transactionId": "txn_123",
  "merchantId": "merchant_456",
  "customerId": "customer_789",
  "destination": {
    "country": "US",
    "state": "CA",
    "city": "Los Angeles"
  },
  "items": [
    {
      "id": "item_1",
      "category": "SOFTWARE",
      "amount": 100.00
    },
    {
      "id": "item_2",
      "category": "PHYSICAL_GOODS",
      "amount": 50.00
    }
  ],
  "totalAmount": 150.00,
  "currency": "USD"
}
```

### Expected Response
```json
{
  "transactionId": "txn_123",
  "status": "CALCULATED",
  "gates": [
    {
      "name": "ADDRESS_VALIDATION",
      "passed": true,
      "message": "Valid US address",
      "appliedExemptions": null
    },
    {
      "name": "APPLICABILITY",
      "passed": true,
      "message": "Merchant above $100,000 threshold in CA",
      "appliedExemptions": null
    },
    {
      "name": "EXEMPTION_CHECK",
      "passed": true,
      "message": "No exemptions applied",
      "appliedExemptions": []
    }
  ],
  "calculation": {
    "items": [
      {
        "itemId": "item_1",
        "amount": 100.00,
        "category": "SOFTWARE",
        "fees": {
          "stateRate": {
            "jurisdiction": "CA",
            "category": null,
            "rate": 0.06,
            "amount": 6.00
          },
          "countyRate": {
            "jurisdiction": "Los Angeles County",
            "category": null,
            "rate": 0.0025,
            "amount": 0.25
          },
          "cityRate": {
            "jurisdiction": "Los Angeles",
            "category": null,
            "rate": 0.0225,
            "amount": 2.25
          },
          "categoryModifier": {
            "jurisdiction": "CA",
            "category": "SOFTWARE",
            "rate": 0.01,
            "amount": 1.00
          }
        },
        "totalFee": 9.50
      },
      {
        "itemId": "item_2",
        "amount": 50.00,
        "category": "PHYSICAL_GOODS",
        "fees": {
          "stateRate": {
            "jurisdiction": "CA",
            "category": null,
            "rate": 0.06,
            "amount": 3.00
          },
          "countyRate": {
            "jurisdiction": "Los Angeles County",
            "category": null,
            "rate": 0.0025,
            "amount": 0.13
          },
          "cityRate": {
            "jurisdiction": "Los Angeles",
            "category": null,
            "rate": 0.0225,
            "amount": 1.13
          },
          "categoryModifier": {
            "jurisdiction": "CA",
            "category": "PHYSICAL_GOODS",
            "rate": 0.005,
            "amount": 0.25
          }
        },
        "totalFee": 4.51
      }
    ],
    "totalFees": 14.01,
    "effectiveRate": 0.0934
  },
  "auditTrail": [
    "Country validation passed: US",
    "State validation passed: CA",
    "City validation passed: Los Angeles",
    "Address validated via cache",
    "State threshold for CA: $100,000",
    "Retrieved merchant volume for merchant_456 in CA: $2,300,000",
    "Applicability check passed: Merchant volume $2,300,000 >= threshold $100,000",
    "Customer customer_789 does not have tax-exempt status",
    "Item item_1 category SOFTWARE has no exemptions",
    "Item item_2 category PHYSICAL_GOODS has no exemptions",
    "No exemptions applicable to this transaction",
    "Processing item item_1 ($100.00, SOFTWARE)",
    "  State rate (CA): 6.00% = $6.00",
    "  County rate (Los Angeles County): 0.25% = $0.25",
    "  City rate (Los Angeles): 2.25% = $2.25",
    "  Category modifier (SOFTWARE): 1.00% = $1.00",
    "  Item total fee: $9.50",
    "Processing item item_2 ($50.00, PHYSICAL_GOODS)",
    "  State rate (CA): 6.00% = $3.00",
    "  County rate (Los Angeles County): 0.25% = $0.13",
    "  City rate (Los Angeles): 2.25% = $1.13",
    "  Category modifier (PHYSICAL_GOODS): 0.50% = $0.25",
    "  Item total fee: $4.51",
    "Total fees calculated: $14.01",
    "Effective rate: 9.34%"
  ]
}
```

### Output Breakdown

#### 1. Gates Section (Which gates passed/failed and why)
```json
"gates": [
  {
    "name": "ADDRESS_VALIDATION",
    "passed": true,
    "message": "Valid US address"
  },
  {
    "name": "APPLICABILITY",
    "passed": true,
    "message": "Merchant above $100,000 threshold in CA"
  },
  {
    "name": "EXEMPTION_CHECK",
    "passed": true,
    "message": "No exemptions applied",
    "appliedExemptions": []
  }
]
```

✅ **ADDRESS_VALIDATION** - Passed because address is valid US/CA/Los Angeles
✅ **APPLICABILITY** - Passed because merchant volume ($2.3M) exceeds CA threshold ($100K)
✅ **EXEMPTION_CHECK** - Passed with no exemptions found

#### 2. Per-Item Fee Breakdown
```json
"items": [
  {
    "itemId": "item_1",
    "amount": 100.00,
    "category": "SOFTWARE",
    "fees": {
      "stateRate": {
        "jurisdiction": "CA",
        "rate": 0.06,
        "amount": 6.00
      },
      "countyRate": {
        "jurisdiction": "Los Angeles County",
        "rate": 0.0025,
        "amount": 0.25
      },
      "cityRate": {
        "jurisdiction": "Los Angeles",
        "rate": 0.0225,
        "amount": 2.25
      },
      "categoryModifier": {
        "jurisdiction": "CA",
        "category": "SOFTWARE",
        "rate": 0.01,
        "amount": 1.00
      }
    },
    "totalFee": 9.50
  }
]
```

**Item 1 Calculation:**
- Base Amount: $100.00
- State Rate (CA): 6% × $100 = **$6.00**
- County Rate (LA County): 0.25% × $100 = **$0.25**
- City Rate (LA): 2.25% × $100 = **$2.25**
- Category (SOFTWARE): 1% × $100 = **$1.00**
- **Item Total: $9.50**

#### 3. Total Fees Calculated
```json
"totalFees": 14.01,
"effectiveRate": 0.0934
```

- **Total Fees**: $14.01 (sum of all item fees)
- **Effective Rate**: 9.34% ($14.01 / $150.00)

#### 4. Audit Trail of Decisions Made
```json
"auditTrail": [
  "Country validation passed: US",
  "State validation passed: CA",
  "City validation passed: Los Angeles",
  "Address validated via cache",
  "State threshold for CA: $100,000",
  "Retrieved merchant volume for merchant_456 in CA: $2,300,000",
  "Applicability check passed: Merchant volume $2,300,000 >= threshold $100,000",
  "Customer customer_789 does not have tax-exempt status",
  "Item item_1 category SOFTWARE has no exemptions",
  "Item item_2 category PHYSICAL_GOODS has no exemptions",
  "No exemptions applicable to this transaction",
  "Processing item item_1 ($100.00, SOFTWARE)",
  "  State rate (CA): 6.00% = $6.00",
  "  County rate (Los Angeles County): 0.25% = $0.25",
  "  City rate (Los Angeles): 2.25% = $2.25",
  "  Category modifier (SOFTWARE): 1.00% = $1.00",
  "  Item total fee: $9.50",
  "Processing item item_2 ($50.00, PHYSICAL_GOODS)",
  "  State rate (CA): 6.00% = $3.00",
  "  County rate (Los Angeles County): 0.25% = $0.13",
  "  City rate (Los Angeles): 2.25% = $1.13",
  "  Category modifier (PHYSICAL_GOODS): 0.50% = $0.25",
  "  Item total fee: $4.51",
  "Total fees calculated: $14.01",
  "Effective rate: 9.34%"
]
```

The audit trail provides a **complete chronological record** of every decision made during processing.

---

## Failure Scenario 1 - Invalid Address

### Input Request
```json
{
  "transactionId": "txn_124",
  "merchantId": "merchant_456",
  "customerId": "customer_789",
  "destination": {
    "country": "UK",  // Invalid - only US supported
    "state": "CA",
    "city": "Los Angeles"
  },
  "items": [
    {
      "id": "item_1",
      "category": "SOFTWARE",
      "amount": 100.00
    }
  ],
  "totalAmount": 100.00,
  "currency": "USD"
}
```

### Response (Failed at Gate 1)
```json
{
  "transactionId": "txn_124",
  "status": "FAILED",
  "gates": [
    {
      "name": "ADDRESS_VALIDATION",
      "passed": false,
      "message": "Invalid country. Only US addresses are supported.",
      "appliedExemptions": null
    }
  ],
  "calculation": null,
  "auditTrail": [
    "Address validation failed: Invalid country 'UK'"
  ]
}
```

**Result:** Processing stopped at Gate 1. No subsequent gates were called. No calculation performed.

---

## Failure Scenario 2 - Below Threshold

### Input Request
```json
{
  "transactionId": "txn_125",
  "merchantId": "merchant_small_001",  // Small merchant
  "customerId": "customer_789",
  "destination": {
    "country": "US",
    "state": "CA",
    "city": "Los Angeles"
  },
  "items": [
    {
      "id": "item_1",
      "category": "SOFTWARE",
      "amount": 100.00
    }
  ],
  "totalAmount": 100.00,
  "currency": "USD"
}
```

### Response (Failed at Gate 2)
```json
{
  "transactionId": "txn_125",
  "status": "FAILED",
  "gates": [
    {
      "name": "ADDRESS_VALIDATION",
      "passed": true,
      "message": "Valid US address",
      "appliedExemptions": null
    },
    {
      "name": "APPLICABILITY",
      "passed": false,
      "message": "Merchant below $100,000 threshold in CA",
      "appliedExemptions": null
    }
  ],
  "calculation": null,
  "auditTrail": [
    "Country validation passed: US",
    "State validation passed: CA",
    "City validation passed: Los Angeles",
    "Address validated via cache",
    "State threshold for CA: $100,000",
    "Merchant merchant_small_001 not found in volume database",
    "Applicability check failed: Merchant volume $0 < threshold $100,000"
  ]
}
```

**Result:** Passed Gate 1, but failed at Gate 2. Processing stopped. No calculation performed.

---

## Response Format Summary

### ✅ Successful Transaction (Status: "CALCULATED")
- All gates passed
- Complete calculation with per-item breakdown
- Full audit trail from all 4 gates

### ❌ Failed Transaction (Status: "FAILED")
- One or more gates failed
- Processing stopped at first failure
- calculation is null
- Partial audit trail up to failure point

### 📊 Output Components

1. **Transaction ID**: Original transaction identifier
2. **Status**: "CALCULATED" or "FAILED"
3. **Gates**: Array showing each gate's result
   - name: Gate identifier
   - passed: true/false
   - message: Human-readable result
   - appliedExemptions: (only for exemption gate)
4. **Calculation**: Detailed fee breakdown (null if failed)
   - items: Per-item fee details
   - totalFees: Sum of all fees
   - effectiveRate: Overall percentage
5. **Audit Trail**: Complete decision log
   - Chronological order
   - Human-readable messages
   - Includes all validation, lookups, and calculations

---

## Testing the Output

Run the test script to see actual output:
```powershell
.\test-system.ps1
```

This will display a formatted version of the response showing:
- ✓/✗ symbols for gate pass/fail
- Color-coded output (green for success, red for failure)
- Formatted fee breakdown
- Complete audit trail

Example console output:
```
Response received!
==================

Transaction ID: txn_123
Status: CALCULATED

Gates:
  ✓ ADDRESS_VALIDATION: Valid US address
  ✓ APPLICABILITY: Merchant above $100,000 threshold in CA
  ✓ EXEMPTION_CHECK: No exemptions applied

Calculation:
  Total Fees: $14.01
  Effective Rate: 9.34%

Item Breakdown:
  Item item_1 (SOFTWARE):
    State: $6.00 (6%)
    County: $0.25 (0.25%)
    City: $2.25 (2.25%)
    Category: $1.00 (1%)
    Total: $9.50

Audit Trail:
  • Country validation passed: US
  • State validation passed: CA
  • Address validated via cache
  • Merchant volume: $2.3M in CA
  • Total fees calculated: $14.01
```
