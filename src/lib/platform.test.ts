import { describe, expect, it } from "vitest";

import { platformOf } from "./platform.ts";

describe("platformOf", () => {
  it("reads the system from the webview's user agent", () => {
    expect(platformOf("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Edg/131.0")).toBe("windows");
    expect(platformOf("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15")).toBe("mac");
  });

  it("takes any other system for Linux", () => {
    expect(platformOf("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/605.1.15")).toBe("linux");
    expect(platformOf("")).toBe("linux");
  });
});
