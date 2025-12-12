# VatIT UI

This is a minimal Vite + Vue 3 frontend used to run the benchmark and submit transactions to the local Orchestrator API.

Quick start:

1. Install dependencies

```bash
cd frontend
npm install
```

2. Run dev server

```bash
npm run dev
```

The UI will open on the port printed by Vite (usually http://localhost:5173). The app calls the orchestrator endpoints at `/api/benchmark/run` and `/api/transaction/process` — if you run the frontend on a different origin, configure a proxy in Vite or run the orchestrator on the same host/port.
