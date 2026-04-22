using System.CodeDom.Compiler;

namespace Nalu.SharpState.Generators.Emit;

/// <summary>
/// Thin wrapper around <see cref="IndentedTextWriter"/> that exposes a couple of convenience helpers
/// used by the state machine emitter.
/// </summary>
internal sealed class SourceWriter
{
    private readonly StringWriter _buffer = new();
    private readonly IndentedTextWriter _writer;

    public SourceWriter()
    {
        _writer = new IndentedTextWriter(_buffer, "    ");
    }

    public int Indent
    {
        get => _writer.Indent;
        set => _writer.Indent = value;
    }

    public SourceWriter Write(string value)
    {
        _writer.Write(value);
        return this;
    }

    public SourceWriter WriteLine(string value = "")
    {
        _writer.WriteLine(value);
        return this;
    }

    public SourceWriter WriteBlankLine()
    {
        _writer.WriteLine();
        return this;
    }

    public IDisposable Block(string header = "")
    {
        if (!string.IsNullOrEmpty(header))
        {
            _writer.WriteLine(header);
        }

        _writer.WriteLine("{");
        _writer.Indent++;
        return new BlockScope(this);
    }

    private sealed class BlockScope : IDisposable
    {
        private readonly SourceWriter _owner;

        public BlockScope(SourceWriter owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            _owner._writer.Indent--;
            _owner._writer.WriteLine("}");
        }
    }

    public override string ToString() => _buffer.ToString();
}
