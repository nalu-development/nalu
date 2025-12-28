## Magnet

`Magnet` is a powerful **constraint-based** layout that allows you to create complex layouts with ease by defining constraints.

![Magnet](assets/images/magnet.png)

The documentation is not yet available here, but you can [look at this presentation](https://docs.google.com/presentation/d/1VkKodflxRsIWdPN8ZgwiQKUBybEszTV3gXBW4cIiEqs/edit?usp=sharing).

> **Note:** This layout is in **alpha/preview** state so its API may be subject to change.

### Performance

Here's a performance comparison between `GridLayout` and `MagnetLayout` considering two scenarios:

1. **Dynamic measure**: All the views keep chaining their size on each measure pass (rare case)
2. **Constant measure**: The views always return the same size (very common)

```csharp
for (var i = 0; i < _iterations; i++)
{
    var result = _layoutManager.Measure(500, 500);
    _layoutManager.ArrangeChildren(new Rect(Point.Zero, result));
}
```

| Method                          | Mean     | Error     | StdDev    | Gen0     | Allocated |
|-------------------------------- |---------:|----------:|----------:|---------:|----------:|
| GridLayoutPerf                  | 2.332 ms | 0.0206 ms | 0.0183 ms | 222.6563 |   1.78 MB |
| MagnetLayoutPerf                | 6.747 ms | 0.0444 ms | 0.0393 ms | 273.4375 |   2.21 MB |
| GridLayoutConstantMeasurePerf   | 1.388 ms | 0.0221 ms | 0.0207 ms | 166.0156 |   1.33 MB |
| MagnetLayoutConstantMeasurePerf | 2.756 ms | 0.0211 ms | 0.0176 ms | 218.7500 |   1.75 MB |

As we can see `Magnet` is about 2 times slower than `Grid` but it provides a lot of flexibility and power.
So it's up to you to decide whether to use `Magnet` or the standard MAUI layouts.

On a common page with a few views, the performance impact is negligible while the flexibility gain is huge in comparison.

Inside a `CollectionView` template is probably better to use MAUI layouts, but that still needs to be verified with real data
considering that you may be forced to use nested layouts to achieve the same result and that also comes with a non-negligible performance cost.

### Important Notes

- In a `CollectionView` template, make sure your `Stage` property references a `MagnetStage` defined outside the template (aka `Resources`)
- At the moment it is not supported to change the `MagnetStage` content at runtime

### When to Use Magnet

**Good use cases:**
- Complex layouts that would require deeply nested Grid/StackLayout combinations
- Layouts where elements need to be positioned relative to multiple other elements
- Responsive layouts that adapt based on content size

**Consider alternatives when:**
- Simple layouts that Grid or StackLayout can handle easily
- Inside frequently recycled templates (like CollectionView items)
- Performance-critical scenarios with many layout passes

