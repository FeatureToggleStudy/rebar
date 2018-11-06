using RustyWires.SourceModel;

namespace RustyWires.Design
{
    public class InputParameterAccessorViewModel : BasicNodeViewModel
    {
        public InputParameterAccessorViewModel(InputParameterAccessor accessor, string name) 
            : base(accessor, name)
        {
        }

        /// <inheritdoc />
        public override bool CanChangeTemplate => false;
    }
}
