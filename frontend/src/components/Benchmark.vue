<template>
  <div class="card">
    <div style="display:flex;align-items:center;justify-content:space-between">
      <h2>Benchmark Runner</h2>
      <div style="display:flex;gap:8px;align-items:center">
        <button class="primary" :disabled="running" @click="run">{{ running ? 'Running…' : 'Run Benchmark' }}</button>
      </div>
    </div>

    <div style="display:flex;gap:12px;margin-top:12px;flex-wrap:wrap">
      <div style="flex:1;min-width:240px">
        <label>Target URL</label>
        <input v-model="targetUrl" :disabled="running" />
      </div>
      <div style="width:160px">
        <label>Total Requests</label>
        <input type="number" v-model.number="totalRequests" :disabled="running" />
      </div>
      <div style="width:160px">
        <label>Concurrency</label>
        <input type="number" v-model.number="concurrency" :disabled="running" />
      </div>
    </div>

    <div :class="['overlay', { 'overlay-running': running }]" style="margin-top:18px">
      <div v-if="running" class="spinner"></div>
      <div class="stat-grid">
        <div class="stat">
          <div class="kpi">{{ displayResult?.target ?? '-' }}</div>
          <div class="muted">Target</div>
        </div>
        <div class="stat">
          <div class="kpi">{{ displayResult?.totalRequests ?? '-' }}</div>
          <div class="muted">Total Requests</div>
        </div>
        <div class="stat">
          <div class="kpi">{{ displayResult?.concurrency ?? '-' }}</div>
          <div class="muted">Concurrency</div>
        </div>
        <div class="stat">
          <div class="kpi">{{ displayResult?.success ?? 0 }}</div>
          <div class="muted">Success</div>
        </div>
        <div class="stat">
          <div class="kpi">{{ displayResult?.failed ?? 0 }}</div>
          <div class="muted">Failed</div>
        </div>
        <div class="stat throughput">
          <div class="kpi">{{ displayResult?.throughputPerSec ?? 0 }}</div>
          <div class="muted">Req/s</div>
        </div>
      </div>

      <div style="display:flex;gap:12px;margin-top:12px;flex-wrap:wrap">
        <div style="flex:1;min-width:220px" class="card">
          <div class="muted">Avg / Fastest / Slowest (ms)</div>
          <div style="display:flex;gap:8px;align-items:baseline;margin-top:8px">
            <div class="big">{{ displayResult?.avgRequestMs ?? 0 }}</div>
            <div class="muted">/</div>
            <div class="big">{{ displayResult?.fastestRequestMs ?? 0 }}</div>
            <div class="muted">/</div>
            <div class="big">{{ displayResult?.slowestRequestMs ?? 0 }}</div>
          </div>
        </div>

        <div style="flex:1;min-width:300px" class="card">
          <div class="muted">Worker Stats</div>
          <table style="width:100%;margin-top:8px;border-collapse:collapse">
            <thead>
              <tr class="muted"><th style="text-align:left">Worker</th><th>Count</th><th>Avg ms</th><th>Min</th><th>Max</th></tr>
            </thead>
            <tbody>
              <tr v-if="!displayResult || Object.keys(displayResult.workerStats || {}).length === 0">
                <td colspan="5" style="padding:8px;color:var(--muted)">No worker stats yet</td>
              </tr>
              <tr v-for="(s, name) in (displayResult?.workerStats || {})" :key="name">
                <td style="padding:6px 8px">{{ name }}</td>
                <td style="padding:6px 8px;text-align:right">{{ s.count }}</td>
                <td style="padding:6px 8px;text-align:right">{{ s.avgMs }}</td>
                <td style="padding:6px 8px;text-align:right">{{ s.minMs }}</td>
                <td style="padding:6px 8px;text-align:right">{{ s.maxMs }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <div style="margin-top:12px;display:flex;gap:8px;align-items:center">
        <button v-if="displayResult" @click="showRaw = !showRaw" class="primary">{{ showRaw ? 'Hide Raw JSON' : 'Show Raw JSON' }}</button>
        <div class="muted">Elapsed: {{ displayResult?.durationMs ?? 0 }} ms</div>
      </div>

      <div v-if="showRaw && displayResult" style="margin-top:12px">
        <pre>{{ pretty }}</pre>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const targetUrl = ref('http://localhost:5100/api/benchmark/run')
const totalRequests = ref(1000)
const concurrency = ref(200)
const running = ref(false)
const result = ref(null)
const showRaw = ref(false)
const lastResult = ref(null)

const displayResult = computed(() => result.value || lastResult.value)
const pretty = computed(() => displayResult.value ? JSON.stringify(displayResult.value, null, 2) : '')

async function run(){
  running.value = true
  // keep lastResult visible while running so UI doesn't collapse
  showRaw.value = false
  try{
    const qs = `?totalRequests=${encodeURIComponent(totalRequests.value)}&concurrency=${encodeURIComponent(concurrency.value)}`
    const resp = await fetch(targetUrl.value + qs, { method: 'POST' })
    const json = await resp.json()
    // normalize null/undefined fields for UI
    json.WorkerStats = json.WorkerStats || {}
    result.value = json
    lastResult.value = json
  }catch(err){
    result.value = { error: err?.message ?? String(err) }
    lastResult.value = result.value
  }finally{ running.value = false }
}
</script>

<style scoped>
.overlay{
  position:relative;
}
  /* Make the Avg ms header and cells slightly larger and bolder for readability */
  table th:nth-child(3),
  table td:nth-child(3) {
    font-size: 1.05rem;
    font-weight: 600;
    color: #16a34a; /* green */
  }
  /* also tint the header label */
  table thead tr th:nth-child(3) {
    color: #16a34a;
  }
.overlay::after{
  content: '';
  position: absolute;
  inset: 0;
  background: rgba(2,6,23,0.6);
  display: none;
}
.overlay.running::after{ display:block }
.spinner{
  position: absolute; right:16px; top:12px; width:28px; height:28px; border-radius:50%; border:4px solid rgba(255,255,255,0.08); border-top-color: var(--accent); animation:spin 1s linear infinite;
}
@keyframes spin{ to{ transform: rotate(360deg) } }
</style>
