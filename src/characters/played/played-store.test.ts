import i18next from "i18next";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";

// The back end and the notifications, faked: what Forget asks, and what it says.
const backEnd = vi.hoisted(() => ({ forget: vi.fn(), toasts: [] as string[] }));
vi.mock("@/bindings.ts", () => ({
  commands: { forgetCharacter: backEnd.forget },
  events: {},
}));
vi.mock("sonner", () => ({ toast: { error: (text: string) => backEnd.toasts.push(text) } }));

const { usePlayed } = await import("./played-store.ts");

const kalyx = {
  key: "294-2",
  name: "Kalyx",
  level: 54,
  breedName: null,
  head: null,
  sex: null,
  lastSeen: 0,
  place: null,
};

describe("Forget", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  beforeEach(() => {
    backEnd.forget.mockReset();
    backEnd.toasts.length = 0;
  });

  it("asks the back end to forget the character's file, and says nothing when it did", async () => {
    backEnd.forget.mockResolvedValue(null);
    await usePlayed.getState().forget(kalyx);
    expect(backEnd.forget).toHaveBeenCalledWith("294-2");
    expect(backEnd.toasts).toEqual([]);
  });

  it("says why in a notification when the file could not be forgotten", async () => {
    backEnd.forget.mockRejectedValueOnce({ kind: "noSuchCharacter" });
    await usePlayed.getState().forget(kalyx);
    backEnd.forget.mockRejectedValueOnce({ kind: "notForgotten", reason: "the trash is full" });
    await usePlayed.getState().forget(kalyx);
    expect(backEnd.toasts).toEqual([
      "Kalyx was not forgotten: its file is gone.",
      "Kalyx was not forgotten: the trash is full",
    ]);
  });
});
