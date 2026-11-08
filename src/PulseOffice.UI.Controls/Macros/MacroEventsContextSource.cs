namespace PulseOffice.UI.Controls.Macros;

public interface IMacroEventsContextSource
{
    MacroEventsContext? Create(bool readOnly);
}
