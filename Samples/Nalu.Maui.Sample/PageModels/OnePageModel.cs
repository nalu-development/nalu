namespace Nalu.Maui.Sample.PageModels;

using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public class AnimalModel
{
    public string Name { get; set; } = null!;
}

public partial class OnePageModel(
    INavigationService navigationService,
    [FromKeyedServices("dummyjson")] IBackgroundHttpClient backgroundHttpClient,
    [FromKeyedServices("dummyjson")] HttpClient httpClient) : ObservableObject
{
    private static int _instanceCount;

    [ObservableProperty]
    private BackgroundHttpRequestHandle? _requestHandle1;
    [ObservableProperty]
    private string? _result1;

    [ObservableProperty]
    private BackgroundHttpRequestHandle? _requestHandle2;
    [ObservableProperty]
    private string? _result2;

    [ObservableProperty]
    private BackgroundHttpRequestHandle? _requestHandle3;
    [ObservableProperty]
    private string? _result3;

    public AnimalModel Animal { get; } = new AnimalModel { Name = "Dog" };

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PushThreeAsync() => navigationService.GoToAsync(Navigation.Relative().Push<ThreePageModel>());

    [RelayCommand]
    private async Task SendRequestAsync()
    {
        RequestHandle1 = null;
        RequestHandle2 = null;
        RequestHandle3 = null;
        Result1 = null;
        Result2 = null;
        Result3 = null;

        // Update the UI before starting the request
        await Task.Yield();

        var requestTask1 = httpClient.SendAsync(CreateTestLongRequest());
        var requestTask2 = httpClient.SendAsync(CreateTestLongRequest());

        try
        {
            var result1 = await requestTask1;
            Result1 = await result1.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            Result1 = e.Message;
        }

        try
        {
            var result2 = await requestTask2;
            Result2 = await result2.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            Result2 = e.Message;
        }

        try
        {
            var result3 = await httpClient.SendAsync(CreateTestLongRequest());
            Result3 = await result3.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            Result3 = e.Message;
        }
    }

    [RelayCommand]
    private async Task SendBackgroundRequestAsync()
    {
        RequestHandle1 = null;
        RequestHandle2 = null;
        RequestHandle3 = null;
        Result1 = null;
        Result2 = null;
        Result3 = null;

        // Update the UI before starting the request
        await Task.Yield();

        RequestHandle1 = await backgroundHttpClient.StartAsync(CreateTestLongRequest());
        RequestHandle2 = await backgroundHttpClient.StartAsync(CreateTestLongRequest());

        var result1 = await RequestHandle1.GetResultAsync();
        Result1 = await result1.Content.ReadAsStringAsync();

        var result2 = await RequestHandle2.GetResultAsync();
        Result2 = await result2.Content.ReadAsStringAsync();

        RequestHandle3 = await backgroundHttpClient.StartAsync(CreateTestLongRequest());

        RequestHandle1.Acknowledge();
        RequestHandle2.Acknowledge();

        var result3 = await RequestHandle3.GetResultAsync();
        Result3 = await result3.Content.ReadAsStringAsync();
        RequestHandle3.Acknowledge();
    }

    // See https://dummyjson.com/docs#intro-test for more information
    private BackgroundHttpRequestMessage CreateTestLongRequest() =>
        new()
        {
            RequestUri = new Uri("test?delay=5000", UriKind.Relative),
            Method = HttpMethod.Post,
            Content = JsonContent.Create(Animal),
        };
}
