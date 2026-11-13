# Icons

The app's icons are [Lucide](https://lucide.dev)'s, from the `lucide-react` package already used by the shadcn
components (ISC licence: bundled into the app's code without its notice, so the packages must carry it, as `docs/shell.md`
says): the owner chose Lucide for every icon on 2026-11-13, replacing the
JetBrains icons copied here before.

- `icons.ts` names those in use, each with its colour when it marks a problem or an offer; `icon.tsx` draws one,
  decorative, with a 1.5 px stroke.
- A new icon is imported from `lucide-react` and named in `icons.ts`.
