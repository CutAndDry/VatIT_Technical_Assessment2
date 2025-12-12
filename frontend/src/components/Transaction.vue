<template>
  <div class="card">
    <h2>Process Transaction</h2>

    <label>Request JSON</label>
    <textarea v-model="payload" rows="12"></textarea>

    <div style="margin-top:12px">
      <button class="primary" :disabled="running" @click="send">{{ running ? 'Sending…' : 'Send Transaction' }}</button>
    </div>

    <div style="margin-top:12px" v-if="response">
      <h3>Response</h3>
      <pre>{{ pretty }}</pre>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const running = ref(false)
const response = ref(null)

const sample = {
  transactionId: "txn_123",
  merchantId: "merchant_456",
  customerId: "customer_789",
  destination: { country: "US", state: "CA", city: "Los Angeles" },
  items: [
    { id: "item_1", category: "SOFTWARE", amount: 100.00 },
    { id: "item_2", category: "PHYSICAL_GOODS", amount: 50.00 }
  ],
  totalAmount: 150.00,
  currency: "USD"
}

const payload = ref(JSON.stringify(sample, null, 2))

function cryptoRandomId(){ return ([1e7]+-1e3+-4e3+-8e3+-1e11).replace(/[018]/g,c=> (c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)) }

const pretty = computed(() => response.value ? JSON.stringify(response.value, null, 2) : '')

async function send(){
  running.value = true
  response.value = null
  try{
    const req = JSON.parse(payload.value)
    const resp = await fetch('http://localhost:5100/api/transaction/process', { method: 'POST', headers: { 'content-type':'application/json' }, body: JSON.stringify(req) })
    const json = await resp.json()
    response.value = json
  }catch(err){ response.value = { error: err?.message ?? String(err) } }
  finally{ running.value = false }
}
</script>
