# MauiNaluApp — guide for AI coding agents

.NET MAUI app (iOS + Android) hosted by the **Nalu Scaffold** (`Nalu.Maui.Scaffold`) with **Nalu
model-first navigation** (`Nalu.Maui.Navigation`). Pages are plain `ContentPage`s; the tab bar,
nav bar, transitions and overlays are MAUI views drawn by the scaffold — there is no Shell and no
`NavigationPage`.

## Skills — load before touching the corresponding area

Detailed, verified knowledge lives in `.claude/skills/*/SKILL.md` (each ≈ 1 page; some link a
`reference.md` for the long tail). Read the skill for the area you are about to change; do not guess
Nalu APIs from Shell/NavigationPage habits.

| Task | Skill |
|---|---|
| Navigate between pages, pass data (intents), register pages, tab/root switching | `nalu-navigation` |
| Where to put load/save/cleanup code; lifecycle order; guards; state restoration; testing page models | `nalu-navigation-lifecycle` |
| Tab bar / roots / flyout, nav bar (title, buttons, appearance), safe areas & system bars | `nalu-scaffold-structure` |
| Parallax headers, materializing nav bar, anything driven by the scroll offset (`{nalu:ScrollValue}`) | `nalu-scaffold-scroll` |
| Page transitions, shared elements, modal pages, predictive back, migrating Shell/NavigationPage code | `nalu-scaffold-transitions` |
| Popups, bottom sheets, tab bar panels, `IOverlayService` (MVVM overlays) | `nalu-scaffold-overlays` |
| Anything involving the soft keyboard (entries under the keyboard, `Scaffold.KeyboardMode`) | `nalu-scaffold-keyboard` |

## Project map

| Path | Role |
|---|---|
| `MauiProgram.cs` | `UseNaluNavigation<App>(nav => nav.AddPages())` + `UseNaluScaffold()`; register services here. `AddPages()` is source-generated. |
| `AppScaffold.xaml` | The whole app structure: `nalu:Scaffold` → `ScaffoldTabBar` → `ScaffoldRoot`s (`PageType`). Global nav bar appearance and page transition. |
| `Pages/*.xaml(.cs)` | `ContentPage`s. Constructor takes the page model and assigns `BindingContext` (that is how the generator pairs them). |
| `PageModels/*.cs` | `ObservableObject` (CommunityToolkit.Mvvm) page models; get `INavigationService` by DI; navigate with `Nav.Push<TModel>()` / `Nav.Pop()`. |
| `GlobalUsings.cs` | `global using Nalu;` and `global using Nav = Nalu.Navigation;` |
| `Resources/Styles/*.xaml` | Colors/styles used by the scaffold chrome (`Accent`, `TabIcon`, `Card`, `Muted`…). |

## Conventions

- Add a page = add `Pages/FooPage.xaml(.cs)` + `PageModels/FooPageModel.cs`; the page ctor takes
  `FooPageModel` and sets `BindingContext`. Nothing to register — `AddPages()` finds it. Use
  `x:DataType` on every page.
- Navigation only through `INavigationService.GoToAsync(Nav....)`. Never `Navigation.PushAsync`,
  `Shell.Current`, `PushModalAsync`.
- New tab = new `ScaffoldRoot` in `AppScaffold.xaml`.
- Overlays (dialogs, sheets) = scaffold popups/bottom sheets, not `DisplayAlert`-style platform
  modals when custom UI is needed.
- Keep MAUI at the `MauiVersion` pin in the csproj (10.0.100; the scaffold's own floor is 10.0.90) — do not
  drop below it: the workload default still has Android chrome rendering bugs. iOS 15+ / Android API 30+.

## Build & run

```bash
dotnet build -f net10.0-android -t:Run     # Android emulator/device
dotnet build -f net10.0-ios -t:Run         # iOS simulator (macOS)
```
