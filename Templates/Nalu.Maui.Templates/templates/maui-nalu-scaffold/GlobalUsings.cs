// Nalu's public types (INavigationService, IEnteringAware, Scaffold…) live in the `Nalu` namespace.
global using Nalu;

// Nalu's navigation entry point, aliased once for the whole app: `Nav.Push<SomePageModel>()`.
// The alias keeps the API reachable from page code too — inside a Page subclass the inherited
// Page.Navigation property would otherwise hide the Navigation class.
global using Nav = Nalu.Navigation;
