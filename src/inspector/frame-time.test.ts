import { describe, expect, it } from "vitest";

import { frameTime } from "./frame-time.ts";

describe("frameTime", () => {
  it("writes minutes, seconds, milliseconds, and hours once there are some", () => {
    expect(frameTime(0)).toBe("00:00.000");
    expect(frameTime(192_418)).toBe("03:12.418");
    expect(frameTime(3_599_999)).toBe("59:59.999");
    expect(frameTime(3_600_000)).toBe("1:00:00.000");
    expect(frameTime(37_812_005)).toBe("10:30:12.005");
  });
});
