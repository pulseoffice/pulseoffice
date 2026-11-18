using PulseOffice.UI.Controls.Lists;

namespace PulseOffice.UI.Controls.Review;

public interface IMentionPickers
{
    MentionPicker Create();
}

public sealed class MentionPickers(IMentionQuery query) : IMentionPickers
{
    public MentionPicker Create() => new(query, new ChoiceListPopup());
}
