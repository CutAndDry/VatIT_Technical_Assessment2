<template>
  <div class="card rules-admin">
    <div class="rules-header" style="display:flex;align-items:center;justify-content:space-between;gap:12px">
      <div>
        <h2 style="margin:0">Business Rules Editor</h2>
          <div class="muted">Edit rule JSON, validate and publish. Changes are versioned on the server.</div>
      </div>
        <div style="display:flex;gap:8px;align-items:center">
          <label class="muted" style="margin-right:8px">Worker</label>
          <select v-model="worker">
            <option value="applicability">Applicability</option>
            <option value="exemption">Exemption</option>
            <option value="calculation">Calculation</option>
            <option value="validation">Validation</option>
          </select>
          <button class="primary" @click="runSample">Run Sample</button>
          <button @click="saveRules">Save</button>
          <button @click="refreshRules">Refresh</button>
        </div>
    </div>

    <div class="rules-body" style="margin-top:12px;display:grid;grid-template-columns:1fr 360px;gap:12px">
      <div>
        <label v-if="worker==='calculation'">Calculation Rules (form)</label>
        <div v-if="worker==='calculation'" class="card" style="margin-bottom:8px;padding:8px">
          <div v-for="(r, idx) in calcRules" :key="idx" style="display:flex;gap:8px;align-items:center;margin-bottom:6px">
            <select v-model="r.when.field" style="width:160px">
              <option value="totalAmount">totalAmount</option>
            </select>
            <select v-model="r.when.op" style="width:80px">
              <option value=">=">>=</option>
              <option value="<">&lt;</option>
            </select>
            <input v-model.number="r.when.value" style="width:80px" />
            <input v-model.number="r.then.rate" style="width:80px" />
            <input v-model="r.then.note" placeholder="note" />
            <button @click="removeCalc(idx)">Delete</button>
          </div>
          <button @click="addCalc">Add Rule</button>
        </div>

        <label v-else-if="worker==='validation'">Validation Rules (form)</label>
        <div v-else-if="worker==='validation'" class="card" style="margin-bottom:8px;padding:8px">
          <div v-for="(r, idx) in valRules" :key="idx" style="display:flex;gap:8px;align-items:center;margin-bottom:6px">
            <select v-model="r.when.field" style="width:160px">
              <option value="itemsCount">itemsCount</option>
              <option value="merchantId">merchantId</option>
            </select>
            <select v-model="r.when.op" style="width:80px">
              <option value=">=">>=</option>
              <option value="<">&lt;</option>
              <option value="==">==</option>
            </select>
            <input v-model="r.when.value" style="width:120px" />
            <input v-model="r.then.error" placeholder="error message" />
            <button @click="removeVal(idx)">Delete</button>
          </div>
          <button @click="addVal">Add Rule</button>
        </div>

        <label v-else>Rule JSON</label>
        <textarea v-model="text" rows="18" class="code"></textarea>
        <div style="margin-top:8px;display:flex;gap:8px;align-items:center">
          <div class="muted">Status:</div>
          <div :class="['muted', valid? 'green' : 'red']">{{ valid ? 'Valid JSON' : 'Invalid JSON' }}</div>
        </div>
      </div>

      <div>
        <div class="card" style="padding:12px;">
          <div class="muted">Preview / Test</div>
          <label>Sample Transaction (JSON)</label>
          <textarea v-model="sample" rows="6"></textarea>
          <label>Result</label>
          <pre>{{ result }}</pre>
        </div>
        <div class="card" style="margin-top:12px;padding:12px">
          <div class="muted">Versions</div>
          <ul>
            <li v-for="v in versions" :key="v.ts" class="muted">{{ new Date(v.ts).toLocaleString() }} — {{ v.note || 'saved' }}</li>
          </ul>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue'
import rulesApi from '../rulesApi'

