import { useTranslation } from "react-i18next";

/**
 * VS Code's status bar, 22 px high, on the window's frame. Its one accent is a small blue dot before the app's name
 * (the owner's "2 / 3 sober", 2026-11-12); its other items come with their features.
 */
export function StatusBar() {
  const { t } = useTranslation();
  return (
    <footer className="bg-frame text-text-muted flex h-[22px] shrink-0 items-center gap-1.5 px-2.5 text-xs">
      <span aria-hidden="true" className="bg-focus size-1.5 rounded-full" />
      <span>{t("app.name")}</span>
    </footer>
  );
}
