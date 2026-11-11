import "i18next";

import type en from "./locales/en.json";

// Keys are typed from the English locale: a key missing from it is a type error.
declare module "i18next" {
  interface CustomTypeOptions {
    resources: { translation: typeof en };
  }
}
