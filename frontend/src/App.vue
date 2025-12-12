<template>
  <div class="app-shell">
    <VerticalNav :active="view" @navigate="view = $event" />
    <div class="content">
      <header class="topbar card">
        <h1>
          {{ view === 'benchmark' ? 'Benchmark UI' : view === 'transaction' ? 'Transactions UI' : view === 'rules' ? 'Business Rules UI' : 'VatIT' }}
        </h1>
      </header>

      <main class="main-area">
        <Benchmark v-if="view==='benchmark'" />
        <Transaction v-else-if="view==='transaction'" />
        <RulesAdmin v-else-if="view==='rules'" />
      </main>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import VerticalNav from './components/VerticalNav.vue'
import Benchmark from './components/Benchmark.vue'
import Transaction from './components/Transaction.vue'
import RulesAdmin from './pages/RulesAdmin.vue'

const view = ref('benchmark')
</script>

<style>
@import './styles.css';

.app-shell{display:flex;min-height:100vh}
.content{flex:1;padding:20px}
.topbar{display:flex;align-items:center;justify-content:space-between;margin-bottom:14px}
.main-area{margin-top:6px}

</style>
