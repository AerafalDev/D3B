import { describe, expect, it } from "vitest";

import { keyLabel, offered, shellCommands } from "./commands.ts";

const english = { ctrl: "Ctrl", shift: "Shift", alt: "Alt" };

describe("keyLabel", () => {
  it("joins the modifiers' names with + outside macOS", () => {
    expect(keyLabel("mod+shift+p", "windows", english)).toBe("Ctrl+Shift+P");
    expect(keyLabel("mod+alt+b", "linux", english)).toBe("Ctrl+Alt+B");
  });

  it("writes the modifiers in the app's language", () => {
    expect(keyLabel("mod+shift+p", "windows", { ...english, shift: "Maj" })).toBe("Ctrl+Maj+P");
  });

  it("writes macOS's symbols, unjoined, whatever the language", () => {
    expect(keyLabel("mod+shift+p", "mac", { ...english, shift: "Maj" })).toBe("⌘⇧P");
  });
});

describe("offered", () => {
  it("offers the panel's command only with Advanced tools, and every other always", () => {
    const off = shellCommands.filter((command) => offered(command, false)).map((command) => command.id);
    const on = shellCommands.filter((command) => offered(command, true)).map((command) => command.id);
    expect(off).toEqual(["palette", "primarySideBar", "secondarySideBar", "advancedTools"]);
    expect(on).toEqual(["palette", "primarySideBar", "panel", "secondarySideBar", "advancedTools"]);
  });

  it("writes no shortcut for a command without one", () => {
    expect(keyLabel(undefined, "windows", english)).toBe("");
  });
});
