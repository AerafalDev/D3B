import moveToLeft from "./expui/actions/moveToLeft_dark.svg";
import moveToRight from "./expui/actions/moveToRight_dark.svg";
import close from "./expui/general/close_dark.svg";
import search from "./expui/general/search_dark.svg";
import windowClose from "./expui/windows/close_dark.svg";
import windowMaximize from "./expui/windows/maximize_dark.svg";
import windowMinimize from "./expui/windows/minimize_dark.svg";
import windowRestore from "./expui/windows/restore_dark.svg";

/**
 * The JetBrains icons the app uses, by name: IntelliJ Platform's new UI icons in their dark variant, copied as they
 * are (README.md says from where). Each is 16 px.
 */
export const icons = {
  close,
  moveToLeft,
  moveToRight,
  search,
  windowClose,
  windowMaximize,
  windowMinimize,
  windowRestore,
} as const;

export type IconName = keyof typeof icons;
