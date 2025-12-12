<template>
  <aside class="vnav">
    <div class="brand">VatIT</div>
    <ul>
      <li :class="{active: active==='benchmark'}" @click="$emit('navigate','benchmark')">Benchmark</li>
      <li :class="{active: active==='transaction'}" @click="$emit('navigate','transaction')">Transactions</li>
      <li :class="{active: active==='rules'}" @click="$emit('navigate','rules')">Business Rules</li>
      <li class="spacer"></li>
      <li class="muted">v1.0</li>
    </ul>
  </aside>
</template>

<script setup>
defineProps({active:String})
</script>

<style scoped>
.vnav{width:220px;background:linear-gradient(180deg,#071428,#092034);padding:18px 12px;color:var(--text);display:flex;flex-direction:column}
.brand{font-size:20px;font-weight:800;color:var(--accent);margin-bottom:12px}
.vnav ul{list-style:none;padding:0;margin:0;display:flex;flex-direction:column;gap:8px}
.vnav li{padding:10px 12px;border-radius:8px;cursor:pointer;color:var(--muted)}
.vnav li.active{background:rgba(6,182,212,0.12);color:var(--text);box-shadow:inset 0 0 0 1px rgba(6,182,212,0.06)}
.vnav li.spacer{flex:1}
.vnav li.muted{font-size:12px;color:var(--muted)}
</style>
