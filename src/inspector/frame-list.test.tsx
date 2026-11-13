import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
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

function range(from: number, to: number): FrameSummary[] {
  return Array.from({ length: to - from + 1 }, (_, index) => frame(from + index));
}

/** The list's box, laid out as happy-dom cannot: ten rows in view, `count()` rows in all, a settable scroll. */
function scrolling(count: () => number) {
  const box = screen.getByRole("list").parentElement;
  if (box === null) {
    throw new Error("the list has no scrolling box");
  }
  let top = 0;
  Object.defineProperty(box, "clientHeight", { configurable: true, get: () => 220 });
  Object.defineProperty(box, "scrollHeight", { configurable: true, get: () => count() * 22 });
  Object.defineProperty(box, "scrollTop", {
    configurable: true,
    get: () => top,
    set: (value: number) => {
      top = value;
    },
  });
  return {
    box,
    scrollTo(value: number) {
      top = value;
      fireEvent.scroll(box);
    },
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

  it("marks a frame not trusted, and it alone, as a problem", () => {
    render(<FrameList frames={[frame(1, { outcome: "notTrusted" }), frame(2, { outcome: "unknown" })]} />);

    expect(screen.getByText("not trusted").className).toContain("text-error");
    expect(screen.getByText("unknown").className).not.toContain("text-error");
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

  it("follows the newest frames while scrolled to its end", () => {
    const { rerender } = render(<FrameList frames={range(1, 100)} />);

    rerender(<FrameList frames={range(1, 110)} />);

    expect(rows().at(-1)).toMatch(/^110/);
  });

  it("scrolled up, keeps the row at its top in place as the oldest frames go", () => {
    let count = 3000;
    const { rerender } = render(<FrameList frames={range(1, 3000)} />);
    const { box, scrollTo } = scrolling(() => count);
    scrollTo(1000 * 22 + 5);

    count = 3000;
    rerender(<FrameList frames={range(51, 3050)} />);

    // Frame 1001 was the 1001st row and is now the 951st: the view moved with it, and still draws it.
    expect(box.scrollTop).toBe(950 * 22 + 5);
    expect(rows().some((row) => row.startsWith("100103:"))).toBe(true);
    expect(rows().some((row) => row.startsWith("3050"))).toBe(false);
  });

  it("follows again once scrolled back to its end", () => {
    let count = 3000;
    const { rerender } = render(<FrameList frames={range(1, 3000)} />);
    const { scrollTo } = scrolling(() => count);
    scrollTo(1000 * 22);
    scrollTo(3000 * 22 - 220);

    count = 3010;
    rerender(<FrameList frames={range(1, 3010)} />);

    expect(rows().at(-1)).toMatch(/^3010/);
  });
});
