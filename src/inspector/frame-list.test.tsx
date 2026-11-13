import { cleanup, render, screen, within } from "@testing-library/react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import { FrameList } from "./frame-list.tsx";
import type { FrameSummary } from "./inspector-store.ts";

function frame(number: number, change: Partial<FrameSummary> = {}): FrameSummary {
  return {
    number,
    millis: 192_418,
    fromClient: false,
    name: "jow",
    readable: "MapCurrentEvent",
    outcome: "understood",
    size: 3912,
    ...change,
  };
}

function rows(): string[] {
  return within(screen.getByRole("list"))
    .queryAllByRole("listitem")
    .map((row) => row.textContent ?? "");
}

describe("the frame list", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  afterEach(cleanup);

  it("lists a frame by its number, time, side, message, outcome and size", () => {
    render(<FrameList frames={[frame(41818)]} />);

    expect(rows()).toEqual(["4181803:12.418↓ServerMapCurrentEventunderstood3,912 B"]);
  });

  it("names a frame without pins by the build's name, and one without a name by a dash", () => {
    render(
      <FrameList
        frames={[
          frame(1, { readable: null, outcome: "unknown", fromClient: true }),
          frame(2, { readable: null, name: null }),
        ]}
      />,
    );

    expect(rows()[0]).toContain("↑Clientjowunknown");
    expect(rows()[1]).toContain("Server—");
  });

  it("says how many frames between two were not listed", () => {
    render(<FrameList frames={[frame(1), frame(2), frame(6), frame(7)]} />);

    expect(rows()).toHaveLength(5);
    expect(rows()[2]).toBe("3 frames not listed");
  });

  it("shows the newest frames of a long list, not its oldest", () => {
    render(<FrameList frames={Array.from({ length: 3000 }, (_, index) => frame(index + 1))} />);

    expect(rows().length).toBeLessThan(100);
    expect(rows().at(-1)).toMatch(/^3000/);
    expect(rows().some((row) => row.startsWith("1003:"))).toBe(false);
  });
});
