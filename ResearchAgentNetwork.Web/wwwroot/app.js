async function fetchJSON(url, options) {
  const res = await fetch(url, options);
  if (!res.ok) throw new Error(await res.text());
  return await res.json();
}

let cachedTasks = [];

async function loadSettings() {
  try {
    const s = await fetchJSON('/api/settings');
    const depth = document.getElementById('depth');
    const log = document.getElementById('logPrompts');
    if (depth && typeof s.maxDecompositionDepth === 'number') depth.value = s.maxDecompositionDepth;
    if (log && typeof s.logPrompts === 'boolean') log.checked = s.logPrompts;
  } catch {}
}

async function loadTasks() {
  const tbody = document.getElementById('tasks');
  const error = document.getElementById('tasksError');
  if (error) error.classList.add('hidden');
  if (tbody) tbody.innerHTML = '<tr><td colspan="5">Loading…</td></tr>';
  try {
    const list = await fetchJSON('/api/tasks');
    cachedTasks = list;
    renderTasks();
  } catch (e) {
    if (error) {
      error.textContent = e.message;
      error.classList.remove('hidden');
    }
    if (tbody) tbody.innerHTML = '';
  }
}

function renderTasks() {
  const tbody = document.getElementById('tasks');
  const textEl = document.getElementById('filterText');
  const statusEl = document.getElementById('filterStatus');
  const text = (textEl?.value || '').toLowerCase();
  const status = statusEl?.value || '';
  const filtered = cachedTasks.filter(t =>
    (!text || (t.description?.toLowerCase().includes(text) || (t.id + '').toLowerCase().includes(text))) &&
    (!status || t.status === status)
  );
  tbody.innerHTML = '';
  filtered.forEach(t => {
    const tr = document.createElement('tr');
    tr.className = `status-${t.status}`;
    const cls = `badge ${t.status}`;
    const badge = `<span class="${cls}">${t.status}</span>`;
    const created = t.createdAt ? new Date(t.createdAt).toLocaleString() : '';
    const viewBtn = `<button data-id="${t.id}" class="view">View report</button>`;
    tr.innerHTML = `<td>${t.id}</td><td>${t.description}</td><td>${badge}</td><td>${created}</td><td>${viewBtn}</td>`;
    tbody.appendChild(tr);
  });
}

async function loadReport(id) {
  const res = await fetch(`/api/tasks/${id}/report`);
  const text = await res.text();
  const rawEl = document.getElementById('reportRaw');
  const htmlEl = document.getElementById('reportHtml');
  const details = document.getElementById('taskDetails');
  const copyBtn = document.getElementById('copyReport');
  const dlBtn = document.getElementById('downloadReport');
  const persistedBtn = document.getElementById('loadPersistedReport');
  const task = cachedTasks.find(t => t.id === id);
  if (details && task) {
    details.innerHTML = `
      <div><strong>Description:</strong> ${task.description}</div>
      <div><strong>Status:</strong> ${task.status}</div>
      <div><strong>Priority:</strong> ${task.priority ?? ''}</div>
      <div class="muted">Id: ${task.id}</div>
    `;
  }
  if (rawEl) rawEl.textContent = text;
  if (htmlEl && window.marked) htmlEl.innerHTML = marked.parse(text);
  if (copyBtn) {
    copyBtn.disabled = false;
    copyBtn.onclick = async () => {
      await navigator.clipboard.writeText(text);
    };
  }
  if (dlBtn) {
    dlBtn.disabled = false;
    dlBtn.onclick = () => {
      const blob = new Blob([text], { type: 'text/markdown' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `report-${id}.md`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    };
  }
  if (persistedBtn) {
    persistedBtn.disabled = false;
    persistedBtn.onclick = async () => {
      try {
        const res = await fetch(`/api/tasks/${id}/report/persisted`);
        if (res.ok) {
          const md = await res.text();
          if (rawEl) rawEl.textContent = md;
          if (htmlEl && window.marked) htmlEl.innerHTML = marked.parse(md);
        }
      } catch {}
    };
  }
}

async function loadSummary() {
  const data = await fetchJSON('/api/progress');
  document.getElementById('summary').textContent = `Progress: ${JSON.stringify(data)}`;
}

function bindEvents() {
  document.getElementById('taskForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const description = document.getElementById('desc').value.trim();
    const priority = parseInt(document.getElementById('prio').value, 10) || 5;
    if (!description) return;
    await fetchJSON('/api/tasks', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ description, priority })
    });
    document.getElementById('desc').value = '';
    await loadTasks();
  });

  document.getElementById('tasks').addEventListener('click', async (e) => {
    const btn = e.target.closest('button.view');
    if (btn) {
      const id = btn.getAttribute('data-id');
      await loadReport(id);
      renderTree(id);
      enableActions(id);
      return;
    }
    const retry = e.target.closest('#actRetry');
    const cancel = e.target.closest('#actCancel');
    const force = e.target.closest('#actForce');
    if (retry || cancel || force) {
      const details = document.getElementById('taskDetails');
      const idLine = details?.querySelector('.muted')?.textContent || '';
      const match = idLine?.match(/Id:\s*([0-9a-f\-]+)/i);
      const id = match?.[1];
      if (!id) return;
      if (retry) await fetchJSON(`/api/tasks/${id}`, { method: 'PATCH', headers: { 'Content-Type':'application/json' }, body: JSON.stringify({ action: 'retry' }) });
      if (cancel) await fetchJSON(`/api/tasks/${id}`, { method: 'PATCH', headers: { 'Content-Type':'application/json' }, body: JSON.stringify({ action: 'cancel' }) });
      if (force) await fetchJSON(`/api/tasks/${id}`, { method: 'PATCH', headers: { 'Content-Type':'application/json' }, body: JSON.stringify({ action: 'force' }) });
      // refresh list and details after action
      await loadTasks();
      await loadReport(id);
      renderTree(id);
    }
  });

  const textEl = document.getElementById('filterText');
  const statusEl = document.getElementById('filterStatus');
  if (textEl) textEl.addEventListener('input', renderTasks);
  if (statusEl) statusEl.addEventListener('change', renderTasks);

  document.getElementById('settingsForm').addEventListener('submit', async (e) => {
    e.preventDefault();
    const maxDepth = parseInt(document.getElementById('depth').value, 10);
    const logPrompts = document.getElementById('logPrompts').checked;
    await fetchJSON('/api/settings', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ maxDecompositionDepth: isNaN(maxDepth) ? undefined : maxDepth, logPrompts })
    });
  });
  const adminTasks = document.getElementById('adminListTasks');
  const adminEvents = document.getElementById('adminListEvents');
  const adminOut = document.getElementById('adminOut');
  if (adminTasks) adminTasks.addEventListener('click', async () => {
    const data = await fetchJSON('/admin/tasks');
    if (adminOut) adminOut.textContent = JSON.stringify(data, null, 2);
  });
  if (adminEvents) adminEvents.addEventListener('click', async () => {
    const data = await fetchJSON('/admin/events');
    if (adminOut) adminOut.textContent = JSON.stringify(data, null, 2);
  });
}

