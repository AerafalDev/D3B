import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";

import { Icon } from "./icon.tsx";

function drawn(name: Parameters<typeof Icon>[0]["name"]) {
  const { container } = render(<Icon name={name} />);
  const icon = container.querySelector("[data-icon]");
  if (icon === null) {
    throw new Error("no icon drawn");
  }
  return icon;
}

describe("an icon", () => {
  afterEach(cleanup);

  it("is grey and hidden from assistive technologies, its control carrying the label", () => {
    const icon = drawn("close");
    expect(icon.getAttribute("class")).toContain("text-[#ced0d6]");
    expect(icon.getAttribute("aria-hidden")).toBe("true");
  });

  it("takes its colour when it marks a problem or an offer, and not the grey", () => {
    for (const [name, tone] of [
      ["warning", "text-[#f2c55c]"],
      ["error", "text-[#db5c5c]"],
      ["update", "text-[#c77d55]"],
    ] as const) {
      const classes = drawn(name).getAttribute("class") ?? "";
      expect(classes, name).toContain(tone);
      expect(classes, name).not.toContain("text-[#ced0d6]");
      cleanup();
    }
  });
});
