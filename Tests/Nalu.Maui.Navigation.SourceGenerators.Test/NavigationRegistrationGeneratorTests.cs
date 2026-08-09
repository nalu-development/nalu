using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Nalu.Maui.Test.SourceGenerators;

public class NavigationRegistrationGeneratorTests
{
    [Fact(DisplayName = "Page assigning a ctor parameter to BindingContext registers with that model")]
    public void BindingContextAssignmentInfersModel()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.ComponentModel;

            namespace MyApp;

            public class DetailViewModel : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;
            }

            public class DetailPage : ContentPage
            {
                public DetailPage(DetailViewModel model)
                {
                    BindingContext = model;
                }
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.DetailViewModel, global::MyApp.DetailPage>();");
    }

    [Fact(DisplayName = "XAML-style partial page whose code-behind declares no base list is discovered")]
    public void XamlPartialWithoutBaseListInCodeBehindIsDiscovered()
    {
        // Real-world XAML shape: the developer's code-behind declares NO base type — the
        // XamlG-generated partial carries it (and the base may be an app-level ContentPageBase).
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.ComponentModel;

            namespace MyApp;

            public class ContentPageBase : ContentPage;

            public class DetailViewModel : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;
            }

            // Code-behind part: no base list.
            public partial class DetailPage
            {
                public DetailPage(DetailViewModel model)
                {
                    BindingContext = model;
                }
            }

            // XamlG-style generated part: carries the base list.
            public partial class DetailPage : ContentPageBase;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.DetailViewModel, global::MyApp.DetailPage>();");
    }

    [Fact(DisplayName = "Real-world XAML shape: separate trees, cross-namespace base, XamlG attributes")]
    public void RealWorldXamlShapeIsDiscovered()
    {
        // Mirrors an actual app: the code-behind and the XamlG part are DIFFERENT files,
        // the base class lives in another namespace, and the generated part carries the
        // XamlFilePath attribute and InitializeComponent.
        var result = GeneratorTestHarness.Run(
            [
                """
                using System.ComponentModel;
                using MyApp.Pages;

                namespace MyApp;

                public class InitializationViewModel : INotifyPropertyChanged
                {
                    public event PropertyChangedEventHandler? PropertyChanged;
                }

                public partial class InitializationPage
                {
                    public InitializationPage(InitializationViewModel viewModel)
                    {
                        BindingContext = viewModel;
                        InitializeComponent();
                    }
                }
                """,
                """
                using Microsoft.Maui.Controls;

                namespace MyApp.Pages;

                public abstract class ContentPageBase : ContentPage;
                """,
                """
                // XamlG-style generated file.
                namespace MyApp
                {
                    [global::System.CodeDom.Compiler.GeneratedCode("Microsoft.Maui.Controls.SourceGen", "1.0.0.0")]
                    public partial class InitializationPage : global::MyApp.Pages.ContentPageBase
                    {
                        private void InitializeComponent()
                        {
                        }
                    }
                }
                """
            ]
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.InitializationViewModel, global::MyApp.InitializationPage>();");
    }

    [Fact(DisplayName = "Page deriving from an app-level ContentPage subclass is discovered")]
    public void PageDerivingFromIntermediateBaseIsDiscovered()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;

            namespace MyApp;

            public abstract class ContentPageBase : ContentPage;

            public class SettingsPage : ContentPageBase;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.SettingsPage>();");
    }

    [Fact(DisplayName = "XAML source-gen mode: page discovered from the .xaml AdditionalFile alone")]
    public void SourceGenModePageDiscoveredFromXamlFile()
    {
        // Simulates MauiXamlInflator=sourcegen: NO XamlG partial exists in the compilation
        // (the base-carrying partial is emitted by MAUI's own generator, invisible to us).
        // The only signals are the bare code-behind and the .xaml AdditionalFile.
        var result = GeneratorTestHarness.Run(
            [
                """
                using System.ComponentModel;
                using MyApp.Pages;

                namespace MyApp;

                public class InitializationViewModel : INotifyPropertyChanged
                {
                    public event PropertyChangedEventHandler? PropertyChanged;
                }

                public partial class InitializationPage
                {
                    public InitializationPage(InitializationViewModel viewModel)
                    {
                        BindingContext = viewModel;
                    }

                    public object? BindingContext { get; set; }
                }
                """,
                """
                using Microsoft.Maui.Controls;

                namespace MyApp.Pages;

                public abstract class ContentPageBase : ContentPage;
                """
            ],
            xamlFiles:
            [
                new GeneratorTestHarness.XamlFile(
                    "Pages/InitializationPage.xaml",
                    """
                    <?xml version="1.0" encoding="utf-8" ?>
                    <pages:ContentPageBase xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                           xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                           xmlns:pages="clr-namespace:MyApp.Pages"
                                           x:Class="MyApp.InitializationPage">
                        <Label Text="Hello" />
                    </pages:ContentPageBase>
                    """
                )
            ]
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.InitializationViewModel, global::MyApp.InitializationPage>();");
    }

    [Fact(DisplayName = "XAML source-gen mode: default-xmlns ContentPage root and non-page XAML")]
    public void SourceGenModeDefaultXmlnsAndNonPages()
    {
        var result = GeneratorTestHarness.Run(
            [
                """
                namespace MyApp;

                public partial class HomePage
                {
                    public HomePage()
                    {
                    }
                }

                public partial class BadgeView;
                """
            ],
            xamlFiles:
            [
                new GeneratorTestHarness.XamlFile(
                    "HomePage.xaml",
                    """
                    <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 x:Class="MyApp.HomePage" />
                    """
                ),
                new GeneratorTestHarness.XamlFile(
                    "BadgeView.xaml",
                    """
                    <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 x:Class="MyApp.BadgeView" />
                    """
                )
            ]
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.HomePage>();");
        result.GeneratedText.Should().NotContain("BadgeView");
    }

    [Fact(DisplayName = "XamlG mode: syntax and XAML discovery of the same page dedupe to one registration")]
    public void DualDiscoveryDedupes()
    {
        var result = GeneratorTestHarness.Run(
            [
                """
                using Microsoft.Maui.Controls;

                namespace MyApp;

                // XamlG mode: the generated partial IS in the compilation (syntax path sees it)...
                public partial class HomePage : ContentPage
                {
                    public HomePage()
                    {
                    }
                }
                """
            ],
            // ...and the .xaml AdditionalFile is present too (XAML path sees it as well).
            xamlFiles:
            [
                new GeneratorTestHarness.XamlFile(
                    "HomePage.xaml",
                    """
                    <ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 x:Class="MyApp.HomePage" />
                    """
                )
            ]
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        System.Text.RegularExpressions.Regex.Matches(result.GeneratedText, "AddPage<global::MyApp\\.HomePage>").Count.Should().Be(1);
    }

    [Fact(DisplayName = "Scaffold subclasses are never pages, from either discovery path")]
    public void ScaffoldSubclassesAreExcluded()
    {
        // Nalu.Scaffold derives from ContentPage for hosting reasons, but it is the app
        // shell: an AppScaffold (typically XAML-defined) must never reach AddPages.
        var result = GeneratorTestHarness.Run(
            [
                """
                using Microsoft.Maui.Controls;
                using Nalu;

                namespace MyApp;

                public partial class AppScaffold : Scaffold
                {
                    public AppScaffold()
                    {
                    }
                }

                public class HomePage : ContentPage;
                """
            ],
            xamlFiles:
            [
                new GeneratorTestHarness.XamlFile(
                    "AppScaffold.xaml",
                    """
                    <nalu:Scaffold xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                   xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                   xmlns:nalu="clr-namespace:Nalu"
                                   x:Class="MyApp.AppScaffold" />
                    """
                )
            ]
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.HomePage>();");
        result.GeneratedText.Should().NotContain("AppScaffold");
    }

    [Fact(DisplayName = "Concrete base page other pages derive from is registered too (abstract stays out)")]
    public void ConcreteBasePageIsRegistered()
    {
        // Registering a never-navigated concrete base is harmless; only abstract bases are skipped.
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;

            namespace MyApp;

            public abstract class AbstractPageBase : ContentPage;

            public class ContentPageBase : AbstractPageBase;

            public class HomePage : ContentPageBase;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.HomePage>();");
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.ContentPageBase>();");
        result.GeneratedText.Should().NotContain("AbstractPageBase>");
    }

    [Fact(DisplayName = "Empty assembly emits a per-assembly scoping breadcrumb in AddPages")]
    public void EmptyAssemblyEmitsBreadcrumb()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace MyApp;

            public class NotAPage;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("No pages were discovered in THIS assembly");
    }

    [Fact(DisplayName = "Interface-typed BindingContext parameter resolves its single implementation")]
    public void InterfaceModelResolvesImplementation()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.ComponentModel;

            namespace MyApp;

            public interface IDetailPageModel : INotifyPropertyChanged;

            public class DetailPageModel : IDetailPageModel
            {
                public event PropertyChangedEventHandler? PropertyChanged;
            }

            public class DetailPage : ContentPage
            {
                public DetailPage(IDetailPageModel model)
                {
                    this.BindingContext = model;
                }
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.IDetailPageModel, global::MyApp.DetailPageModel, global::MyApp.DetailPage>();");
    }

    [Fact(DisplayName = "Single INotifyPropertyChanged ctor parameter is used when BindingContext is not assigned in source")]
    public void SingleInpcParameterFallback()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.ComponentModel;

            namespace MyApp;

            public class SomeViewModel : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;
            }

            public class SomeService;

            public class SomePage : ContentPage
            {
                public SomePage(SomeService service, SomeViewModel model)
                {
                }
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.SomeViewModel, global::MyApp.SomePage>();");
    }

    [Fact(DisplayName = "Naming convention MyPage -> MyPageModel applies when the ctor gives no signal")]
    public void NamingConventionFallback()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.ComponentModel;

            namespace MyApp;

            public class HomePageModel : INotifyPropertyChanged
            {
                public event PropertyChangedEventHandler? PropertyChanged;
            }

            public class HomePage : ContentPage;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.HomePageModel, global::MyApp.HomePage>();");
    }

    [Fact(DisplayName = "Naming convention prefers the IMyPageModel interface when implemented")]
    public void NamingConventionPrefersInterface()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.ComponentModel;

            namespace MyApp;

            public interface IHomePageModel : INotifyPropertyChanged;

            public class HomePageModel : IHomePageModel
            {
                public event PropertyChangedEventHandler? PropertyChanged;
            }

            public class HomePage : ContentPage;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.IHomePageModel, global::MyApp.HomePageModel, global::MyApp.HomePage>();");
    }

    [Fact(DisplayName = "Page without any model registers view-only with an info diagnostic")]
    public void ViewOnlyFallback()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;

            namespace MyApp;

            public class AboutPage : ContentPage;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("navigation.AddPage<global::MyApp.AboutPage>();");
        result.GeneratorDiagnostics.Should().ContainSingle(static d => d.Id == "NALU0001");
    }

    [Fact(DisplayName = "AutoNavigationPage(Enabled = false), abstract and generic pages are skipped")]
    public void ExcludedPagesAreSkipped()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            [AutoNavigationPage(Enabled = false)]
            public class HiddenPage : ContentPage;

            public abstract class BasePage : ContentPage;

            public class GenericPage<T> : ContentPage;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().NotContain("HiddenPage").And.NotContain("BasePage").And.NotContain("GenericPage");
    }

    [Fact(DisplayName = "Intents from IEnteringAware/IAppearingAware on page and model are registered")]
    public void IntentsAreDiscovered()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.ComponentModel;
            using System.Threading.Tasks;
            using Nalu;

            namespace MyApp;

            public record EditIntent(int Id);
            public record ShowIntent(string Name);

            public class EditorPageModel : INotifyPropertyChanged, IEnteringAware<EditIntent>
            {
                public event PropertyChangedEventHandler? PropertyChanged;
                public ValueTask OnEnteringAsync(EditIntent intent) => default;
            }

            public class EditorPage : ContentPage, IAppearingAware<ShowIntent>
            {
                public EditorPage(EditorPageModel model)
                {
                    BindingContext = model;
                }

                public ValueTask OnAppearingAsync(ShowIntent intent) => default;
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("options.AddIntent<global::MyApp.EditIntent>(\"EditIntent\");");
        result.GeneratedText.Should().Contain("options.AddIntent<global::MyApp.ShowIntent>(\"ShowIntent\");");
    }

    [Fact(DisplayName = "AutoNavigationIntent controls restorability and the type id")]
    public void IntentOptionsAttributeIsHonored()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.Threading.Tasks;
            using Nalu;

            namespace MyApp;

            [AutoNavigationIntent(Enabled = false)]
            public record EphemeralIntent;

            [AutoNavigationIntent("stable-id")]
            public record RenamedIntent;

            public class SomePage : ContentPage, IEnteringAware<EphemeralIntent>, IEnteringAware<RenamedIntent>
            {
                public ValueTask OnEnteringAsync(EphemeralIntent intent) => default;
                public ValueTask OnEnteringAsync(RenamedIntent intent) => default;
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().NotContain("EphemeralIntent");
        result.GeneratedText.Should().Contain("options.AddIntent<global::MyApp.RenamedIntent>(\"stable-id\");");
    }

    [Fact(DisplayName = "Awaitable intents are never registered for restore")]
    public void AwaitableIntentsAreSkipped()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.Threading.Tasks;
            using Nalu;

            namespace MyApp;

            public class PickIntent : AwaitableIntent<int>;

            public class PickerPage : ContentPage, IEnteringAware<PickIntent>
            {
                public ValueTask OnEnteringAsync(PickIntent intent) => default;
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().NotContain("AddIntent<global::MyApp.PickIntent>");
    }

    [Fact(DisplayName = "Two restorable intents with the same short name produce an error diagnostic")]
    public void IntentIdCollisionIsReported()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.Threading.Tasks;
            using Nalu;

            namespace MyApp.A { public record EditIntent; }
            namespace MyApp.B { public record EditIntent; }

            namespace MyApp;

            public class SomePage : ContentPage, IEnteringAware<A.EditIntent>, IEnteringAware<B.EditIntent>
            {
                public ValueTask OnEnteringAsync(A.EditIntent intent) => default;
                public ValueTask OnEnteringAsync(B.EditIntent intent) => default;
            }
            """
        );

        result.GeneratorDiagnostics.Should().ContainSingle(static d => d.Id == "NALU0005" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact(DisplayName = "BindingContext assigned from multiple parameters skips the page with a warning")]
    public void AmbiguousBindingContextIsReported()
    {
        var result = GeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using System.ComponentModel;

            namespace MyApp;

            public class ModelA : INotifyPropertyChanged { public event PropertyChangedEventHandler? PropertyChanged; }
            public class ModelB : INotifyPropertyChanged { public event PropertyChangedEventHandler? PropertyChanged; }

            public class WeirdPage : ContentPage
            {
                public WeirdPage(ModelA a, ModelB b, bool flag)
                {
                    if (flag) { BindingContext = a; } else { BindingContext = b; }
                }
            }
            """
        );

        result.GeneratorDiagnostics.Should().ContainSingle(static d => d.Id == "NALU0002" && d.Severity == DiagnosticSeverity.Warning);
        result.GeneratedText.Should().NotContain("WeirdPage");
    }

    [Fact(DisplayName = "Without a Nalu reference nothing is generated")]
    public void NoNaluReferenceNoOutput()
    {
        var result = GeneratorTestHarness.Run("public class Nothing;", includeStubs: false);

        result.GeneratedText.Should().BeEmpty();
    }

    [Fact(DisplayName = "Generated methods exist and compile even with zero pages")]
    public void EmptyAssemblyStillGeneratesCallableMethods()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace MyApp;

            public static class Usage
            {
                public static void Use(Nalu.NavigationConfigurator nav, Nalu.NavigationRestoreOptions options)
                {
                    nav.AddPages();
                    options.AddIntents();
                }
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("AddPages").And.Contain("AddIntents");
    }
}
