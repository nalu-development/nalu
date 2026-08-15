using FluentAssertions;
using Microsoft.CodeAnalysis;

namespace Nalu.Maui.Test.SourceGenerators;

public class ScaffoldOverlayGeneratorTests
{
    [Fact(DisplayName = "Model taking IOverlayRef pairs with the view whose ctor takes the model")]
    public void ModelPairsWithViewByConstructor()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            public class DurationSheetModel(IOverlayRef overlay);

            public class DurationSheetView : ContentView
            {
                public DurationSheetView(DurationSheetModel model)
                {
                    BindingContext = model;
                }
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.DurationSheetModel, global::MyApp.DurationSheetView>();");
    }

    [Fact(DisplayName = "View-only overlay taking IOverlayRef registers with the single-generic overload")]
    public void ViewOnlyOverlayIsRegistered()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            public class QuickPopup : ContentView
            {
                public QuickPopup(IOverlayRef overlay)
                {
                }
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.QuickPopup>();");
        result.GeneratedText.Should().NotContain("AddOverlay<global::MyApp.QuickPopup, ");
    }

    [Fact(DisplayName = "XAML source-gen mode: view-only overlay discovered from the .xaml AdditionalFile alone")]
    public void SourceGenModeViewOnlyOverlayDiscoveredFromXamlFile()
    {
        // Simulates MauiXamlInflator=sourcegen: NO generated partial in the compilation —
        // the code-behind is bare, the View base is only visible through the .xaml root.
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Nalu;

            namespace MyApp;

            public partial class QuickPopup
            {
                public QuickPopup(IOverlayRef overlay)
                {
                }
            }
            """,
            xamlFiles:
            [
                new ScaffoldGeneratorTestHarness.XamlFile(
                    "QuickPopup.xaml",
                    """
                    <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 x:Class="MyApp.QuickPopup" />
                    """
                )
            ]
        );

        // The View base lives in MAUI's generated partial, absent from this TEST compilation:
        // the only acceptable output error is the resulting TView constraint violation.
        result.OutputCompilationErrors.Should().OnlyContain(static d => d.Id == "CS0311");
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.QuickPopup>();");
        result.GeneratedText.Should().NotContain("AddOverlay<global::MyApp.QuickPopup, ");
    }

    [Fact(DisplayName = "XAML source-gen mode: model pairs with the view discovered from its .xaml file")]
    public void SourceGenModeModelPairsWithXamlView()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Nalu;

            namespace MyApp;

            public class DurationSheetModel(IOverlayRef overlay);

            public partial class DurationSheetView
            {
                public DurationSheetView(DurationSheetModel model)
                {
                    BindingContext = model;
                }

                public object? BindingContext { get; set; }
            }
            """,
            xamlFiles:
            [
                new ScaffoldGeneratorTestHarness.XamlFile(
                    "DurationSheetView.xaml",
                    """
                    <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 x:Class="MyApp.DurationSheetView" />
                    """
                )
            ]
        );

