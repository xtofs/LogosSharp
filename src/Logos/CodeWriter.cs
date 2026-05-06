namespace Logos;

using System.Text;
using System.CodeDom.Compiler;


internal sealed class CodeWriter
{
    private readonly StringBuilder _builder = new();
    private readonly StringWriter _stringWriter;
    private readonly IndentedTextWriter _writer;

    public CodeWriter()
    {
        _stringWriter = new StringWriter(_builder);
        _writer = new IndentedTextWriter(_stringWriter, "    ");
    }


    public int Indent { get => _writer.Indent; set => _writer.Indent = value; }

    public void WriteLine(string text = "")
    {
        _writer.WriteLine(text);
    }

    public void WriteMultiLine(string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            _writer.WriteLine(line);
        }
    }

    public override string ToString()
    {
        _writer.Flush();
        return _builder.ToString();
    }
}
