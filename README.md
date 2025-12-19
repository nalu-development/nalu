![Banner](https://raw.githubusercontent.com/nalu-development/nalu/main/Images/Banner.png)

## Nalu [![GitHub Actions Status](https://github.com/nalu-development/nalu/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/nalu-development/nalu/actions/workflows/build.yml)

`Nalu.Maui` provides a set of libraries designed to simplify and accelerate your .NET MAUI application development by addressing common challenges.

**For comprehensive documentation, guides, API references, and samples, please visit our dedicated documentation website:**

➡️ **[Nalu.Maui Documentation Website](https://nalu-development.github.io/nalu/)** ⬅️

If `Nalu.Maui` is valuable to your work, consider supporting its continued development and maintenance through a donation:

<a target="_blank" href="https://buymeacoffee.com/albyrock87">
    <img src="conceptual_docs/assets/images/donate.png" height="44">
</a>

### Key Modules:

*   **Core** [![Nalu.Maui.Core NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Core.svg)](https://www.nuget.org/packages/Nalu.Maui.Core/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Core)](https://www.nuget.org/packages/Nalu.Maui.Core/)
    *   Provides common utilities, including an `NSUrlBackgroundSessionHttpMessageHandler` for robust background HTTP requests on iOS.
*   **Navigation** [![Nalu.Maui.Navigation NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Navigation.svg)](https://www.nuget.org/packages/Nalu.Maui.Navigation/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Navigation)](https://www.nuget.org/packages/Nalu.Maui.Navigation/)
    *   Offers a fluent, type-safe MVVM navigation service built on `Shell`, supporting relative/absolute navigation, guards, and parameter passing. Includes a leak detector. Also provides a customizable tab bar feature (iOS/Android/MacCatalyst) that works with both standard Shell and NaluShell.
*   **Layouts** [![Nalu.Maui.Layouts NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Layouts.svg)](https://www.nuget.org/packages/Nalu.Maui.Layouts/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Layouts)](https://www.nuget.org/packages/Nalu.Maui.Layouts/)
    *   Simplifies XAML development with template controls (`ToggleTemplate`, `TemplateBox`), lightweight `ContentView` replacement with clipping support (`ViewBox`), animated expanders (`ExpanderViewBox`), and a **constraint-based layout system** (`Magnet`).
*   **Controls** [![Nalu.Maui.Controls NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Controls.svg)](https://www.nuget.org/packages/Nalu.Maui.Controls/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Controls)](https://www.nuget.org/packages/Nalu.Maui.Controls/)
    *   Includes useful cross-platform controls like `InteractableCanvasView` (a `SKCanvasView` with enhanced touch support) and `DurationWheel` (a `TimeSpan?` editor).
*   **VirtualScroll** [![Nalu.Maui.VirtualScroll NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.VirtualScroll.svg)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.VirtualScroll)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/)
    *   A high-performance virtualized scrolling view designed as an alternative to the .NET MAUI `CollectionView`. Optimized for Android (`RecyclerView`) and iOS (`UICollectionView`), with full `ObservableCollection<T>` support, dynamic item sizing, pull-to-refresh, and section templates. **Non-Commercial License**.

We encourage you to explore the [full documentation](https://nalu-development.github.io/nalu/) for detailed information on how to integrate and utilize these features in your projects.