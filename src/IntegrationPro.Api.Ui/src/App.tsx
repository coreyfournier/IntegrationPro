import { useCallback, useState } from "react";
import { PluginPicker } from "./PluginPicker";
import { RunForm } from "./RunForm";
import { ResponseView } from "./ResponseView";

export function App() {
  const [selection, setSelection] = useState<{ name: string; version: string } | null>(null);
  const [resp, setResp] = useState<Response | null>(null);

  const handleSelect = useCallback((name: string, version: string) => {
    setSelection({ name, version });
    setResp(null);
  }, []);

  return (
    <div style={{ fontFamily: "system-ui", padding: 16 }}>
      <h1>IntegrationPro Playground</h1>
      <PluginPicker onSelect={handleSelect} />
      {selection && (
        <RunForm
          key={`${selection.name}@${selection.version}`}
          name={selection.name}
          version={selection.version}
          onResult={setResp}
        />
      )}
      <ResponseView resp={resp} />
    </div>
  );
}
