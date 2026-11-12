import { beforeEach, describe, expect, it, vi } from "vitest";

import type { RelayStatus } from "@/bindings.ts";

type Listener = (event: { payload: RelayStatus }) => void;

const backEnd = vi.hoisted(() => ({
  listener: null as Listener | null,
  answer: null as ((status: RelayStatus) => void) | null,
  unlisten: vi.fn(),
}));

vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => true }));
vi.mock("@/bindings.ts", () => ({
  commands: {
    relayStatus: () =>
      new Promise<RelayStatus>((resolve) => {
        backEnd.answer = resolve;
      }),
  },
  events: {
    relayChanged: {
      listen: async (listener: Listener) => {
        backEnd.listener = listener;
        return backEnd.unlisten;
      },
    },
  },
}));

const { useRelay } = await import("./relay-store.ts");

function status(state: RelayStatus["state"], gameConnections = 0): RelayStatus {
  return { state, reason: null, clientBuild: null, gameConnections, notUnderstood: 0 };
}

describe("useRelay", () => {
  beforeEach(() => {
    useRelay.setState({ status: null });
    backEnd.listener = null;
    backEnd.answer = null;
  });

  it("takes the first answer, then each change", async () => {
    useRelay.getState().follow();
    await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());

    backEnd.answer?.(status("starting"));
    await vi.waitFor(() => expect(useRelay.getState().status?.state).toBe("starting"));
    backEnd.listener?.({ payload: status("ready", 1) });

    expect(useRelay.getState().status).toEqual(status("ready", 1));
  });

  it("keeps a change heard before the first answer comes", async () => {
    useRelay.getState().follow();
    await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());

    backEnd.listener?.({ payload: status("ready", 2) });
    backEnd.answer?.(status("starting"));
    await Promise.resolve();

    expect(useRelay.getState().status).toEqual(status("ready", 2));
  });

  it("stops listening when it stops following", async () => {
    const stop = useRelay.getState().follow();
    await vi.waitFor(() => expect(backEnd.listener).not.toBeNull());

    stop();

    await vi.waitFor(() => expect(backEnd.unlisten).toHaveBeenCalledOnce());
  });
});
