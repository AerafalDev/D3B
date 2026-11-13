import i18next from "i18next";
import { beforeAll, describe, expect, it } from "vitest";

import type { RelayStatus } from "@/bindings.ts";
import { initI18n } from "@/i18n/i18n.ts";
import { relayHover, relayLabel } from "./relay-hover.ts";

const ready: RelayStatus = {
  state: "ready",
  reason: null,
  clientBuild: "3.7.13.17",
  gameConnections: 0,
  notUnderstood: 0,
  connections: [],
};

const t = i18next.t.bind(i18next);

describe("the relay's label", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });

  it("counts the game connections once ready", () => {
    expect(relayLabel({ ...ready, gameConnections: 2 }, t)).toBe("Relay ready · 2 in game");
  });

  it("names the state otherwise", () => {
    expect(relayLabel({ ...ready, state: "failed" }, t)).toBe("Relay off");
    expect(relayLabel({ ...ready, state: "starting" }, t)).toBe("Relay starting…");
  });
});

describe("the relay's hover", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });

  it("tells to start Dofus after D3B, and nothing the bar already says", () => {
    expect(relayHover(ready, "3.7.13.17", t)).toEqual([
      ["Start Dofus after D3B: a client started before it plays without the relay."],
    ]);
  });

  it("warns that quitting ends the game connections while some are open", () => {
    expect(relayHover({ ...ready, gameConnections: 1 }, "3.7.13.17", t)).toEqual([
      [
        "Quitting D3B ends the game connections it relays now.",
        "Start Dofus after D3B: a client started before it plays without the relay.",
      ],
    ]);
  });

  it("names the client's build when the game data is of another one", () => {
    expect(relayHover({ ...ready, clientBuild: "3.7.14.2" }, "3.7.13.17", t)[0]).toEqual([
      "Client 3.7.14.2, game data of 3.7.13.17",
    ]);
  });

  it("names the client's build when there is no game data", () => {
    expect(relayHover(ready, null, t)[0]).toEqual(["Client 3.7.13.17"]);
  });

  it("gives the reason it is off, and no advice", () => {
    const failed: RelayStatus = { ...ready, state: "failed", reason: "port 5555 of 127.0.13.59 is taken" };
    expect(relayHover(failed, "3.7.13.17", t)).toEqual([["port 5555 of 127.0.13.59 is taken"]]);
  });

  it("gives no advice to start Dofus after D3B while the relay is not ready", () => {
    expect(relayHover({ ...ready, state: "starting" }, "3.7.13.17", t)).toEqual([]);
  });

  it("warns that quitting ends the game connections even once off, with no count on the bar", () => {
    const failed: RelayStatus = { ...ready, state: "failed", reason: "redirect lost", gameConnections: 1 };
    expect(relayHover(failed, "3.7.13.17", t)).toEqual([
      ["redirect lost"],
      ["Quitting D3B ends the game connections it relays now."],
    ]);
  });

  it("counts the connections that went straight to Ankama", () => {
    expect(relayHover({ ...ready, notUnderstood: 2 }, "3.7.13.17", t)[0]).toEqual([
      "2 connections went straight to Ankama: their server choice was not understood.",
    ]);
  });
});
