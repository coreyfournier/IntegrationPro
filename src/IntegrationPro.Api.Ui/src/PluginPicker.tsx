import { useEffect, useState, type CSSProperties } from "react";

type Summary = { name: string; latestVersion: string; description: string };

const errorBoxStyle: CSSProperties = {
  background: "#fee",
  border: "1px solid #f5c2c2",
  borderRadius: 4,
  padding: 12,
  marginBottom: 8,
  fontFamily: "ui-monospace, 'Cascadia Code', Menlo, Consolas, monospace",
  fontSize: 13,
  overflow: "auto",
  whiteSpace: "pre-wrap",
  color: "crimson",
};

export function PluginPicker({ onSelect }: { onSelect: (name: string, version: string) => void }) {
  const [items, setItems] = useState<Summary[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [selectedName, setSelectedName] = useState<string | null>(null);
  const [versions, setVersions] = useState<string[]>([]);
  const [selectedVersion, setSelectedVersion] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [refreshing, setRefreshing] = useState(false);

  const handleRefresh = () => {
    setRefreshing(true);
    setError(null);
    fetch("/plugins/refresh", { method: "POST" })
      .then(r => {
        if (!r.ok) throw new Error(`POST /plugins/refresh failed: ${r.status}`);
        return r.json();
      })
      .then(() => setRefreshKey(k => k + 1))
      .catch(e => setError(e.message))
      .finally(() => setRefreshing(false));
  };

  useEffect(() => {
    setError(null);
    fetch(`/plugins?page=${page}&pageSize=50&search=${encodeURIComponent(search)}`)
      .then(r => {
        if (!r.ok) throw new Error(`GET /plugins failed: ${r.status}`);
        return r.json();
      })
      .then(b => { setItems(b.items); setTotal(b.total); })
      .catch(e => setError(e.message));
  }, [page, search, refreshKey]);

  useEffect(() => {
    if (!selectedName) return;
    setError(null);
    fetch(`/plugins/${selectedName}/versions`)
      .then(r => {
        if (!r.ok) throw new Error(`GET /plugins/${selectedName}/versions failed: ${r.status}`);
        return r.json();
      })
      .then(b => { setVersions(b.versions); setSelectedVersion(b.versions[0] ?? null); })
      .catch(e => setError(e.message));
  }, [selectedName, refreshKey]);

  useEffect(() => {
    if (selectedName && selectedVersion) onSelect(selectedName, selectedVersion);
  }, [selectedName, selectedVersion, onSelect]);

  return (
    <div>
      {error && <pre role="alert" style={errorBoxStyle}>Error: {error}</pre>}
      <div style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 8 }}>
        <input placeholder="search" value={search} onChange={e => { setPage(1); setSearch(e.target.value); }} />
        <button onClick={handleRefresh} disabled={refreshing}>
          {refreshing ? "Refreshing…" : "Refresh Plugins"}
        </button>
      </div>
      <ul>
        {items.map(i => (
          <li key={i.name}>
            <button onClick={() => setSelectedName(i.name)}>
              {i.name} <small>{i.latestVersion}</small> — {i.description}
            </button>
          </li>
        ))}
      </ul>
      <div>Page {page} / {Math.max(1, Math.ceil(total / 50))}</div>
      <button disabled={page === 1} onClick={() => setPage(p => p - 1)}>Prev</button>
      <button disabled={page * 50 >= total} onClick={() => setPage(p => p + 1)}>Next</button>
      {selectedName && (
        <select value={selectedVersion ?? ""} onChange={e => setSelectedVersion(e.target.value)}>
          {versions.map(v => <option key={v} value={v}>{v}</option>)}
        </select>
      )}
    </div>
  );
}
