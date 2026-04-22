import { useEffect, useState, type CSSProperties } from "react";

export function ResponseView({ resp }: { resp: Response | null }) {
  const [body, setBody] = useState<string>("");

  useEffect(() => {
    if (!resp) { setBody(""); return; }
    let cancelled = false;
    setBody("");
    resp.clone().text().then(t => { if (!cancelled) setBody(t); });
    return () => { cancelled = true; };
  }, [resp]);

  if (!resp) return null;

  const baseStyle: CSSProperties = {
    background: "#f5f5f5",
    padding: 12,
    borderRadius: 4,
    border: "1px solid #ddd",
    fontFamily: "ui-monospace, 'Cascadia Code', Menlo, Consolas, monospace",
    fontSize: 13,
    overflow: "auto",
    maxHeight: 600,
    marginTop: 16,
  };

  const ct = resp.headers.get("content-type") ?? "";
  if (resp.status >= 400) {
    return <pre style={{ ...baseStyle, background: "#fee", borderColor: "#f5c2c2" }}>{body}</pre>;
  }
  if (ct.includes("application/json")) {
    try {
      return <pre style={baseStyle}>{JSON.stringify(JSON.parse(body), null, 2)}</pre>;
    } catch {
      return <pre style={baseStyle}>{body}</pre>;
    }
  }
  if (ct.includes("text/csv")) {
    const rows = body.split("\n").slice(0, 101);
    return <pre style={baseStyle}>{rows.join("\n")}</pre>;
  }
  return (
    <a href={URL.createObjectURL(new Blob([body]))} download>Download</a>
  );
}
