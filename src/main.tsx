import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import "./index.css";
import { Toaster } from "@/components/ui/sonner.tsx";
import { TooltipProvider } from "@/components/ui/tooltip.tsx";
import { App } from "./app/app.tsx";
import { initI18n } from "./i18n/i18n.ts";

initI18n();

const root = document.getElementById("root");
if (root === null) {
  throw new Error("index.html has no #root element");
}

createRoot(root).render(
  <StrictMode>
    {/* Dark only (the owner): index.html sets the `dark` class once; nothing switches it. */}
    <TooltipProvider delayDuration={300}>
      <App />
      <Toaster theme="dark" />
    </TooltipProvider>
  </StrictMode>,
);
