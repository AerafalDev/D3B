import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

// The app's window, faked: the drag and drop events it sends, and what stops listening to them.
const window_ = vi.hoisted(() => ({
  handler: null as ((event: { payload: { type: string; paths?: string[] } }) => void) | null,
  stopped: 0,
}));
vi.mock("@/lib/tauri.ts", () => ({ isTauri: () => true }));
vi.mock("@tauri-apps/api/webview", () => ({
  getCurrentWebview: () => ({
    onDragDropEvent: (handler: typeof window_.handler) => {
      window_.handler = handler;
      return Promise.resolve(() => {
        window_.stopped += 1;
      });
    },
  }),
}));

const { ImportPreview } = await import("./import-preview.tsx");
const { useShare } = await import("./share-store.ts");

describe("a file dropped on the window", () => {
  afterEach(cleanup);

  it("previews the first file dropped, and nothing while it is dragged over", async () => {
    const open = vi.fn().mockResolvedValue(undefined);
    useShare.setState({ preview: null, importing: false, open });
    const { unmount } = render(<ImportPreview />);
    window_.handler?.({ payload: { type: "enter", paths: ["a.d3b"] } });
    window_.handler?.({ payload: { type: "over" } });
    window_.handler?.({ payload: { type: "leave" } });
    expect(open).not.toHaveBeenCalled();
    window_.handler?.({ payload: { type: "drop", paths: ["C:\\a.d3b", "C:\\b.d3b"] } });
    expect(open.mock.calls).toEqual([["C:\\a.d3b"]]);
    window_.handler?.({ payload: { type: "drop", paths: [] } });
    expect(open).toHaveBeenCalledTimes(1);
    unmount();
    await Promise.resolve();
    expect(window_.stopped).toBe(1);
  });
});
