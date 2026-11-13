import { beforeEach, describe, expect, it, vi } from "vitest";

import { openEditor, useEditors } from "./editor-store.ts";

// A fake dockview API: the panels open by id, adding one, and bringing one forward.
function fakeApi() {
  const panels = new Map<string, { api: { setActive: ReturnType<typeof vi.fn> } }>();
  return {
    panels,
    getPanel: (id: string) => panels.get(id),
    addPanel: vi.fn((options: { id: string }) => {
      panels.set(options.id, { api: { setActive: vi.fn() } });
    }),
  };
}

const editor = { id: "character:1", component: "character", title: "Lyra", params: { icon: "user" as const } };

describe("openEditor", () => {
  beforeEach(() => useEditors.setState({ api: null, active: null }));

  it("opens an editor once, and brings its tab forward when it is opened again", () => {
    const api = fakeApi();
    // The fake carries only what `openEditor` calls.
    useEditors.setState({ api: api as never });
    openEditor(editor);
    openEditor(editor);
    expect(api.addPanel).toHaveBeenCalledOnce();
    expect(api.panels.get("character:1")?.api.setActive).toHaveBeenCalledOnce();
  });

  it("does nothing before the editor area is mounted", () => {
    expect(() => openEditor(editor)).not.toThrow();
  });
});
