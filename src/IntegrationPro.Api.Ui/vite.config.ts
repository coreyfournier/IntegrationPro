import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: "/ui/",
  plugins: [react()],
  server: {
    proxy: {
      "/plugins": "http://localhost:8081",
      "/integrations": "http://localhost:8081"
    }
  },
  build: { outDir: "dist", emptyOutDir: true }
});
