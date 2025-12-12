const API_BASE = 'http://localhost:5100/admin/rules'

async function callJson(url, opts){
  const res = await fetch(url, opts)
  if (!res.ok) throw new Error(await res.text())
  return res.json()
}

export default {
  async getRules(worker = 'applicability'){
    return callJson(`${API_BASE}/${worker}`)
  },
  async saveRules(payload, meta, worker = 'applicability'){
    const url = `${API_BASE}/${worker}${meta?.note ? '?note='+encodeURIComponent(meta.note) : ''}`
    return callJson(url, {method:'PUT', headers:{'Content-Type':'application/json'}, body: JSON.stringify(payload)})
  },
  async getVersions(worker = 'applicability'){
    return callJson(`${API_BASE}/${worker}/versions`)
  },
  async evaluate(rulesDoc, input, worker = 'applicability'){
    const body = rulesDoc ? { input, rules: rulesDoc } : { input }
    return callJson(`${API_BASE}/${worker}/evaluate`, {method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify(body)})
  }
}
