import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

// The back end and the editor area, faked: what Rust undoes and redoes, and the editors opened.
const backEnd = vi.hoisted(() => ({ undo: vi.fn(), redo: vi.fn(), editors: [] as { id: string }[] }));
vi.mock("@/bindings.ts", () => ({ commands: { undoZone: backEnd.undo, redoZone: backEnd.redo } }));
vi.mock("@/shell/parts/editor-store.ts", () => ({
  openEditor: (editor: { id: string }) => backEnd.editors.push(editor),
  useEditors: { getState: () => ({ active: null, api: null }) },
}));
vi.mock("sonner", () => ({ toast: { error: () => {} } }));

const { useZoneUndo } = await import("./zone-undo.ts");
const { useZones } = await import("./zones-store.ts");
const { useWorld } = await import("@/world/world-store.ts");

const zone = (id: string, world = 1) => ({ id, name: id, world, maps: [] });

function Probe() {
  const box = useZoneUndo<HTMLDivElement>();
  return (
    <div ref={box} tabIndex={-1} data-testid="box">
      <input aria-label="name" />
    </div>
  );
}

/** Ctrl and the letter typed, on the key AZERTY puts it: Z where QWERTY has W. */
function press(target: Element, key: string, shiftKey = false) {
  return act(async () => {
    const code = key === "z" ? "KeyW" : `Key${key.toUpperCase()}`;
    fireEvent.keyDown(target, { key: shiftKey ? key.toUpperCase() : key, code, ctrlKey: true, shiftKey });
    fireEvent.keyUp(target, { key: shiftKey ? key.toUpperCase() : key, code, ctrlKey: true, shiftKey });
  });
}

describe("undo and redo of my zones on the World", () => {
  beforeEach(() => {
    backEnd.undo.mockReset();
    backEnd.redo.mockReset();
    backEnd.editors.length = 0;
    useZones.setState({ zones: [zone("a"), zone("b")] });
    useWorld.setState({ zone: "a", world: 1, picked: null });
  });
  afterEach(cleanup);

  it("undoes on Ctrl+Z, opening the zone it changed on the World tab, on its world", async () => {
    backEnd.undo.mockResolvedValue({ id: "b", zone: zone("b", 2) });
    render(<Probe />);
    const box = screen.getByTestId("box");
    box.focus();
    await press(box, "z");
    expect(backEnd.undo).toHaveBeenCalledTimes(1);
    expect(useWorld.getState()).toMatchObject({ zone: "b", world: 2 });
    expect(backEnd.editors.map((editor) => editor.id)).toEqual(["world"]);
  });

  it("redoes on Ctrl+Y and Ctrl+Shift+Z, closing the zone open once it is gone", async () => {
    backEnd.redo.mockResolvedValue({ id: "a", zone: null });
    render(<Probe />);
    const box = screen.getByTestId("box");
    box.focus();
    await press(box, "y");
    expect(useWorld.getState().zone).toBeNull();
    expect(useZones.getState().zones.map((one) => one.id)).toEqual(["b"]);
    await press(box, "z", true);
    expect(backEnd.redo).toHaveBeenCalledTimes(2);
    expect(backEnd.undo).not.toHaveBeenCalled();
    expect(backEnd.editors).toEqual([]);
  });

  it("leaves Ctrl+Z to a field being typed in, and does nothing when nothing was undone", async () => {
    backEnd.undo.mockResolvedValue(null);
    render(<Probe />);
    const field = screen.getByRole("textbox", { name: "name" });
    field.focus();
    await press(field, "z");
    expect(backEnd.undo).not.toHaveBeenCalled();
    const box = screen.getByTestId("box");
    box.focus();
    await press(box, "z");
    expect(backEnd.undo).toHaveBeenCalledTimes(1);
    expect(useWorld.getState().zone).toBe("a");
    expect(backEnd.editors).toEqual([]);
  });
});
