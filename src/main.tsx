import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import "./index.css";
import { ThemeProvider } from "@/components/theme-provider.tsx";
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
    <ThemeProvider defaultTheme="dark">
      <TooltipProvider delayDuration={300}>
        <App />
        <Toaster />
      </TooltipProvider>
    </ThemeProvider>
  </StrictMode>,
);
