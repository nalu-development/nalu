using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class CreditCardModel : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; }
    
    [ObservableProperty]
    public partial string Type { get; set; }
    
    [ObservableProperty]
    public partial string Exp { get; set; }

    [ObservableProperty]
    public partial bool Starred { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CreditDollar))]
    public partial double Credit { get; set; }
    
    public string CreditDollar => Credit.ToString("C2");

    [RelayCommand]
    public void StarUnstar() => Starred = !Starred;
}

public class MagnetDemoPageModel : ObservableObject
{
    public List<CreditCardModel> Credits { get; }

    public MagnetDemoPageModel()
    {
        // Generate 20 sample credit cards
        Credits = [];
        var rand = new Random();

        for (var i = 0; i < 20; i++)
        {
            Credits.Add(
                new CreditCardModel
                {
                    Name = rand.Next(0, 2) == 1 ? $"Regular card {i + 1}" : $"Premium card with a long name {i + 1}",
                    Type = i % 2 == 0 ? "Visa" : "MasterCard",
                    Exp = $"{rand.Next(1, 13):D2}/{rand.Next(24, 30)}",
                    Credit = rand.Next(100, 5000),
                    Starred = rand.Next(0, 2) == 1
                }
            );
        }
    }
}
