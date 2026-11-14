import { cleanup, render, screen } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { type PlayedCharacter, usePlayed } from "./played-store.ts";
import { PlayedTab } from "./played-tab.tsx";

const kalyx: PlayedCharacter = {
  key: "294-2",
  name: "Kalyx",
  level: 54,
  breedName: { fr: "Iop", en: "Iop" },
  head: null,
  sex: "male",
  lastSeen: Date.UTC(2026, 10, 14, 10, 40),
  place: { mapId: 1, at: { x: 5, y: -18 }, subArea: { fr: "Astrub", en: "Astrub" }, area: null },
};

function tab() {
  // The tab reads only its params; the panel's API plays no part.
  const props = { params: { key: "294-2", icon: "user" } } as unknown as Parameters<typeof PlayedTab>[0];
  return render(<PlayedTab {...props} />);
}

describe("the tab of a character played whose client is closed", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  afterEach(cleanup);

  it("says who it is, that it is offline, and when and where it was last seen, without a map or gauges", () => {
    usePlayed.setState({ played: [kalyx] });
    const { container } = tab();
    expect(screen.getByRole("heading").textContent).toBe("Kalyx");
    expect(container.textContent).toContain("Level 54 · Iop · Offline");
    expect(container.textContent).toMatch(/Last seen on .*2026.*, in Astrub \(5, -18\)\./);
    expect(container.querySelector("svg, [role='meter'], [role='progressbar']")).toBeNull();
  });

  it("says when it was last seen alone when its place is unknown", () => {
    usePlayed.setState({ played: [{ ...kalyx, place: null }] });
    const { container } = tab();
    expect(container.textContent).toMatch(/Last seen on [^,]*, 2026, [^,]*\.$/);
  });

  it("says the character was forgotten once its file is gone", () => {
    usePlayed.setState({ played: [] });
    tab();
    expect(screen.getByText("This character was forgotten.")).toBeTruthy();
  });
});
