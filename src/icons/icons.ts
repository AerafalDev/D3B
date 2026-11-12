import close from "./expui/general/close_dark.svg";

/**
 * The JetBrains icons the app uses, by name: IntelliJ Platform's new UI icons in their dark variant, copied as they
 * are (README.md says from where). Each is 16 px.
 */
export const icons = {
  close,
} as const;

export type IconName = keyof typeof icons;
