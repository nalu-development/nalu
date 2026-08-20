# Migrating from NaluShell to Scaffold

The Scaffold replaces MAUI `Shell` as the *host*; your **navigation code does not change** —
same `INavigationService`, same relative/absolute navigations, same page models, intents,
guards and lifecycle events. The migration is a re-declaration of the app structure plus
swapping a few Shell-specific chrome features for their Scaffold equivalents.

## Concept map

| NaluShell / Shell | Scaffold |
|-------------------|----------|
| `NaluShell` subclass | `Scaffold` subclass |
| `TabBar` + `Tab` + `ShellContent` | `ScaffoldTabBar` + `ScaffoldRoot` |
| `ShellContent nalu:Navigation.PageType="..."` | `ScaffoldRoot PageType="{x:Type ...}"` |
| Shell flyout | `Scaffold.FlyoutStart`/`FlyoutEnd` + `ScaffoldFlyoutMenuView` |
| `Shell.TitleView` | `nalu:Scaffold.TitleView` (page-attached) |
| `Shell.NavBarIsVisible` | `nalu:Scaffold.IsNavBarVisible` |
| Shell tab bar visibility | `nalu:Scaffold.TabBarVisibility` (`Auto`/`Visible`/`Hidden`) |
| NaluTabBar (`UseNaluTabBar` + `SetTabBarView`) | Built-in default tab bar (or `ScaffoldTabBar.TabBarView`) |
| Shell modal routes / `PresentationMode` | `nalu:Scaffold.PageMode` (`Modal`/`DismissableModal`) |
| Native nav bar styling (`Shell.BackgroundColor`, …) | `Scaffold.NavBarBackground`/`NavBarForeground`/… attached properties (per-property page → area → scaffold merge) |
| Native page transitions | `Scaffold.PageTransition` specs + `Scaffold.TransitionName` shared elements |

## Step by step

### 1. Packages & registration

```csharp
builder
    .UseMauiApp<App>()
    .UseNaluNavigation<App>(nav => nav.AddPage<...>())   // unchanged
    .UseNaluScaffold();                                   // NEW — registers the handler and services (IOverlayService, IScaffoldFlyoutController)
```

Remove `UseNaluTabBar()` if you used the custom Shell tab bar — the Scaffold's default bar
replaces it.

### 2. Re-declare the structure

Before (`AppShell.xaml`):

```xml
<nalu:NaluShell xmlns:nalu="https://nalu-development.github.com/nalu/navigation" ...>
    <TabBar>
        <Tab Title="Home" Icon="home.png">
            <ShellContent nalu:Navigation.PageType="pages:HomePage" />
        </Tab>
        <Tab Title="Settings" Icon="gear.png">
            <ShellContent nalu:Navigation.PageType="pages:SettingsPage" />
        </Tab>
    </TabBar>
</nalu:NaluShell>
```

After (`AppScaffold.xaml` — note the different xmlns):

```xml
<nalu:Scaffold xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"
               nalu:Scaffold.NavBarView="{nalu:ScaffoldNavBarView}" ...>
    <nalu:ScaffoldTabBar>
        <nalu:ScaffoldRoot Title="Home" Icon="home.png"
                           PageType="{x:Type pages:HomePage}" />
        <nalu:ScaffoldRoot Title="Settings" Icon="gear.png"
                           PageType="{x:Type pages:SettingsPage}" />
    </nalu:ScaffoldTabBar>
</nalu:Scaffold>
```

The code-behind loses the Shell plumbing — a `Scaffold` subclass needs no constructor
arguments (structure defines the initial selection; use `InitialRootPageType` to override):

```csharp
public partial class AppScaffold : Scaffold
{
    public AppScaffold() => InitializeComponent();
}
```

### 3. Host it

```csharp
protected override Window CreateWindow(IActivationState? activationState)
    => new(_serviceProvider.GetRequiredService<AppScaffold>());
```

(Register `AppScaffold` as a singleton service.) The Android window-caching workaround
NaluShell needed is not required.

### 4. Move page-attached chrome properties

| Before | After |
|--------|-------|
| `Shell.TitleView` | `nalu:Scaffold.TitleView` — its `BindingContext` is the **page model** now |
| `Shell.NavBarIsVisible="False"` | `nalu:Scaffold.IsNavBarVisible="False"` |
| Tab bar hidden on pushed pages | opt-in per page: `nalu:Scaffold.TabBarVisibility="Auto"` (the default is `Visible`) |

The `TitleView` binding-context change is the most common migration fix: NaluShell's title
views bound against Shell internals; Scaffold title views bind your page model directly (and
`{nalu:NavBarBinding}` reaches the nav-bar ambient state when needed).

### 5. Navigation code — nothing to do

`INavigationService`, `Navigation.Relative()...`, intents, `ILeavingGuard`,
`IEnteringAware`/`IAppearingAware` etc. work unchanged. The Shell-specific relative-navigation
caveats (route registration, `//` prefixes) never applied to Nalu APIs, so there is nothing to
port.

`INavigation`-based automation (e.g. test drivers popping pages) keeps working: the Scaffold
installs a truthful `INavigation` bridge whose pops route through the engine.

### 6. Adopt what Shell couldn't do

Once migrated, the features that motivated the move are one attribute away — full-bleed
headers with scroll-materializing bars ([Nav Bar](scaffold-navbar.md)), shared-element
push/pop ([Transitions](scaffold-transitions.md)), popups/sheets without platform modals
([Popups & Sheets](scaffold-overlays.md)), and automatic status-bar contrast
([System Bars](scaffold-systembars.md)).

## Behavior differences worth knowing

- **Every navigation is engine-routed** — including tab taps from chrome. If you relied on
  Shell's native tab switching bypassing guards, that no longer happens (by design).
- **Safe areas**: pages get the correct insets from the chrome automatically (bar footprints
  contribute as insets unless `NavBarOverlapsContent`); page content uses standard
  `SafeAreaEdges` per edge.
- **Android back** uses the predictive-back dispatcher (works on Android 16+ where Shell's
  legacy back channel is dead); root pages defer to the system back-to-home preview.
- **`Page.OnBackButtonPressed` is deliberately unsupported** on hosted pages: it only fires
  for hardware back, so confirmation logic written there would be silently bypassed by
  on-screen pops. Move it to `ILeavingGuard` — the single confirmation mechanism covering
  every leave path.
- **State preservation** across tab switches matches NaluShell semantics (stacks kept alive);
  scroll offsets and entry text survive switches on both platforms.
- Shell-specific APIs (`Shell.Current`, custom `ShellItem` templates, query-string routes)
  have no Scaffold counterpart — their use cases map to scaffold structure, intents, and the
  chrome APIs above.
