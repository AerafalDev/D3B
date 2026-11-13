import { create } from "zustand";

import { commands, events, type FrameAnswer, type FrameSummary_Serialize } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";

/** A frame as the inspector lists it (`docs/protocol.md`, "The inspector"). */
export type FrameSummary = FrameSummary_Serialize;

/** Which side's frames the list shows. */
export type SideShown = "all" | "client" | "server";

/**
 * How many frames the list keeps, the newest: as many as the back end keeps per connection at most (it keeps fewer when
 * they pass 16 MiB, and then the oldest listed have no detail any more).
 */
export const KEPT = 3000;

interface InspectorStore {
  /** The connection whose frames are listed; `null` when none is watched. */
  connection: number | null;
  /** Its frames, oldest first, at most [`KEPT`]. */
  frames: FrameSummary[];
  /** The number of the frame chosen, whose detail shows. */
  selected: number | null;
  /** Whether the frame chosen is a secret, as listed when chosen: it may leave the list before its detail is read. */
  secret: boolean;
  /** The chosen frame's detail, or why it has none; `null` until the back end answers. */
  detail: FrameAnswer | null;
  /** Whether the list is paused: the frames streamed meanwhile are held, not listed. */
  paused: boolean;
  /** The frames streamed while paused, at most [`KEPT`]. */
  held: FrameSummary[];
  /** How many frames arrived while paused, those beyond what is held and the back end's skipped ones included. */
  arrived: number;
  /** The side shown, and the text a frame's name must hold to be shown; kept from one connection to the next. */
  side: SideShown;
  name: string;
  /**
   * Watches `connection`, or none: the back end answers with the frames it keeps, then streams the next. Watching the
   * connection watched already changes nothing.
   */
  watch: (connection: number | null) => void;
  /** Chooses a frame of the connection watched and asks its detail. */
  select: (number: number) => void;
  /** Pauses the list, or resumes it with the frames held meanwhile. */
  pause: (paused: boolean) => void;
  setSide: (side: SideShown) => void;
  setName: (name: string) => void;
  /** Follows the frames streamed, after watching none; returns what stops following. */
  follow: () => () => void;
}

/**
 * `frames` with `incoming` merged by number, the last [`KEPT`]. Both are in order, and a connection numbers its frames
 * one after another, so `incoming` either comes after `frames` or, the frames kept answered after a first batch was
 * streamed, before and up to it.
 */
function merged(frames: FrameSummary[], incoming: FrameSummary[]): FrameSummary[] {
  const first = frames[0];
  const last = frames.at(-1);
  let all: FrameSummary[];
  if (first === undefined || last === undefined) {
    all = incoming;
  } else {
    const before = incoming.filter((frame) => frame.number < first.number);
    const after = incoming.filter((frame) => frame.number > last.number);
    all = [...before, ...frames, ...after];
  }
  return all.length > KEPT ? all.slice(all.length - KEPT) : all;
}

// Each watch's turn: the answer of a watch overtaken by another is dropped. The same for each frame chosen.
let turn = 0;
let choice = 0;

// What a watch starts from: nothing listed, chosen or held.
const FRESH = { frames: [], selected: null, secret: false, detail: null, paused: false, held: [], arrived: 0 };

export const useInspector = create<InspectorStore>((set, get) => ({
  connection: null,
  ...FRESH,
  side: "all",
  name: "",

  watch: (connection) => {
    if (connection === get().connection) {
      return;
    }
    turn += 1;
    const mine = turn;
    choice += 1;
    set({ connection, ...FRESH });
    if (!isTauri()) {
      return;
    }
    void commands.inspectorWatch(connection).then((kept) => {
      if (mine === turn && connection !== null) {
        // Paused since the watch began, the list stands still: the frames kept wait with those held.
        set((store) => (store.paused ? { held: merged(store.held, kept) } : { frames: merged(store.frames, kept) }));
      }
    });
  },

  select: (number) => {
    const connection = get().connection;
    choice += 1;
    const mine = choice;
    const secret = get().frames.some((frame) => frame.number === number && frame.outcome === "secret");
    set({ selected: number, secret, detail: null });
    if (connection === null || !isTauri()) {
      return;
    }
    void commands.inspectorFrame(connection, number).then((detail) => {
      if (mine === choice) {
        set({ detail });
      }
    });
  },

  pause: (paused) =>
    set((store) =>
      paused ? { paused, arrived: 0 } : { paused, frames: merged(store.frames, store.held), held: [], arrived: 0 },
    ),

  setSide: (side) => set({ side }),
  setName: (name) => set({ name }),

  follow: () => {
    if (!isTauri()) {
      return () => {};
    }
    let live = true;
    const unlisten = events.inspectorFrames.listen((event) => {
      const { connection, frames, skipped } = event.payload;
      if (live && connection === get().connection) {
        set((store) =>
          store.paused
            ? { held: merged(store.held, frames), arrived: store.arrived + frames.length + skipped }
            : { frames: merged(store.frames, frames) },
        );
      }
    });
    // The back end may still stream to a page reloaded: it is told to stop, whatever this store believes.
    turn += 1;
    choice += 1;
    set({ connection: null, ...FRESH });
    void commands.inspectorWatch(null);
    return () => {
      live = false;
      void unlisten.then((stop) => stop());
    };
  },
}));
