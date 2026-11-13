import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import type { IDockviewPanelHeaderProps } from "dockview-react";
import i18next from "i18next";
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";

import { initI18n } from "@/i18n/i18n.ts";
import type { EditorParams } from "./editor-store.ts";
import { EditorTab } from "./editor-tab.tsx";

// A fake panel API: its title, a way to change it as dockview does, and its close.
function fakeApi(title: string) {
  let listener: ((event: { title: string }) => void) | null = null;
  return {
    title,
    close: vi.fn(),
    onDidTitleChange: (next: (event: { title: string }) => void) => {
      listener = next;
      return { dispose: () => {} };
    },
    rename: (next: string) => listener?.({ title: next }),
  };
}

function renderTab(api: ReturnType<typeof fakeApi>) {
  // Only the panel's title, its close and its icon play a part in the tab.
  const props = { api, params: { icon: "user" } } as unknown as IDockviewPanelHeaderProps<EditorParams>;
  return render(<EditorTab {...props} />);
}

describe("an editor's tab", () => {
  beforeAll(async () => {
    initI18n();
    await i18next.changeLanguage("en");
  });
  afterEach(cleanup);

  it("shows its title, and follows it when it changes", () => {
    const api = fakeApi("Connection 1");
    renderTab(api);
    expect(screen.getByText("Connection 1")).toBeTruthy();
    act(() => api.rename("Lyra"));
    expect(screen.getByText("Lyra")).toBeTruthy();
  });

  it("closes its editor from its close button", () => {
    const api = fakeApi("Lyra");
    renderTab(api);
    fireEvent.click(screen.getByRole("button", { name: "Close Lyra" }));
    expect(api.close).toHaveBeenCalledOnce();
  });

  it("keeps a press on its close button from reaching dockview, which would activate or drag the tab", () => {
    const api = fakeApi("Lyra");
    renderTab(api);
    const close = screen.getByRole("button", { name: "Close Lyra" });
    expect(fireEvent.pointerDown(close)).toBe(false);
    expect(fireEvent.mouseDown(close)).toBe(false);
  });
});
