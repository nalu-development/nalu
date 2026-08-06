// Nalu's navigation entry point, aliased once for the whole app: `Nav.Push<SomePageModel>()`.
// The alias is what makes the API reachable from page code too — inside a Page subclass the
// inherited Page.Navigation property hides the Navigation class. See conceptual_docs/navigation.md.
global using Nav = Nalu.Navigation;