async function enableActions(id) {
  const retry = document.getElementById('actRetry');
  const cancel = document.getElementById('actCancel');
  const force = document.getElementById('actForce');
  for (const b of [retry, cancel, force]) if (b) b.disabled = false;
  if (retry) retry.onclick = async () => { await fetchJSON(`/api/tasks/${id}`, { method: 'PATCH', headers: { 'Content-Type':'application/json' }, body: JSON.stringify({ action: 'retry' }) }); await loadTasks(); await loadReport(id); renderTree(id); };
  if (cancel) cancel.onclick = async () => { await fetchJSON(`/api/tasks/${id}`, { method: 'PATCH', headers: { 'Content-Type':'application/json' }, body: JSON.stringify({ action: 'cancel' }) }); await loadTasks(); await loadReport(id); renderTree(id); };
  if (force) force.onclick = async () => { await fetchJSON(`/api/tasks/${id}`, { method: 'PATCH', headers: { 'Content-Type':'application/json' }, body: JSON.stringify({ action: 'force' }) }); await loadTasks(); await loadReport(id); renderTree(id); };
}

async function fetchChildren(id) {
  return await fetchJSON(`/api/tasks/${id}/children`);
}

async function renderTree(rootId) {
  const container = document.getElementById('taskTree');
  container.innerHTML = 'Loading tree…';
  const root = cachedTasks.find(t => t.id === rootId) || await fetchJSON(`/api/tasks/${rootId}`);
  async function build(node, depth = 0) {
    const children = await fetchChildren(node.id);
    const indent = '&nbsp;'.repeat(depth * 4);
    let html = `${indent}• <strong>${node.description}</strong> <span class="badge ${node.status}">${node.status}</span><br/>`;
    for (const c of children) {
      html += await build(c, depth + 1);
    }
    return html;
  }
  const html = await build(root, 0);
  container.innerHTML = html || 'No children';
}

function startSse() {
  const es = new EventSource('/api/events');
  es.onmessage = async (ev) => {
    try {
      const msg = JSON.parse(ev.data);
      if (msg.type === 'progress') {
        document.getElementById('summary').textContent = `Progress: ${JSON.stringify(msg.summary)}`;
        loadTasks();
      }
      if (msg.type === 'task') {
        loadTasks();
        // append to per-task events panel if selected
        const details = document.getElementById('taskDetails');
        const eventsEl = document.getElementById('taskEvents');
        const idLine = details?.querySelector('.muted')?.textContent || '';
        const match = idLine?.match(/Id:\s*([0-9a-f\-]+)/i);
        const currentId = match?.[1];
        if (currentId && msg.TaskId && (msg.TaskId.toLowerCase?.() === currentId.toLowerCase())) {
          if (eventsEl) {
            const line = `${new Date().toLocaleTimeString()}  ${msg.EventType} → ${msg.Status}${msg.Message ? ' — ' + msg.Message : ''}`;
            eventsEl.textContent = (eventsEl.textContent + '\n' + line).trimStart();
          }
        }
      }
    } catch { }
  };
}

window.addEventListener('DOMContentLoaded', async () => {
  bindEvents();
  await loadSettings();
  await loadTasks();
  await loadSummary();
  startSse();
});

