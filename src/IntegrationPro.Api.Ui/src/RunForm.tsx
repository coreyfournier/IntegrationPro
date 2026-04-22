import { useEffect, useState, type CSSProperties } from "react";
import Form from "@rjsf/core";
import validator from "@rjsf/validator-ajv8";

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

export function RunForm({ name, version, onResult }: {
  name: string; version: string; onResult: (resp: Response) => void;
}) {
  const [schema, setSchema] = useState<any | null>(null);
  const [credentials, setCredentials] = useState<any>({});
  const [configuration, setConfiguration] = useState<any>({});
  const [pending, setPending] = useState(false);
  const [abort, setAbort] = useState<AbortController | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    fetch(`/plugins/${name}/${version}/schema`)
      .then(r => {
        if (!r.ok) throw new Error(`GET schema failed: ${r.status}`);
        return r.json();
      })
      .then(setSchema)
      .catch(e => setError(e.message));
  }, [name, version]);

  async function run() {
    const ac = new AbortController();
    setAbort(ac); setPending(true); setError(null);
    try {
      const resp = await fetch("/integrations/run", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ pluginName: name, version, credentials, configuration }),
        signal: ac.signal,
      });
      onResult(resp);
    } catch (e: any) {
      if (e?.name !== "AbortError") setError(e?.message ?? String(e));
    } finally {
      setPending(false); setAbort(null);
    }
  }

  if (error && !schema) {
    return <pre role="alert" style={errorBoxStyle}>Error: {error}</pre>;
  }
  if (!schema) return <div>Loading schema…</div>;
  return (
    <div>
      {error && <pre role="alert" style={errorBoxStyle}>Error: {error}</pre>}
      <div style={{ display: "flex", gap: 16 }}>
        <div>
          <h3>Credentials</h3>
          <Form schema={schema.credentials} validator={validator} formData={credentials}
                onChange={e => setCredentials(e.formData)} uiSchema={{ password: { "ui:widget": "password" } }}>
            <span />
          </Form>
        </div>
        <div>
          <h3>Configuration</h3>
          <Form schema={schema.config} validator={validator} formData={configuration}
                onChange={e => setConfiguration(e.formData)}>
            <span />
          </Form>
        </div>
        <div>
          <button onClick={run} disabled={pending}>Run</button>
          {pending && <button onClick={() => abort?.abort()}>Cancel</button>}
        </div>
      </div>
    </div>
  );
}
