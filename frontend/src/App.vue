<template>
  <div class="app">
    <header>
      <h1>VatIT — Admin UI</h1>
      <nav>
        <button :class="{active: view==='benchmark'}" @click="view='benchmark'">Benchmark</button>
        <button :class="{active: view==='transaction'}" @click="view='transaction'">Process Transaction</button>
      </nav>
    </header>

    <main>
      <Benchmark v-if="view==='benchmark'" />
      <Transaction v-else />
    </main>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import Benchmark from './components/Benchmark.vue'
import Transaction from './components/Transaction.vue'

const view = ref('benchmark')
</script>

<style>
@import './styles.css';
</style>