const worker = ref('applicability')
const text = ref(JSON.stringify({rules:[]}, null, 2))
// NOTE (temporary): The Rules Admin UI includes an in-UI preview helper.
// The `Run Sample` action calls the Orchestrator preview evaluator (a lightweight
// mock) for fast interactive feedback. This preview is intended for testing
// and business onboarding only; it is NOT the workers' production rule engines.
// Keep this client-side helper for convenience, but do not treat preview results
// as authoritative for production behavior.
// Per-worker sample transactions that exercise the rules and generally "pass" validation
const defaultSamples = {
  applicability: { transactionId: 'txn_app_001', merchantId: 'merchant_456', totalAmount: 1500, itemsCount: 2, customerId: 'cust_100' },
  exemption: { transactionId: 'txn_ex_001', merchantId: 'MER-3', totalAmount: 500, itemsCount: 1, customerId: 'customer_exempt_001' },
  calculation: { transactionId: 'txn_calc_001', merchantId: 'merchant_456', totalAmount: 1500, itemsCount: 3, category: 'GENERAL', customerId: 'cust_300' },
  validation: { transactionId: 'txn_val_001', merchantId: 'MER-1', itemsCount: 1, totalAmount: 0 }
}

const sample = ref(JSON.stringify(defaultSamples[worker.value] || {transactionId:'txn_123', totalAmount:150}, null, 2))
const result = ref('')
const versions = ref([])

// form-backed rules
const calcRules = ref([])
const valRules = ref([])
const suppressSync = ref(false)

// built-in frontend defaults (used when API is unreachable or empty)
const defaultSeeds = {
  applicability: {
    rules: [],
    thresholds: { CA: 100000, NY: 500000, TX: 250000, FL: 100000 },
    merchantVolumes: {
      merchant_456: { CA: 2300000, NY: 500000, TX: 150000 },
      "MER-1": { CA: 0, NY: 0, TX: 0 },
      "MER-2": { CA: 0, NY: 0, TX: 0 },
      "MER-3": { CA: 300000, NY: 800000, TX: 400000 },
      "MER-4": { CA: 120000, NY: 550000, TX: 260000 },
      "MER-5": { CA: 500000, NY: 900000, TX: 600000 }
    }
  },
  exemption: {
    exemptCustomers: ["customer_exempt_001", "customer_nonprofit_002"],
    categoryExemptions: { EDUCATION: ["Educational materials exemption"], MEDICAL: ["Medical supplies exemption"] }
  },
  calculation: {
    rules: [
      { when: { field: "totalAmount", op: ">=", value: 1000 }, then: { rate: 0.08, note: "High-value transaction rate" } },
      { when: { field: "totalAmount", op: "<", value: 1000 }, then: { rate: 0.06, note: "Standard transaction rate" } }
    ],
    defaults: { rate: 0.06 }
  },
  validation: {
    rules: [
      { when: { field: "itemsCount", op: "<", value: 1 }, then: { error: "Transaction must contain at least one item" } },
      { when: { field: "merchantId", op: "==", value: "" }, then: { error: "Missing merchantId" } }
    ],
    notes: "Basic validation rules. Business users should extend as needed."
  }
}

const valid = ref(true)
const priorText = ref(text.value)

onMounted(() => {
  // Initialize editor with frontend defaults; do not auto-poll server.
  text.value = JSON.stringify(defaultSeeds[worker.value] || { rules: [] }, null, 2)
  hydrateForms()
  priorText.value = text.value

  // when worker selection changes, switch to the corresponding frontend default and hydrate forms.
  watch(worker, (v) => {
    suppressSync.value = true
      text.value = JSON.stringify(defaultSeeds[v] || { rules: [] }, null, 2)
      // update sample to a sensible per-worker example
      sample.value = JSON.stringify(defaultSamples[v] || defaultSamples['validation'], null, 2)
    try{ hydrateForms() }catch{}
    priorText.value = text.value
    suppressSync.value = false
    versions.value = []
  })
})

// Manual refresh: fetch server rules and versions only when user clicks Refresh
async function refreshRules(){
  const w = worker.value
  suppressSync.value = true
  try {
    const raw = await rulesApi.getRules(w)
    console.log('rulesApi.getRules (refresh) for', w, raw)
    if (raw && Object.keys(raw).length > 0) {
      text.value = JSON.stringify(raw, null, 2)
    } else {
      console.log('Server returned empty on refresh for', w)
        text.value = JSON.stringify(defaultSeeds[w] || { rules: [] }, null, 2)
        sample.value = JSON.stringify(defaultSamples[w] || defaultSamples['validation'], null, 2)
    }
  } catch (e) {
    console.warn('Failed to refresh server rules for', w, e)
    text.value = JSON.stringify(defaultSeeds[w] || { rules: [] }, null, 2)
  }

  try{ versions.value = Array.isArray(await rulesApi.getVersions(w)) ? await rulesApi.getVersions(w) : [] }catch{ versions.value = [] }
  try{ hydrateForms() }catch{}
  priorText.value = text.value
  suppressSync.value = false
}

