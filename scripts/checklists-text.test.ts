import { describe, expect, it } from "vitest";
import { CAP, type Checklist, checklistProblems, NOW_LIMIT, statusOf } from "./checklists-text.ts";

const file = (name: string, status: string): Checklist => ({
  name,
  text: `# ${name}\n\nStatus: ${status}\n\n1. an item;\n`,
});
const lines = (count: number) => Array.from({ length: count }, (_, i) => `line ${i}`).join("\n");
const indexOf = (checklists: Checklist[]) =>
  `# Now\n\n${checklists.map((checklist) => `- [x](checklists/${checklist.name}): open.`).join("\n")}\n`;
const opened = (count: number) => Array.from({ length: count }, (_, i) => file(`open-${i}.md`, "open"));

describe("statusOf", () => {
  it("reads the status line under the title", () => {
    expect(statusOf("# A\n\nStatus: open\n")).toBe("open");
    expect(statusOf("# A\n\nStatus: waiting\n")).toBe("waiting");
    expect(statusOf("# A\r\n\r\nStatus: measured\r\n")).toBe("measured");
  });

  it("refuses a file without one, or with another word", () => {
    expect(statusOf("# A\n\n1. an item;\n")).toBeUndefined();
    expect(statusOf("# A\n\nStatus: done\n")).toBeUndefined();
    expect(statusOf("# A\n\nThe status: open, it says\n")).toBeUndefined();
  });
});

describe("checklistProblems", () => {
  it("counts only the open checklists", () => {
    const checklists = [file("a.md", "open"), file("b.md", "waiting"), file("c.md", "measured")];
    const result = checklistProblems({ now: indexOf(checklists), checklists });
    expect(result).toEqual({ open: 1, nowLines: 5, problems: [] });
  });

  it("refuses a now.md over its limit", () => {
    expect(checklistProblems({ now: lines(NOW_LIMIT), checklists: [] })).toEqual({
      open: 0,
      nowLines: NOW_LIMIT,
      problems: [],
    });
    expect(checklistProblems({ now: `${lines(NOW_LIMIT + 1)}\n`, checklists: [] }).problems).toEqual([
      `docs/now.md: ${NOW_LIMIT + 1} lines (limit ${NOW_LIMIT}): move what is not the step, the next items or the index out`,
    ]);
  });

  it("refuses a checklist without a status", () => {
    const checklists = [{ name: "a.md", text: "# A\n\n1. an item;\n" }];
    expect(checklistProblems({ now: indexOf(checklists), checklists }).problems).toEqual([
      "docs/checklists/a.md: no `Status: open`, `waiting` or `measured` line under its title",
    ]);
  });

  it("refuses an open or waiting checklist that now.md does not list", () => {
    const checklists = [file("a.md", "open"), file("b.md", "waiting"), file("c.md", "measured")];
    expect(checklistProblems({ now: "# Now\n", checklists }).problems).toEqual([
      "docs/now.md: no line for checklists/a.md (open)",
      "docs/now.md: no line for checklists/b.md (waiting)",
    ]);
  });

  it("lets a pull request that is not a feature through over the cap", () => {
    const checklists = opened(CAP + 1);
    for (const title of ["fix(bot): a bug", "docs: a page", "ci: a job", "featherweight: no type"]) {
      const result = checklistProblems({ now: indexOf(checklists), checklists, pr: { title, labels: [] } });
      expect(result.problems).toEqual([]);
    }
  });

  it("holds a feature back over the cap, unless it finishes the started one", () => {
    const checklists = opened(CAP + 1);
    const now = indexOf(checklists);
    const problem = `${CAP + 1} checklists open (cap ${CAP}): a new feature waits for the owner's measures; the remaining PRs of the feature already started carry the \`step-finish\` label`;
    for (const title of ["feat(bot): a node", "feat: a node", "feat(ui)!: a node"]) {
      expect(checklistProblems({ now, checklists, pr: { title, labels: [] } }).problems).toEqual([problem]);
    }
    const finishing = { title: "feat(bot): a node", labels: ["size-exception", "step-finish"] };
    expect(checklistProblems({ now, checklists, pr: finishing }).problems).toEqual([]);
  });

  it("lets a feature through at the cap", () => {
    const checklists = opened(CAP);
    const result = checklistProblems({ now: indexOf(checklists), checklists, pr: { title: "feat: x", labels: [] } });
    expect(result).toEqual({ open: CAP, nowLines: CAP + 2, problems: [] });
  });
});
