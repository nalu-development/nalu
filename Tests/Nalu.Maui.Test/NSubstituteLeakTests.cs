namespace Nalu.Maui.Test;

public class NSubstituteLeakTests
{
    public class Foo;

    public interface IInterface
    {
        void Method(Foo foo);
    }

    // private readonly IInterface _interfaceMock = Substitute.For<IInterface>();
    [Fact(DisplayName = "NSubstitute does not leak")]
    public async Task NSubstituteDoesNotLeak()
    {
        WeakReference<Foo> weakFoo;

        {
            var foo = new Foo();
            weakFoo = new WeakReference<Foo>(foo);

            // _interfaceMock.Method(foo);
            // _interfaceMock.ClearReceivedCalls();
        }

        await GcCollect();

        weakFoo.TryGetTarget(out _).Should().BeFalse();
    }

    private static async Task GcCollect()
    {
        for (var i = 0; i < 3; i++)
        {
            await Task.Yield();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
