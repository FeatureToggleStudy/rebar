using NationalInstruments.Shell;
using Rebar.SourceModel;

namespace Rebar.Design
{
    /// <summary>
    /// <see cref="DocumentEditControlInfo"/> implementation for <see cref="FunctionDiagramEditor"/>.
    /// </summary>
    public class FunctionDiagramEditorInfo : DocumentEditControlInfo<FunctionDiagramEditor>
    {
        public FunctionDiagramEditorInfo(string uniqueId, FunctionDocument document)
            : base(uniqueId, document, document.Function.Diagram, "editor", FunctionDiagramPaletteLoader.DiagramPaletteIdentifier, string.Empty, string.Empty)
        {
            ClipboardDataFormat = Function.FunctionClipboardDataFormat;
        }
    }
}
