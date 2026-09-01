using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Magnet layout: anchors, GONE handling, chains, barriers and transitions.
/// </summary>
[UsedImplicitly]
[TestPage("Magnet Tests")]
public class MagnetTestsPage : ContentPage
{
    public MagnetTestsPage()
    {
        const string p = MagnetAnchor.Parent;

        var magnet = new Magnet
        {
            AutomationId = "MagnetRoot",
            WidthRequest = 320,
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Colors.LightGray,
            Definition = new MagnetDefinition().Add(
                new MagnetBarrier { MagnetId = "textsEnd", Direction = MagnetPole.Bottom, Margin = 8 }.With("avatar", "subtitle"),
                new MagnetChain { MagnetId = "footer", Style = MagnetChainStyle.Spread }.With("f1", "f2", "f3")
            )
        };

        var avatar = new BoxView { Color = Colors.SteelBlue };
        Magnet.GetConstraints(avatar).Id("avatar").Size(48, 48).AlignLeft(p, 16).AlignTop(p, 16);

        var title = new Label { Text = "Title", FontSize = 18, BackgroundColor = Colors.LightGoldenrodYellow };
        Magnet.GetConstraints(title).Id("title").After(avatar, 12, goneMargin: 0).AlignRight(p, 16).AlignTop(avatar).Bias(0, 0.5);

        var subtitle = new Label { Text = "Subtitle", FontSize = 13, BackgroundColor = Colors.LightGreen };
        Magnet.GetConstraints(subtitle).Id("subtitle").AlignLeft(title).Below(title, 2);

        var badge = new BoxView { Color = Colors.IndianRed };
        Magnet.GetConstraints(badge).Id("badge").Size(20, 20).AlignRight(p, 16).AlignTop(p, 16);

        var f1 = new BoxView { Color = Colors.Coral };
        Magnet.GetConstraints(f1).Id("f1").Size(40, 24).AlignLeft(p, 16).Below("textsEnd").AlignBottom(p, 16);
        var f2 = new BoxView { Color = Colors.MediumPurple };
        Magnet.GetConstraints(f2).Id("f2").Size(40, 24).AlignTop(f1);
        var f3 = new BoxView { Color = Colors.SeaGreen };
        Magnet.GetConstraints(f3).Id("f3").Size(40, 24).AlignRight(p, 16).AlignTop(f1);

        magnet.Add(avatar);
        magnet.Add(title);
        magnet.Add(subtitle);
        magnet.Add(badge);
        magnet.Add(f1);
        magnet.Add(f2);
        magnet.Add(f3);

        var toggleAvatarButton = new Button { Text = "Toggle avatar", AutomationId = "ToggleAvatarButton" };
        toggleAvatarButton.Clicked += (_, _) => avatar.IsVisible = !avatar.IsVisible;

        var transitionButton = new Button { Text = "Transition", AutomationId = "TransitionButton" };
        transitionButton.Clicked += async (_, _) =>
        {
            var node = Magnet.GetConstraints(avatar);
            var margin = node.LeftTo!.Value.Margin > 16 ? 16 : 80;
            await magnet.TransitionToAsync(() => node.AlignLeft(p, margin).AlignTop(p, margin), 400);
        };

        var swapButton = new Button { Text = "Swap badge side", AutomationId = "SwapBadgeButton" };
        swapButton.Clicked += async (_, _) =>
        {
            var node = Magnet.GetConstraints(badge);

            await magnet.TransitionToAsync(() =>
                {
                    if (node.RightTo is not null)
                    {
                        node.RightTo = null;
                        node.AlignLeft(p, 16);
                    }
                    else
                    {
                        node.LeftTo = null;
                        node.AlignRight(p, 16);
                    }
                },
                400
            );
        };

        // Scene switching: two definitions declaring geometry AND visibility (ApplyVisibility).
        MagnetDefinition CreateScene(MagnetVisibilityAction iconAction)
            => new MagnetDefinition().Add(
                new MagnetView().Id("sceneIcon").Size(24, 24).AlignLeft(p, 16).AlignTop(p, 12).Visibility(iconAction),
                new MagnetView().Id("sceneText").Left("sceneIcon", MagnetPole.Right, 12, goneMargin: 0).AlignTop(p, 14)
            );

        var sceneA = CreateScene(MagnetVisibilityAction.Show);
        var sceneB = CreateScene(MagnetVisibilityAction.Hide);

        var sceneMagnet = new Magnet
        {
            AutomationId = "SceneRoot",
            WidthRequest = 320,
            HeightRequest = 48,
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Colors.LightGray,
            Definition = sceneA
        };

        var sceneIcon = new BoxView { Color = Colors.DarkOrange };
        Magnet.SetMagnetId(sceneIcon, "sceneIcon");
        var sceneText = new Label { Text = "Scene text", FontSize = 15 };
        Magnet.SetMagnetId(sceneText, "sceneText");
        sceneMagnet.Add(sceneIcon);
        sceneMagnet.Add(sceneText);

        var sceneButton = new Button { Text = "Toggle scene", AutomationId = "SceneButton" };
        var showing = true;
        sceneButton.Clicked += async (_, _) =>
        {
            showing = !showing;
            await sceneMagnet.TransitionToAsync(showing ? sceneA : sceneB, 400);
        };

        Content = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = 16,
            Children = { toggleAvatarButton, transitionButton, swapButton, magnet, sceneButton, sceneMagnet }
        };
    }
}
