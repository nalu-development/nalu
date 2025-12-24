namespace Nalu.Maui.TestApp;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class TestPageAttribute : Attribute
{
    public string Name { get; }

    public TestPageAttribute(string name)
    {
        Name = name;
    }
}
