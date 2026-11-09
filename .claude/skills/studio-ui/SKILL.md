---
name: studio-ui
description: Build or change a view of the D3B studio - Avalonia, ShadUI, CommunityToolkit.Mvvm - its look, its scale, its texts in French and English, its view model and its tests. Use before touching a file of src/D3B.App.
---

The studio is dark, docked and drawn from one scale. Read `docs/architecture.md`, section "Desktop app", and the mockup
`docs/mockups/studio-7c.html` (the frame), `docs/mockups/route-7d.html` (the routes) and `docs/mockups/studio-7r.html` (the
studio seen as an engine, with its world viewport) before drawing: they are the reference the owner judges against. The
decisions behind them are D159 to D163, D320 to D356 and D395 to D397 in the index.

## The look

- **Colours** come from `Themes/Colors.axaml` through `{DynamicResource ...}` (`BorderColor`, `MutedColor`, `BrightColor`,
  `DimColor`, `ClearColor`). A hexadecimal colour in a view is a defect. Dark only (D320); one blue accent; green, amber and
  red say states.
- **One scale** (D159, D163, D335), held by `Themes/Styles.axaml`: spacings 4, 8, 12, 16, 24 and 32; the studio's text 12.5
  and its small text 11; the pages' fonts 12, 14, 16, 22 and 28 (`Caption`, text, `Title`, `PageTitle`, `Figure`); the
  studio's controls 24 to 30 high (`StudioButton` with `Tool`, `TabFace`, `TitleSearch`, `Status`), the pages' 32 and 36;
  radii 6, 7, 10 and 12; icons 12, 14, 16 and 20.
- **Reuse a style before writing one**: `Dim`, `Muted`, `Bright`, `Outline`, `Tool Small`, `Key`, `Section`, `MenuPopup` are
  the classes the views use most. A new style goes in `Themes/Styles.axaml`, not in a view.
- **A surface lit on hover rests on `ClearColor`**, the hover's grey with no alpha, never on `Transparent`, which a brush
  transition flashes through.
- **Icons**: IntelliJ's, as `<Icon>` with a key such as `General.Search`, from `Themes/IntelliJIcons.axaml`, which
  `scripts/icons.cs` writes and `--check` compares (D331): never edit it by hand. The game's own concepts are `GlyphIcon`,
  characters `Portrait`. A new control goes in `D3B.App.Controls`.
- **Motion** is short and eases out (D320, D347): reuse the transitions of `Themes/Controls.axaml` and `FadeIn`.
- **A page** is an `AppPage`: its title or a header of its own, its actions on the right, the same margins. A shortcut is
  said with a keycap (`Border.Key`) and, in a tooltip, a `KeyTip`. A setting's help is a tooltip on a mark beside its name.

## The structure

- A view is an `.axaml` with `x:DataType` and `x:ClassModifier="internal"`, and a code-behind that does only what the view
  alone can do. The state and the commands are the view model's: `[ObservableProperty]` and `[RelayCommand]` on a `partial`
  class, in `D3B.App.ViewModels`. A view model never holds a control.
- **Dialogs and toasts** go through `Dialogs` (a `QuestionViewModel` or a dialog's own view model) and `Toasts`; the view
  model opens, closes and says, and is tested without a window.
- **Every text is a resource**: `{l:Tr Key}` in a view, `Localizer.Instance["Key"]` in a view model, and the key in both
  `Localization/Strings.resx` (English) and `Strings.fr.resx` (French, with its accents). A key missing shows as `[Key]`.
- Work for the bots and the host never runs on the UI thread (D91); a view model that reaches back to it goes through the
  dispatcher.

## The tests

- A view model is a plain test with no window.
- A view is a headless test (`HeadlessApp`, drawn by Skia) in `tests/D3B.App.Tests/Views`: run the code on the UI thread with
  `HeadlessUnitTestSession.GetOrStartForAssembly(...).Dispatch(...)`, then read what is visible with
  `GetVisualDescendants()`, as `StudioViewsTests` does. Test what a person would see: the text, the buttons the state
  allows, the control present or gone.
- The look is judged by the owner against the mockup, not by a screenshot test. Run the app with
  `dotnet run --project src/D3B.App` when the task changes what is drawn, and say what you saw, or that you did not run it.

Done when `dotnet build` is green, both `.resx` files carry the new keys, `dotnet test --project tests/D3B.App.Tests` passes,
and the report says whether the change was seen in the running app.