        // See above: the missing MAUI-generated partial makes the TView constraint the only
        // acceptable output error in the test compilation.
        result.OutputCompilationErrors.Should().OnlyContain(static d => d.Id == "CS0311");
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.DurationSheetModel, global::MyApp.DurationSheetView>();");
    }

    [Fact(DisplayName = "XAML-style partial view whose code-behind declares no base list is discovered")]
    public void XamlPartialViewWithoutBaseListInCodeBehindIsDiscovered()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            // Code-behind part: no base list.
            public partial class QuickPopup
            {
                public QuickPopup(IOverlayRef overlay)
                {
                }
            }

            // XamlG-style generated part: carries the base list.
            public partial class QuickPopup : ContentView;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.QuickPopup>();");
    }

    [Fact(DisplayName = "Naming convention FooModel -> FooView applies when no view ctor takes the model")]
    public void NamingConventionFallback()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            public class PickerModel(IOverlayRef overlay);

            public class PickerView : ContentView;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.PickerModel, global::MyApp.PickerView>();");
    }

    [Fact(DisplayName = "AutoOverlay opts in a model without IOverlayRef and can name the view explicitly")]
    public void AutoOverlayOptInWithExplicitView()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            [AutoOverlay(typeof(SpecialView))]
            public class SilentModel;

            public class SpecialView : ContentView;
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.SilentModel, global::MyApp.SpecialView>();");
    }

    [Fact(DisplayName = "XAML source-gen mode: AutoOverlay can name a view whose base is only in its .xaml")]
    public void SourceGenModeAutoOverlayExplicitXamlView()
    {
        // The named view is a View only through its .xaml root: the generated x:Class partial
        // carrying the base is invisible here, so View-ness is settled at emit time.
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Nalu;

            namespace MyApp;

            [AutoOverlay(typeof(SpecialView))]
            public class SilentModel;

            public partial class SpecialView;
            """,
            xamlFiles:
            [
                new ScaffoldGeneratorTestHarness.XamlFile(
                    "SpecialView.xaml",
                    """
                    <ContentView xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                                 xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                                 x:Class="MyApp.SpecialView" />
                    """
                )
            ]
        );

        // Missing MAUI partial ⇒ the TView constraint is the only acceptable output error.
        result.OutputCompilationErrors.Should().OnlyContain(static d => d.Id == "CS0311");
        result.GeneratorDiagnostics.Should().NotContain(static d => d.Id == "NALU0103");
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.SilentModel, global::MyApp.SpecialView>();");
    }

    [Fact(DisplayName = "AutoOverlay(Enabled = false) excludes a discovered overlay")]
    public void AutoOverlayOptOut()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            [AutoOverlay(Enabled = false)]
            public class ManualModel(IOverlayRef overlay);

            public class ManualView : ContentView
            {
                public ManualView(ManualModel model)
                {
                    BindingContext = model;
                }
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().NotContain("ManualModel");
    }

    [Fact(DisplayName = "Multiple views taking the model prefer the BindingContext assigner")]
    public void BindingContextAssignerWinsTies()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            public class SharedModel(IOverlayRef overlay);

            public class PassiveView : ContentView
            {
                public PassiveView(SharedModel model)
                {
                }
            }

            public class ActiveView : ContentView
            {
                public ActiveView(SharedModel model)
                {
                    BindingContext = model;
                }
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("scaffold.AddOverlay<global::MyApp.SharedModel, global::MyApp.ActiveView>();");
    }

    [Fact(DisplayName = "Ambiguous views without a BindingContext winner produce NALU0102")]
    public void AmbiguousViewsAreReported()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Microsoft.Maui.Controls;
            using Nalu;

            namespace MyApp;

            public class SharedModel(IOverlayRef overlay);

            public class OneView : ContentView { public OneView(SharedModel model) { } }
            public class TwoView : ContentView { public TwoView(SharedModel model) { } }
            """
        );

        result.GeneratorDiagnostics.Should().ContainSingle(static d => d.Id == "NALU0102");
        result.GeneratedText.Should().NotContain("SharedModel");
    }

    [Fact(DisplayName = "Model with no resolvable view produces NALU0101")]
    public void MissingViewIsReported()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Nalu;

            namespace MyApp;

            public class OrphanModel(IOverlayRef overlay);
            """
        );

        result.GeneratorDiagnostics.Should().ContainSingle(static d => d.Id == "NALU0101");
        result.GeneratedText.Should().NotContain("OrphanModel");
    }

    [Fact(DisplayName = "AutoOverlay naming a non-View type produces NALU0103")]
    public void InvalidExplicitViewIsReported()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            using Nalu;

            namespace MyApp;

            public class NotAView;

            [AutoOverlay(typeof(NotAView))]
            public class BrokenModel(IOverlayRef overlay);
            """
        );

        result.GeneratorDiagnostics.Should().ContainSingle(static d => d.Id == "NALU0103");
        result.GeneratedText.Should().NotContain("BrokenModel");
    }

    [Fact(DisplayName = "Without a Nalu scaffold reference nothing is generated")]
    public void NoNaluReferenceNoOutput()
    {
        var result = ScaffoldGeneratorTestHarness.Run("public class Nothing;", includeStubs: false);

        result.GeneratedText.Should().BeEmpty();
    }

    [Fact(DisplayName = "Generated method exists and compiles even with zero overlays")]
    public void EmptyAssemblyStillGeneratesCallableMethod()
    {
        var result = ScaffoldGeneratorTestHarness.Run(
            """
            namespace MyApp;

            public static class Usage
            {
                public static void Use(Nalu.IScaffoldConfigurator scaffold) => scaffold.AddOverlays();
            }
            """
        );

        result.OutputCompilationErrors.Should().BeEmpty();
        result.GeneratedText.Should().Contain("AddOverlays");
    }
}
