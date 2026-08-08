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
