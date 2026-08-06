// Inside a Page subclass the inherited Page.Navigation property hides Nalu's Navigation class,
// so page code cannot reach the fluent API by that name. Aliasing it once here lets every page
// write `Nav.Push<SomePage>()` / `Nav.Pop()` — see conceptual_docs/navigation.md.
global using Nav = Nalu.Navigation;
