import { create } from "zustand";

import { commands, events, type FrameSummary_Serialize } from "@/bindings.ts";
import { isTauri } from "@/lib/tauri.ts";

/** A frame as the inspector lists it (`docs/protocol.md`, "The inspector"). */
export type FrameSummary = FrameSummary_Serialize;

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
  /**
   * Watches `connection`, or none: the back end answers with the frames it keeps, then streams the next. Watching the
   * connection watched already changes nothing.
   */
  watch: (connection: number | null) => void;
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

// Each watch's turn: the answer of a watch overtaken by another is dropped.
let turn = 0;

export const useInspector = create<InspectorStore>((set, get) => ({
  connection: null,
  frames: [],

  watch: (connection) => {
    if (connection === get().connection) {
      return;
    }
    turn += 1;
    const mine = turn;
    set({ connection, frames: [] });
    if (!isTauri()) {
      return;
    }
    void commands.inspectorWatch(connection).then((kept) => {
      if (mine === turn && connection !== null) {
        set({ frames: merged(get().frames, kept) });
      }
    });
  },

  follow: () => {
    if (!isTauri()) {
      return () => {};
    }
    let live = true;
    const unlisten = events.inspectorFrames.listen((event) => {
      const { connection, frames } = event.payload;
      if (live && connection === get().connection) {
        set({ frames: merged(get().frames, frames) });
      }
    });
    // The back end may still stream to a page reloaded: it is told to stop, whatever this store believes.
    turn += 1;
    set({ connection: null, frames: [] });
    void commands.inspectorWatch(null);
    return () => {
      live = false;
      void unlisten.then((stop) => stop());
    };
  },
}));
