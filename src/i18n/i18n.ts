import i18next from "i18next";
import { initReactI18next } from "react-i18next";

import en from "./locales/en.json";
import fr from "./locales/fr.json";

/** The languages of the interface; English is the reference every other locale is checked against. */
export const languages = ["en", "fr"] as const;
export type Language = (typeof languages)[number];

export const resources = { en: { translation: en }, fr: { translation: fr } } as const;

/** The system's language when the app speaks it, English otherwise. */
export function systemLanguage(tags: readonly string[]): Language {
  for (const tag of tags) {
    const base = tag.split("-")[0]?.toLowerCase();
    const match = languages.find((language) => language === base);
    if (match !== undefined) {
      return match;
    }
  }
  return "en";
}

export function initI18n(): typeof i18next {
  i18next.use(initReactI18next).init({
    resources,
    lng: systemLanguage(navigator.languages),
    fallbackLng: "en",
    interpolation: { escapeValue: false },
  });
  return i18next;
}
