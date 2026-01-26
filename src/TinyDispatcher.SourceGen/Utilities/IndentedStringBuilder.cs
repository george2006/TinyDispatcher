using System.Text;

namespace TinyDispatcher.SourceGen.Internal
{
    /// <summary>
    /// Small helper to build indented source code in a readable way.
    /// </summary>
    internal sealed class IndentedStringBuilder
    {
        private readonly StringBuilder _builder = new();
        private int _indentLevel;
        private const int SpacesPerIndent = 2;

        public IndentedStringBuilder Indent()
        {
            _indentLevel++;
            return this;
        }

        public IndentedStringBuilder Unindent()
        {
            if (_indentLevel > 0)
            {
                _indentLevel--;
            }

            return this;
        }

        public IndentedStringBuilder AppendLine(string text = "")
        {
            if (text.Length > 0)
            {
                _builder
                    .Append(' ', _indentLevel * SpacesPerIndent)
                    .AppendLine(text);
            }
            else
            {
                _builder.AppendLine();
            }

            return this;
        }

        public override string ToString() => _builder.ToString();
    }
}
