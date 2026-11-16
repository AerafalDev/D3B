import { cleanup, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

vi.mock("@/lib/platform.ts", () => ({ platform: () => "windows" }));

const { EditorWatermark } = await import("./editor-watermark.tsx");
const { useAppSettings } = await import("@/settings/app-settings.ts");

describe("the editor area's watermark", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  afterEach(cleanup);

  it("lists the main shortcuts, the panel's with Advanced tools", () => {
    useAppSettings.setState({ advancedTools: true });
    render(<EditorWatermark />);
    expect(screen.getAllByRole("term").map((term) => term.textContent)).toEqual([
      "Show all commands",
      "Toggle the primary side bar",
      "Toggle the panel",
      "Toggle the secondary side bar",
    ]);
  });

  it("leaves the panel's out without Advanced tools", () => {
    useAppSettings.setState({ advancedTools: false });
    render(<EditorWatermark />);
    expect(screen.queryByText("Toggle the panel")).toBeNull();
    expect(screen.getAllByRole("term")).toHaveLength(3);
  });
});
