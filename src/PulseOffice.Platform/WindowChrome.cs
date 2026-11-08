namespace PulseOffice.Platform;

public enum MenuPlacement
{
    InTitleBar,

    SystemMenuBar,
}

public interface IWindowChrome
{
    MenuPlacement MenuPlacement { get; }

    double LeadingInset { get; }

    double TrailingInset { get; }

    bool ShowsTitleText { get; }

    bool DrawsCaptionButtons { get; }
}

public interface IWindowChromeProvider
{
    IWindowChrome Current { get; }
}
