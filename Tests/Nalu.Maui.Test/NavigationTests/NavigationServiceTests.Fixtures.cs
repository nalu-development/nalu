using System.ComponentModel;
using System.Text;

namespace Nalu.Maui.Test.NavigationTests;

#pragma warning disable IDE0028

public partial class NavigationServiceTests
{
    private static readonly AsyncLocal<IServiceProvider> _serviceLocator = new();

    private class TestShellItemProxy(string segmentName) : IShellItemProxy
    {
        private IShellSectionProxy? _currentSection;

        public string SegmentName { get; } = segmentName;

        public IShellSectionProxy CurrentSection
        {
            get => _currentSection ?? Sections[0];
            set => _currentSection = value;
        }

        public IReadOnlyList<IShellSectionProxy> Sections { get; } = new List<IShellSectionProxy>();
        public IShellProxy Parent { get; set; } = null!;
    }

    private class TestShellSectionProxy(string segmentName) : IShellSectionProxy
    {
        private readonly List<List<Page>> _navigationStacks = [];
        private IShellContentProxy? _currentContent;

        public string SegmentName { get; } = segmentName;

        public IShellContentProxy CurrentContent
        {
            get => _currentContent ?? Contents[0];
            set => _currentContent = value;
        }

        public IReadOnlyList<IShellContentProxy> Contents { get; } = new List<IShellContentProxy>();
        public IShellItemProxy Parent { get; set; } = null!;

