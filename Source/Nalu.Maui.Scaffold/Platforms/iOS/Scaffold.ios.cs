namespace Nalu;

public partial class Scaffold
{
    partial void EnsurePresenter() => Presenter ??= new ScaffoldPresenter(this);
}
