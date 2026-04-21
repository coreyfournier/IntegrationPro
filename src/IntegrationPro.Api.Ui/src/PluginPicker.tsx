import { useEffect, useState } from "react";

type Summary = { name: string; latestVersion: string; description: string };

export function PluginPicker({ onSelect }: { onSelect: (name: string, version: string) => void }) {
  const [items, setItems] = useState<Summary[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [selectedName, setSelectedName] = useState<string | null>(null);
  const [versions, setVersions] = useState<string[]>([]);
  const [selectedVersion, setSelectedVersion] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    fetch(`/plugins?page=${page}&pageSize=50&search=${encodeURIComponent(search)}`)
      .then(r => {
        if (!r.ok) throw new Error(`GET /plugins failed: ${r.status}`);
        return r.json();
      })
      .then(b => { setItems(b.items); setTotal(b.total); })
      .catch(e => setError(e.message));
  }, [page, search]);

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
  }, [selectedName]);

  useEffect(() => {
    if (selectedName && selectedVersion) onSelect(selectedName, selectedVersion);
  }, [selectedName, selectedVersion, onSelect]);

  return (
    <div>
      {error && <div role="alert" style={{ color: "crimson", marginBottom: 8 }}>Error: {error}</div>}
      <input placeholder="search" value={search} onChange={e => { setPage(1); setSearch(e.target.value); }} />
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
