![Banner](https://raw.githubusercontent.com/nalu-development/nalu/main/Images/Banner.png)

## Nalu [![GitHub Actions Status](https://github.com/nalu-development/nalu/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/nalu-development/nalu/actions/workflows/build.yml)

`Nalu.Maui` is a set of libraries built to make .NET MAUI development faster, smoother and more enjoyable — polished navigation, a fully drawn application shell, high-performance lists and layout primitives that remove entire categories of boilerplate.

**For comprehensive documentation, guides, API references, and samples, please visit our dedicated documentation website:**

➡️ **[Nalu.Maui Documentation Website](https://nalu-development.github.io/nalu/)** ⬅️

If `Nalu.Maui` is valuable to your work, consider sponsoring the author on GitHub ❤️

[![Sponsor](https://img.shields.io/badge/Sponsor-%E2%9D%A4-pink?logo=github&style=for-the-badge)](https://github.com/sponsors/albyrock87)

### The Scaffold — your whole app shell, drawn by Nalu [![Nalu.Maui.Scaffold NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Scaffold.svg)](https://www.nuget.org/packages/Nalu.Maui.Scaffold/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Scaffold)](https://www.nuget.org/packages/Nalu.Maui.Scaffold/)

![Scaffold showcase: shared-element transitions flying between pages, a per-page drawer, a bottom sheet hosting the duration wheel, scroll-driven chrome and the floating tab bar](https://raw.githubusercontent.com/nalu-development/nalu/main/Images/readme-scaffold-showcase.gif)

*Shared elements flying between pages · per-page drawers · bottom sheets · scroll-materializing nav bar · floating tab bar with overflow — all from one sample app.*

A complete replacement for MAUI `Shell` on **iOS and Android** where **every piece of chrome is a MAUI view you can restyle or replace**: tab bar (with overflow), nav bar, edge drawers, popups & bottom sheets, modal presentation, page transitions with **shared elements**, iOS edge-swipe and **Android predictive back** (both scrubbing the same seekable choreography), scroll-driven chrome and system-bar styling that follows your UI.

Because the Scaffold *hosts* Nalu navigation, every interaction — tab tap, back gesture, flyout selection — routes through the engine: guards, lifecycle and intents always run, and [state restoration](conceptual_docs/navigation-restore.md) lands your users exactly where they left off after a restart.

- 📦 **Available on [NuGet.org](https://www.nuget.org/packages/Nalu.Maui.Scaffold/)**: `dotnet add package Nalu.Maui.Scaffold`. `Nalu.Maui.Navigation` keeps working with MAUI `Shell` exactly as before: the Scaffold is an **additional host**, not a breaking change.
- 🚀 **New app?** Start from the template: `dotnet new install Nalu.Maui.Templates`, then `dotnet new maui-nalu-scaffold -n MyApp` — tab bar, model-first navigation, a shared-element transition and predictive back already wired.
- 📖 Docs: [Overview](conceptual_docs/scaffold.md) · [Structure & Tab Bar](conceptual_docs/scaffold-structure.md) · [Nav Bar](conceptual_docs/scaffold-navbar.md) · [Flyouts](conceptual_docs/scaffold-flyout.md) · [Popups & Sheets](conceptual_docs/scaffold-overlays.md) · [Transitions](conceptual_docs/scaffold-transitions.md) · [Migrating from NaluShell](conceptual_docs/scaffold-migration.md)

### See it in motion

![VirtualScroll flinging through hundreds of rows, the Layouts insights card sliding and toggling templates, and day rows expanding inline](https://raw.githubusercontent.com/nalu-development/nalu/main/Images/readme-experience-showcase.gif)

*Left to right: **VirtualScroll** flinging through a week of hourly rows and jumping back with an animated `ScrollTo` · **SlideBox** sliding between insight panels while a **ToggleTemplate** flips to "All caught up" as the last task completes · **ExpanderViewBox** rows expanding inline with smoothly animated height.*

### Key Modules

*   **Navigation** [![Nalu.Maui.Navigation NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Navigation.svg)](https://www.nuget.org/packages/Nalu.Maui.Navigation/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Navigation)](https://www.nuget.org/packages/Nalu.Maui.Navigation/)
    *   A fluent, type-safe MVVM navigation service supporting relative/absolute navigation, guards, typed intents and a built-in **leak detector**. Page registration is **source-generated** (`AddPages()` — trim/AOT-safe, no reflection), and opt-in [navigation state restoration](conceptual_docs/navigation-restore.md) reopens the app exactly where it was. Works with MAUI `Shell`, `NaluShell`, and the Scaffold.
*   **VirtualScroll** [![Nalu.Maui.VirtualScroll NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.VirtualScroll.svg)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.VirtualScroll)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/)
    *   A **fast** alternative to the .NET MAUI `CollectionView`, built directly on native `RecyclerView` (Android) and `UICollectionView` (iOS) with a Java-side hot path that keeps per-frame work off the managed heap — fling through thousands of dynamically-sized rows without a stutter. Sections with headers, horizontal layout, carousel mode, pull-to-refresh, animated `ScrollTo`, and long-press **drag reorder** are all built in.
    *   ⚖️ **Dual Licensed**:
        *   **Non-Commercial:** Free under the Apache 2.0-Based Non-Commercial License (personal, educational, or non-commercial open-source use).
        *   **Commercial:** Requires an active [GitHub Sponsors subscription](https://github.com/sponsors/albyrock87) for usage rights only; no services are included.
    *   By installing this package, you agree to the terms in the `LICENSE.md`. Commercial use includes for-profit entities, internal tools, and contract work.
*   **Layouts** [![Nalu.Maui.Layouts NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Layouts.svg)](https://www.nuget.org/packages/Nalu.Maui.Layouts/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Layouts)](https://www.nuget.org/packages/Nalu.Maui.Layouts/)
    *   The XAML you wish was built in: `ToggleTemplate` swaps content on a boolean, `TemplateBox` renders any `DataTemplate` in place, `SlideBox` pages between lazily-created, state-retaining slides, `ExpanderViewBox` animates expand/collapse with real measured sizes, `ViewBox` is a lightweight clipping `ContentView` replacement, and `Magnet` brings a full **constraint-based layout system**.

        ![ExpanderViewBox: the XAML on the left, the animated growing container on the right](https://raw.githubusercontent.com/nalu-development/nalu/main/conceptual_docs/assets/images/expander.gif)
*   **Controls** [![Nalu.Maui.Controls NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Controls.svg)](https://www.nuget.org/packages/Nalu.Maui.Controls/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Controls)](https://www.nuget.org/packages/Nalu.Maui.Controls/)
    *   Cross-platform controls with personality: `DurationWheel` (the rotary `TimeSpan?` editor shown inside the bottom sheet above) and `InteractableCanvasView` (a `SKCanvasView` with enhanced touch support).
*   **Core** [![Nalu.Maui.Core NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Core.svg)](https://www.nuget.org/packages/Nalu.Maui.Core/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Core)](https://www.nuget.org/packages/Nalu.Maui.Core/)
    *   Common utilities, including `NSUrlBackgroundSessionHttpMessageHandler` for robust background HTTP requests on iOS and a soft-keyboard manager.

Every animation on this page comes from **Daily Helper** (`Samples/Nalu.Maui.DailyHelper`), the complete sample app in this repository — clone it and run it to feel everything first-hand.

We encourage you to explore the [full documentation](https://nalu-development.github.io/nalu/) for detailed information on how to integrate and utilize these features in your projects.
