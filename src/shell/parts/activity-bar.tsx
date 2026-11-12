import { useTranslation } from "react-i18next";

/** VS Code's activity bar, 48 px wide. Empty for now: each entry comes with its feature (the owner, 2026-11-12). */
export function ActivityBar() {
  const { t } = useTranslation();
  return <nav aria-label={t("shell.parts.activityBar")} className="bg-frame w-12 shrink-0" />;
}
