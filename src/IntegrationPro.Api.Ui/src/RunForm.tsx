import { useEffect, useState } from "react";
import Form from "@rjsf/core";
import validator from "@rjsf/validator-ajv8";

export function RunForm({ name, version, onResult }: {
  name: string; version: string; onResult: (resp: Response) => void;
}) {
  const [schema, setSchema] = useState<any | null>(null);
  const [credentials, setCredentials] = useState<any>({});
  const [configuration, setConfiguration] = useState<any>({});
  const [pending, setPending] = useState(false);
  const [abort, setAbort] = useState<AbortController | null>(null);

  useEffect(() => {
    fetch(`/plugins/${name}/${version}/schema`).then(r => r.json()).then(setSchema);
  }, [name, version]);

  async function run() {
    const ac = new AbortController();
    setAbort(ac); setPending(true);
    try {
      const resp = await fetch("/integrations/run", {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({ pluginName: name, version, credentials, configuration }),
        signal: ac.signal,
      });
      onResult(resp);
    } finally { setPending(false); setAbort(null); }
  }

  if (!schema) return <div>Loading schema…</div>;
  return (
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
  );
}
