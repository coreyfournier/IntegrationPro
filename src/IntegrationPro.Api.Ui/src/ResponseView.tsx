import { useEffect, useState } from "react";

export function ResponseView({ resp }: { resp: Response | null }) {
  const [body, setBody] = useState<string>("");

  useEffect(() => {
    if (resp) resp.clone().text().then(setBody);
  }, [resp]);

  if (!resp) return null;

  const ct = resp.headers.get("content-type") ?? "";
  if (resp.status >= 400) {
    return <pre style={{ background: "#fee" }}>{body}</pre>;
  }
  if (ct.includes("application/json")) {
    try {
      return <pre>{JSON.stringify(JSON.parse(body), null, 2)}</pre>;
    } catch {
      return <pre>{body}</pre>;
    }
  }
  if (ct.includes("text/csv")) {
    const rows = body.split("\n").slice(0, 101);
    return <pre>{rows.join("\n")}</pre>;
  }
  return (
    <a href={URL.createObjectURL(new Blob([body]))} download>Download</a>
  );
}
