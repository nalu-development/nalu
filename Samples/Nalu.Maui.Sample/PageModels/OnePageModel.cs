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
    [FromKeyedServices("dummyjson")] HttpClient httpClient) : ObservableObject
{
    private static int _instanceCount;

    [ObservableProperty]
    private string? _result1;

    [ObservableProperty]
    private string? _result2;

    [ObservableProperty]
    private string? _result3;

    [ObservableProperty]
    private string? _result4;

    public AnimalModel Animal { get; } = new() { Name = "Dog" };

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PushThreeAsync() => navigationService.GoToAsync(Navigation.Relative().Push<ThreePageModel>());

    [RelayCommand]
    private async Task SendRequestAsync()
    {
        Result1 = null;
        Result2 = null;
        Result3 = null;
        Result4 = null;

        var parallelRequestTask1 = SendRequestAsync(CreateTestLongRequest(5000));
        Result1 = "waiting...";
        var parallelRequestTask2 = SendRequestAsync(CreateTestLongRequest(4000));
        Result2 = "waiting...";

        Result1 = await parallelRequestTask1;
        Result2 = await parallelRequestTask2;

        var followUpRequest = SendRequestAsync(CreateTestLongRequest(5000));
        Result3 = "waiting...";
        Result3 = await followUpRequest;

        var followUpRequest2 = SendRequestAsync(CreateTestLongRequest(999999));
        Result4 = "waiting...";
        Result4 = await followUpRequest2;
    }

    private async Task<string> SendRequestAsync(HttpRequestMessage requestMessage)
    {
        string result;
        try
        {
            var responseMessage = await httpClient.SendAsync(requestMessage);
            result = await responseMessage.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            result = e.Message;
        }

        return result;
    }

    /// <summary>
    /// Creates a request to an endpoint that will delay the response.
    /// See https://dummyjson.com/docs#intro-test for more information.
    /// </summary>
    private HttpRequestMessage CreateTestLongRequest(int delayMs = 5000) =>
        new()
        {
            RequestUri = new Uri($"test?delay={delayMs}", UriKind.Relative),
            Method = HttpMethod.Post,
            Content = JsonContent.Create(Animal),
        };
}