        private int CurrentContentIndex
        {
            get
            {
                for (var i = 0; i < Contents.Count; i++)
                {
                    if (Contents[i] == CurrentContent)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        public IEnumerable<NavigationStackPage> GetNavigationStack(IShellContentProxy? content = null)
        {
            EnsureStacks();

            content ??= CurrentContent;

            if (content.Page is null)
            {
                yield break;
            }

            var baseRoute = $"//{Parent.SegmentName}/{SegmentName}/{content.SegmentName}";

            yield return new NavigationStackPage(baseRoute, content.SegmentName, content.Page, false);

            if (content.Parent.CurrentContent != content)
            {
                yield break;
            }

            var currentContentIndex = CurrentContentIndex;
            var navigationStack = _navigationStacks[currentContentIndex];
            var route = new StringBuilder(baseRoute);

            foreach (var stackPage in navigationStack)
            {
                var segmentName = NavigationSegmentAttribute.GetSegmentName(stackPage.GetType());
                route.Append('/');
                route.Append(segmentName);

                yield return new NavigationStackPage(route.ToString(), segmentName, stackPage, Shell.GetPresentationMode(stackPage).HasFlag(PresentationMode.Modal));
            }
        }

        public Task PopAsync()
        {
            EnsureStacks();
            var navigationStack = _navigationStacks[CurrentContentIndex];
            navigationStack.RemoveAt(navigationStack.Count - 1);

            return Task.CompletedTask;
        }

        public void RemoveStackPages(int count = -1)
        {
            EnsureStacks();
            var navigationStack = _navigationStacks[CurrentContentIndex];

            if (count < 0)
            {
                count = navigationStack.Count;
            }

            while (count > 1)
            {
                _navigationStacks.RemoveAt(_navigationStacks.Count - 1);
            }
        }

        public void Push(Page page)
        {
            EnsureStacks();
            var navigationStack = _navigationStacks[CurrentContentIndex];
            navigationStack.Add(page);
        }

        private void EnsureStacks()
        {
            while (_navigationStacks.Count < Contents.Count)
            {
                _navigationStacks.Add([]);
            }
        }
    }

    private class TestShellContentProxy(Type pageType, string segmentName) : IShellContentProxy
    {
        public string SegmentName { get; } = segmentName;
        public bool HasGuard => Page?.BindingContext is ILeavingGuard;
        public IShellSectionProxy Parent { get; set; } = null!;
        public Page? Page { get; private set; }
        public Page GetOrCreateContent() => Page ??= ((NavigationService) _serviceLocator.Value!.GetRequiredService<INavigationService>()).CreatePage(pageType, null);

        public void DestroyContent()
        {
            if (Page is not null)
            {
                PageNavigationContext.Dispose(Page);
                Page = null;
            }
        }
    }

    public record EvenIntent(int Value = 0);

    public record OddIntent(string Value = "Hello");

    private class BaseTestPage : ContentPage
    {
        protected BaseTestPage(object model)
        {
            BindingContext = model;
        }
    }

    public interface ITestPageModel<in T> :
        INotifyPropertyChanged,
        IEnteringAware,
        IEnteringAware<T>,
        IAppearingAware,
        IAppearingAware<T>,
        IDisappearingAware,
        ILeavingAware,
        IDisposable;

    public interface IPage1Model : ITestPageModel<OddIntent>;

    private class Page1(IPage1Model model) : BaseTestPage(model);

    public interface IPage2Model : ITestPageModel<EvenIntent>;

    private class Page2(IPage2Model model) : BaseTestPage(model);

    public interface IPage3Model : ITestPageModel<OddIntent>;

    private class Page3(IPage3Model model) : BaseTestPage(model);

    public interface IPage4Model : ITestPageModel<EvenIntent>;

    private class Page4(IPage4Model model) : BaseTestPage(model);

    public interface IPage5Model : ITestPageModel<OddIntent>;

    private class Page5(IPage5Model model) : BaseTestPage(model);

    public interface IPage6Model : ITestPageModel<EvenIntent>;

    private class Page6(IPage6Model model) : BaseTestPage(model);

    public interface IPage7Model : ITestPageModel<OddIntent>;

    private class Page7 : BaseTestPage
    {
        public Page7(IPage7Model model)
            : base(model)
        {
            Shell.SetPresentationMode(this, PresentationMode.Modal);
        }
    }

    public interface IPage8Model : ITestPageModel<EvenIntent>, ILeavingGuard;

    private class Page8(IPage8Model model) : BaseTestPage(model);

    public interface IPage9Model : ITestPageModel<OddIntent>, ILeavingGuard;

    private class Page9(IPage9Model model) : BaseTestPage(model);

    public class Page10Model : IAppearingAware<OddIntent>, IEnteringAware<OddIntent>
    {
        public bool AppearingInvoked { get; private set; }

        ValueTask IAppearingAware<OddIntent>.OnAppearingAsync(OddIntent intent)
        {
            AppearingInvoked = true;
            return ValueTask.CompletedTask;
        }

        public bool EnteringInvoked { get; private set; }

        ValueTask IEnteringAware<OddIntent>.OnEnteringAsync(OddIntent intent)
        {
            EnteringInvoked = true;
            return ValueTask.CompletedTask;
        }
    }
    
    private class Page10(Page10Model model) : BaseTestPage(model);

    /// <summary>
    /// Configures the test with the specified shell contents.
    /// </summary>
    /// <example>
    /// This is an example configuration:
    /// - c1,c2,c3
    /// - i1[s1[c1,c2],c3],c4
    /// - c1,s1[c1,c2],c3
    /// Basically, 'i' stands for item, 's' stands for section, and 'c' stands for content.
    /// It automatically generates implicit shell items/sections.
    /// Supports numbers up to 9.
    /// Page 8 and 9 have leaving guard.
    /// Page 7 is modal.
    /// </example>
    private void ConfigureTestAsync(string shellContents)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped<INavigationServiceProviderInternal, NavigationServiceProvider>();
        serviceCollection.AddScoped<INavigationServiceProvider>(sp => sp.GetRequiredService<INavigationServiceProviderInternal>());
        _navigationConfiguration = new NavigationConfigurator(serviceCollection, typeof(NavigationServiceTests));
        var mapping = (IDictionary<Type, Type>) _navigationConfiguration.Mapping;

        mapping.Add(typeof(IPage1Model), typeof(Page1));
        mapping.Add(typeof(IPage2Model), typeof(Page2));
        mapping.Add(typeof(IPage3Model), typeof(Page3));
        mapping.Add(typeof(IPage4Model), typeof(Page4));
        mapping.Add(typeof(IPage5Model), typeof(Page5));
        mapping.Add(typeof(IPage6Model), typeof(Page6));
        mapping.Add(typeof(IPage7Model), typeof(Page7));
        mapping.Add(typeof(IPage8Model), typeof(Page8));
        mapping.Add(typeof(IPage9Model), typeof(Page9));
        mapping.Add(typeof(Page10Model), typeof(Page10));

        serviceCollection.AddScoped<IPage1Model>(_ => Substitute.For<IPage1Model>());
        serviceCollection.AddScoped<Page1>();
        serviceCollection.AddScoped<IPage2Model>(_ => Substitute.For<IPage2Model>());
        serviceCollection.AddScoped<Page2>();
        serviceCollection.AddScoped<IPage3Model>(_ => Substitute.For<IPage3Model>());
        serviceCollection.AddScoped<Page3>();
        serviceCollection.AddScoped<IPage4Model>(_ => Substitute.For<IPage4Model>());
        serviceCollection.AddScoped<Page4>();
        serviceCollection.AddScoped<IPage5Model>(_ => Substitute.For<IPage5Model>());
        serviceCollection.AddScoped<Page5>();
        serviceCollection.AddScoped<IPage6Model>(_ => Substitute.For<IPage6Model>());
        serviceCollection.AddScoped<Page6>();
        serviceCollection.AddScoped<IPage7Model>(_ => Substitute.For<IPage7Model>());
        serviceCollection.AddScoped<Page7>();
        serviceCollection.AddScoped<IPage8Model>(_ => Substitute.For<IPage8Model>());
        serviceCollection.AddScoped<Page8>();
        serviceCollection.AddScoped<IPage9Model>(_ => Substitute.For<IPage9Model>());
        serviceCollection.AddScoped<Page9>();
        serviceCollection.AddScoped<Page10Model>();
        serviceCollection.AddScoped<Page10>();

        serviceCollection.AddSingleton<INavigationService, NavigationService>();
        _serviceLocator.Value = _serviceProvider = serviceCollection.BuildServiceProvider();
        _navigationService = (NavigationService) _serviceProvider.GetRequiredService<INavigationService>();
        _shellProxy = Substitute.For<IShellProxy>();

        var items = GenerateShellItemProxies(_shellProxy, shellContents);

        var segmentToContent = items
                               .SelectMany(i => i.Sections)
                               .SelectMany(s => s.Contents)
                               .ToDictionary(c => c.SegmentName);

        _shellProxy.Items.Returns(items);
        _shellProxy.CurrentItem.Returns(items[0]);
        _shellProxy.ProposeNavigation(Arg.Any<INavigationInfo>()).Returns(true);

        _shellProxy.CommitNavigationAsync(Arg.Any<Action>())
                   .Returns(callInfo =>
                       {
                           callInfo.Arg<Action>()?.Invoke();

                           return Task.CompletedTask;
                       }
                   );

        _shellProxy
            .GetContent(Arg.Any<string>())
            .Returns(callInfo =>
                {
                    var segmentName = callInfo.Arg<string>();

                    return segmentToContent[segmentName];
                }
            );

        _shellProxy
            .When(m => m.InitializeWithContent(Arg.Any<string>()))
            .Do(callInfo =>
                {
                    var segmentName = callInfo.Arg<string>();
                    var content = segmentToContent[segmentName];
                    var section = content.Parent;
                    var item = section.Parent;
                    ((TestShellSectionProxy) section).CurrentContent = content;
                    ((TestShellItemProxy) item).CurrentSection = section;
                    _shellProxy.CurrentItem.Returns(item);
                }
            );

        _shellProxy
            .SelectContentAsync(Arg.Any<string>())
            .Returns(callInfo =>
                {
                    var segmentName = callInfo.Arg<string>();
                    var content = segmentToContent[segmentName];
                    var section = content.Parent;
                    var item = section.Parent;
                    ((TestShellSectionProxy) section).CurrentContent = content;
                    ((TestShellItemProxy) item).CurrentSection = section;
                    _shellProxy.CurrentItem.Returns(item);

                    return Task.CompletedTask;
                }
            );

        _shellProxy
            .PushAsync(Arg.Any<string>(), Arg.Any<Page>())
            .Returns(callInfo =>
                {
                    var page = callInfo.Arg<Page>();
                    var section = (TestShellSectionProxy) _shellProxy.CurrentItem.CurrentSection;
                    section.Push(page);

                    return Task.CompletedTask;
                }
            );

        _shellProxy
            .PopAsync(Arg.Any<IShellSectionProxy>())
            .Returns(callInfo =>
                {
                    var section = (TestShellSectionProxy) (callInfo.Arg<IShellSectionProxy>() ?? _shellProxy.CurrentItem.CurrentSection);

                    return section.PopAsync();
                }
            );
    }

    /// <inheritdoc cref="ConfigureTestAsync" />
    private static List<IShellItemProxy> GenerateShellItemProxies(IShellProxy shell, ReadOnlySpan<char> shellContents)
    {
        var shellItemProxies = new List<IShellItemProxy>();
        IShellItemProxy? currentItem = null;
        IShellSectionProxy? currentSection = null;

        var pageTypes = new Dictionary<char, Type>
                        {
                            { '1', typeof(Page1) },
                            { '2', typeof(Page2) },
                            { '3', typeof(Page3) },
                            { '4', typeof(Page4) },
                            { '5', typeof(Page5) },
                            { '6', typeof(Page6) },
                            { '7', typeof(Page7) },
                            { '8', typeof(Page8) },
                            { '9', typeof(Page9) }
                        };

        for (var i = 0; i < shellContents.Length; i++)
        {
            var c = shellContents[i];

            if (c == 'i')
            {
                var segmentName = shellContents[i..(i + 2)].ToString();
                ++i;
                currentItem = new TestShellItemProxy(segmentName) { Parent = shell };
                shellItemProxies.Add(currentItem);
            }
            else if (c == 's')
            {
                var segmentName = shellContents[i..(i + 2)].ToString();
                ++i;
                var item = currentItem ?? new TestShellItemProxy($"IMPL_{segmentName}") { Parent = shell };
                currentSection = new TestShellSectionProxy(segmentName) { Parent = item };
                ((IList<IShellSectionProxy>) item.Sections).Add(currentSection);

                if (currentItem is null)
                {
                    shellItemProxies.Add(item);
                }
            }
            else if (c == 'c')
            {
                var pageType = pageTypes[shellContents[i + 1]];
                var segmentName = pageType.Name;
                ++i;
                var section = currentSection ?? new TestShellSectionProxy($"IMPL_{segmentName}");

                ((IList<IShellContentProxy>) section.Contents).Add(
                    new TestShellContentProxy(pageType, segmentName) { Parent = section }
                );

                if (currentSection is null)
                {
                    var item = currentItem ?? new TestShellItemProxy($"IMPL_{segmentName}") { Parent = shell };
                    ((TestShellSectionProxy) section).Parent = item;
                    ((IList<IShellSectionProxy>) item.Sections).Add(section);

                    if (currentItem is null)
                    {
                        shellItemProxies.Add(item);
                    }
                }
            }
            else if (c == ']')
            {
                if (currentSection is not null)
                {
                    currentSection = null;
                }
                else if (currentItem is not null)
                {
                    currentItem = null;
                }
            }
        }

        return shellItemProxies;
    }
}