watch(text, (v)=>{ try{ JSON.parse(v); valid.value=true }catch(e){ valid.value=false } })

// preserve last known-good JSON to avoid UI clearing due to async overwrites
watch(text, (newVal, oldVal) => {
  try {
    const t = newVal?.toString() || '';
    const trimmed = t.trim();
    if (trimmed.length === 0 || trimmed === '{}' || trimmed === '[]') {
      // revert to prior non-empty JSON
      if (priorText.value && priorText.value !== newVal) {
        setTimeout(() => { text.value = priorText.value }, 0)
      }
      return
    }
    // if newVal is valid JSON, store it
    JSON.parse(newVal)
    priorText.value = newVal
  } catch { /* ignore invalid JSON */ }
})

async function saveRules(){
  if(!valid.value) return alert('Fix JSON first')
  const payload = JSON.parse(text.value)
  try{
    await rulesApi.saveRules(payload, {note:'edited via UI'}, worker.value)
    versions.value = await rulesApi.getVersions(worker.value)
    alert('Saved')
  }catch(e){
    alert('Save failed: ' + String(e))
  }
}

function hydrateForms(){
  try{
    const obj = JSON.parse(text.value)
    if(worker.value==='calculation'){
      calcRules.value = (obj.rules||[]).map(r=>({ when: { field: r.when?.field||'totalAmount', op: r.when?.op||'>=', value: r.when?.value||0 }, then: { rate: r.then?.rate||0, note: r.then?.note||'' }}))
    } else { calcRules.value = [] }

    if(worker.value==='validation'){
      valRules.value = (obj.rules||[]).map(r=>({ when: { field: r.when?.field||'itemsCount', op: r.when?.op||'<', value: r.when?.value||'' }, then: { error: r.then?.error||'' }}))
    } else { valRules.value = [] }
  }catch(e){ /* ignore */ }
}

function syncCalcToText(){
  if (suppressSync.value) return
  if (worker.value !== 'calculation') return
  const doc = { rules: calcRules.value.map(r=>({ when: r.when, then: r.then })) , defaults: (JSON.parse(text.value).defaults||{}) }
  text.value = JSON.stringify(doc, null, 2)
}

function syncValToText(){
  if (suppressSync.value) return
  if (worker.value !== 'validation') return
  const doc = { rules: valRules.value.map(r=>({ when: r.when, then: r.then })) }
  text.value = JSON.stringify(doc, null, 2)
}

function addCalc(){ calcRules.value.push({ when:{field:'totalAmount',op:'>=',value:1000}, then:{rate:0.06,note:''}}); syncCalcToText() }
function removeCalc(i){ calcRules.value.splice(i,1); syncCalcToText() }

function addVal(){ valRules.value.push({ when:{field:'itemsCount',op:'<',value:1}, then:{error:'Invalid'}}); syncValToText() }
function removeVal(i){ valRules.value.splice(i,1); syncValToText() }

// keep text in sync when forms change
watch(calcRules, syncCalcToText, { deep:true })
watch(valRules, syncValToText, { deep:true })

async function runSample(){
  try{
    const r = await rulesApi.evaluate(JSON.parse(text.value), JSON.parse(sample.value), worker.value)
    result.value = JSON.stringify(r, null, 2)
  }catch(e){ result.value = String(e) }
}
</script>

<style scoped>
.rules-admin .code{font-family:ui-monospace,SFMono-Regular,Menlo,monospace;background:#021226;color:#bfeefc}
.green{color:#7ee7ff}
.red{color:#ff7b7b}
.rules-header h2{font-size:18px}
.rules-body textarea{resize:vertical}
</style>
