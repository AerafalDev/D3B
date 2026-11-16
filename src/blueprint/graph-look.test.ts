import { describe, expect, it } from "vitest";

import { wirePath } from "./graph-look.ts";

/** Every pair of numbers of a path, its control points among them. */
function corners(path: string): [number, number][] {
  const numbers = path.match(/-?\d+(\.\d+)?/g)?.map(Number) ?? [];
  const points: [number, number][] = [];
  for (let index = 0; index + 1 < numbers.length; index += 2) {
    points.push([numbers[index] ?? 0, numbers[index + 1] ?? 0]);
  }
  return points;
}

describe("a wire's path", () => {
  it("curves forward when its input is to the right of its output", () => {
    const path = wirePath({ x: 0, y: 0 }, { x: 300, y: 50 }, 80);
    expect(path.startsWith("M0,0 C")).toBe(true);
    expect(Math.max(...corners(path).map(([, y]) => y))).toBe(50);
  });

  it("going back, leaves right, runs under both nodes and comes into its input from the left", () => {
    const path = wirePath({ x: 1340, y: 15 }, { x: 240, y: 15 }, 86);
    const points = corners(path);
    expect(points[0]).toEqual([1340, 15]);
    expect(points.at(-1)).toEqual([240, 15]);
    // Out on the right of the output, under the lowest node's bottom, in on the left of the input.
    expect(Math.max(...points.map(([x]) => x))).toBeGreaterThan(1340);
    expect(Math.max(...points.map(([, y]) => y))).toBeGreaterThan(86);
    expect(Math.min(...points.map(([x]) => x))).toBeLessThan(240);
  });

  it("curves forward from 48 px to the right, and loops under the nodes nearer", () => {
    expect(wirePath({ x: 0, y: 0 }, { x: 48, y: 0 }, 30)).toContain(" C");
    const near = corners(wirePath({ x: 0, y: 0 }, { x: 47, y: 0 }, 30));
    expect(Math.max(...near.map(([, y]) => y))).toBe(70);
  });

  it("never runs its bottom back to the right when its input is just right of its output", () => {
    const points = corners(wirePath({ x: 0, y: 0 }, { x: 40, y: 200 }, 260));
    const bottom = points.filter(([, y]) => y === 300).map(([x]) => x);
    // Along the bottom it only goes left: each point no further right than the one before.
    expect(bottom).toEqual([...bottom].sort((a, b) => b - a));
    expect(points.at(-1)).toEqual([40, 200]);
  });
});
