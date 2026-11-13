import { resolve } from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

// Tauri reads the dev server from tauri.conf.json (devUrl), so the port is fixed and must stay free.
const host = process.env["TAURI_DEV_HOST"];

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": resolve(import.meta.dirname, "./src"),
    },
  },
  clearScreen: false,
  envPrefix: ["VITE_", "TAURI_ENV_"],
  server: {
    port: 1420,
    strictPort: true,
    host: host ?? false,
    hmr: host ? { protocol: "ws", host, port: 1421 } : undefined,
    watch: {
      ignored: ["**/src-tauri/**", "**/data/**"],
    },
  },
  build: {
    // The webviews Tauri 2 ships with: WebView2 (Chromium) on Windows, WebKit on macOS and Linux.
    target: process.env["TAURI_ENV_PLATFORM"] === "windows" ? "chrome120" : "safari16",
    minify: !process.env["TAURI_ENV_DEBUG"],
    sourcemap: Boolean(process.env["TAURI_ENV_DEBUG"]),
    // dist/.vite/license.md: the bundled packages' licences, which scripts/notices.ts puts in the app's packages.
    license: true,
  },
  test: {
    environment: "happy-dom",
    include: ["src/**/*.test.{ts,tsx}", "scripts/**/*.test.ts"],
  },
});
