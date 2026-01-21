![Banner](https://raw.githubusercontent.com/nalu-development/nalu/main/Images/Banner.png)

## Nalu [![GitHub Actions Status](https://github.com/nalu-development/nalu/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/nalu-development/nalu/actions/workflows/build.yml)

`Nalu.Maui` provides a set of libraries designed to simplify and accelerate your .NET MAUI application development by addressing common challenges.

**For comprehensive documentation, guides, API references, and samples, please visit our dedicated documentation website:**

➡️ **[Nalu.Maui Documentation Website](https://nalu-development.github.io/nalu/)** ⬅️

If `Nalu.Maui` is valuable to your work, consider supporting its continued development and maintenance ❤️

[![Sponsor](https://img.shields.io/badge/Sponsor-%E2%9D%A4-pink?logo=github&style=for-the-badge)](https://github.com/sponsors/albyrock87)

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
    * A high-performance alternative to the .NET MAUI `CollectionView`, leveraging native `RecyclerView` (Android) and `UICollectionView` (iOS). Supports dynamic sizing, `ObservableCollection<T>`, pull-to-refresh, section templates and carousel mode.
    * ⚖️ **Dual Licensed**: 
        * **Non-Commercial:** Free under the MIT License (personal, educational, or non-commercial open-source use).
        * **Commercial:** Requires an active [GitHub Sponsors subscription](https://github.com/sponsors/albyrock87).
    * By installing this package, you agree to the terms in the `LICENSE.md`. Commercial use includes for-profit entities, internal tools, and contract work.

We encourage you to explore the [full documentation](https://nalu-development.github.io/nalu/) for detailed information on how to integrate and utilize these features in your projects.