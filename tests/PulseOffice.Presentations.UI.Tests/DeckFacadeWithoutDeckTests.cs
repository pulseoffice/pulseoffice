using PulseOffice.Presentations.UI.Scripting;
using PulseOffice.Scripting.Editor.Facades;
using Xunit;

namespace PulseOffice.Presentations.UI.Tests;

public sealed class DeckFacadeWithoutDeckTests
{
    private sealed class DirectDispatcher : IScriptDispatcher
    {
        public T Read<T>(Func<T> read) => read();

        public void Change(Action change) => change();

        public void Show(Action show) => show();

        public T Write<T>(Func<T> write) => write();
    }

    private readonly DeckFacade _facade = new(new DirectDispatcher(), new ActiveDeckContext());

    [Fact]
    public void ReadingASlideWithoutAPresentationSaysNoneIsOpen()
    {
        Assert.Equal("No presentation is open.", Assert.Throws<InvalidOperationException>(() => _facade.Notes(1)).Message);
        Assert.Equal("No presentation is open.", Assert.Throws<InvalidOperationException>(() => _facade.Title(1)).Message);
    }
}
