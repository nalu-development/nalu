# Nalu VirtualScroll — native Android layer

Java sources compiled into `virtualscroll-release.aar` and bound into
`Nalu.Maui.VirtualScroll` automatically: the csproj declares

```xml
<AndroidGradleProject Include="AndroidNative/build.gradle" ModuleName="virtualscroll" />
```

so the .NET for Android SDK drives this gradle project during every Android build and
generates the C# bindings (namespace `Nalu.Platform` via `Transforms/Metadata.xml`).
Consumers of the NuGet package need nothing — the aar ships inside it; only repo builders
need a JDK (17+) and network access for the first gradle-distribution download.

## Why a Java layer

`VirtualScrollNativeRecyclerView` hosts logic on **rendering/recycling hot paths**, where a
managed override would force a JNI transition on every framework callback:

- **Fading-edge padding offsets** — consulted by `View.draw()` every frame while a fading
  edge renders (safe-area insets are padding with `clipToPadding=false`; the offsets move
  the fade from the padded bounds to the physical view edges).
- **Focus tracking + orphaned-IME handling** — `onChildDetachedFromWindow` fires for every
  recycled child during scrolling; the focused-child comparison and the IME close-on-recycle
  live entirely in Java.

The managed `VirtualScrollRecyclerView` derives from the bound class and keeps cold paths
(window insets, scroll adjustment, MAUI integration) in C#.

## Conventions

- Keep the `androidx.recyclerview:recyclerview` version in `virtualscroll/build.gradle`
  (compileOnly) in sync with the `Xamarin.AndroidX.RecyclerView` binding referenced by the
  .NET project.
- Build manually with `./gradlew :virtualscroll:assembleRelease` (output under
  `virtualscroll/build/outputs/aar/`).
