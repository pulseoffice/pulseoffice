using PulseOffice.Document.Commands;
using Xunit;

namespace PulseOffice.Document.Commands.Tests;

public class UndoStackTests
{
    private static IUndoableAction Action(List<string> log, string name) =>
    new DelegateAction(name, () => log.Add($"+{name}"), () => log.Add($"-{name}"));

    [Fact]
    public void UnaCronologiaVuotaNonAnnullaNulla()
    {
        var stack = new UndoStack();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);

        stack.Undo();
        stack.Redo();
    }

    [Fact]
    public void OltreLaCapienzaAnnullareTuttoNonRiportaAlloStatoSalvato()
    {
        var log = new List<string>();
        var stack = new UndoStack(capacity: 2);
        var saved = stack.StatePoint;

        stack.Push(Action(log, "uno"));
        stack.Push(Action(log, "due"));
        stack.Push(Action(log, "tre"));
        stack.Undo();
        stack.Undo();

        Assert.False(stack.CanUndo);
        Assert.NotEqual(saved, stack.StatePoint);
    }

    [Fact]
    public void UnaStoriaLungaRestaEntroLaCapienza()
    {
        var stack = new UndoStack(capacity: 200);
        for (var index = 0; index < 100_000; index++) stack.Push(new DelegateAction("x", () => { }, () => { }));

        var undone = 0;
        while (stack.CanUndo)
        {
            stack.Undo();
            undone++;
        }

        Assert.Equal(200, undone);
    }

    [Fact]
    public void AnnullaERipristinaInOrdine()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        stack.Push(Action(log, "uno"));
        stack.Push(Action(log, "due"));

        stack.Undo();
        stack.Undo();

        Assert.Equal(["-due", "-uno"], log);

        stack.Redo();
        Assert.Equal(["-due", "-uno", "+uno"], log);
    }

    [Fact]
    public void PushNonEseguelOperazione()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        stack.Push(Action(log, "uno"));

        Assert.Empty(log);
    }

    [Fact]
    public void UnaNuovaOperazioneCancellaCioCheEraStatoAnnullato()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        stack.Push(Action(log, "uno"));
        stack.Undo();

        Assert.True(stack.CanRedo);

        stack.Push(Action(log, "due"));

        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void LaDescrizioneServeAllaVoceDiMenu()
    {
        var stack = new UndoStack();
        stack.Push(new DelegateAction("Grassetto", () => { }, () => { }));

        Assert.Equal("Grassetto", stack.UndoDescription);

        stack.Undo();
        Assert.Equal("Grassetto", stack.RedoDescription);
        Assert.Null(stack.UndoDescription);
    }

    [Fact]
    public void UnaTransazioneDiventaUnSoloAnnullamento()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        using (stack.BeginTransaction("Formattazione"))
        {
            stack.Push(Action(log, "a"));
            stack.Push(Action(log, "b"));
            stack.Push(Action(log, "c"));
        }

        Assert.Equal("Formattazione", stack.UndoDescription);

        stack.Undo();

        Assert.Equal(["-c", "-b", "-a"], log);
    }

    [Fact]
    public void UnaTransazioneVuotaNonCompareNellaCronologia()
    {
        var stack = new UndoStack();

        using (stack.BeginTransaction("Niente")) { }

        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void UnaTransazioneConUnaSolaOperazioneNonSiIncarta()
    {
        var stack = new UndoStack();

        using (stack.BeginTransaction("Gruppo"))
            stack.Push(new DelegateAction("Singola", () => { }, () => { }));

        Assert.Equal("Singola", stack.UndoDescription);
    }

    [Fact]
    public void UnPassoEtichettatoTieneLEtichettaAncheConUnaSolaOperazione()
    {
        var stack = new UndoStack();

        using (stack.BeginLabelledStep("Agente: titolo"))
            stack.Push(new DelegateAction("Singola", () => { }, () => { }));

        Assert.Equal("Agente: titolo", stack.UndoDescription);
    }

    [Fact]
    public async Task UnaModificaDiUnAltroFlussoNonFinisceNelPassoEtichettato()
    {
        var stack = new UndoStack();
        var userTyped = new TaskCompletionSource();
        var userMayType = new TaskCompletionSource();

        var user = Task.Run(async () =>
        {
            await userMayType.Task;
            stack.Push(new DelegateAction("Utente", () => { }, () => { }));
            userTyped.SetResult();
        }, TestContext.Current.CancellationToken);

        await Task.Yield();
        using (stack.BeginLabelledStep("Agente: titolo"))
        {
            stack.Push(new DelegateAction("Agente 1", () => { }, () => { }));
            userMayType.SetResult();
            await userTyped.Task;
            stack.Push(new DelegateAction("Agente 2", () => { }, () => { }));
        }

        await user;

        Assert.Equal("Agente: titolo", stack.UndoDescription);
        stack.Undo();
        Assert.Equal("Utente", stack.UndoDescription);
    }

    [Fact]
    public void LeTransazioniAnnidateNonSpezzanoIlGruppo()
    {
        var log = new List<string>();
        var stack = new UndoStack();

        using (stack.BeginTransaction("Esterna"))
        {
            stack.Push(Action(log, "a"));

            using (stack.BeginTransaction("Interna"))
                stack.Push(Action(log, "b"));

            stack.Push(Action(log, "c"));
        }

        stack.Undo();

        Assert.Equal(["-c", "-b", "-a"], log);
    }

    [Fact]
    public void LaCronologiaHaUnLimite()
    {
        var stack = new UndoStack(capacity: 3);

        for (var i = 0; i < 5; i++)
            stack.Push(new DelegateAction($"op{i}", () => { }, () => { }));

        for (var i = 0; i < 3; i++)
        {
            Assert.True(stack.CanUndo);
            stack.Undo();
        }

        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void LeOperazioniCompatibiliSiFondono()
    {
        var stack = new UndoStack();

        stack.Push(new MergingAction("a"));
        stack.Push(new MergingAction("ab"));
        stack.Push(new MergingAction("abc"));

        Assert.Equal("abc", stack.UndoDescription);

        stack.Undo();
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void IlCambiamentoDellaCronologiaVieneSegnalato()
    {
        var stack = new UndoStack();
        var notifications = 0;
        stack.Changed += (_, _) => notifications++;

        stack.Push(new DelegateAction("op", () => { }, () => { }));
        stack.Undo();
        stack.Redo();

        Assert.Equal(3, notifications);
    }

    private sealed class MergingAction(string text) : IUndoableAction
    {
        public string Description => text;

        public void Redo() { }
        public void Undo() { }

        public bool TryMergeWith(IUndoableAction previous) => previous is MergingAction;
    }

    [Fact]
    public void UnAnnullamentoCheFallisceNonPerdeLAzione()
    {
        var stack = new UndoStack();
        stack.Push(new DelegateAction("x", () => { }, () => throw new InvalidOperationException()));

        Assert.Throws<InvalidOperationException>(stack.Undo);
        Assert.True(stack.CanUndo);
    }

    [Fact]
    public void SvuotareLaCronologiaNonFaSembrareSalvatoIlDocumento()
    {
        var stack = new UndoStack();
        var saved = stack.StatePoint;
        stack.Push(new DelegateAction("x", () => { }, () => { }));
        stack.Clear();

        Assert.NotEqual(saved, stack.StatePoint);
    }

    private sealed record Run(int Start, int End) : IUndoMergeKey
    {
        public bool Continues(IUndoMergeKey previous) => previous is Run earlier && earlier.End == Start;

        public IUndoMergeKey Join(IUndoMergeKey previous) => previous is Run earlier ? this with { Start = earlier.Start } : this;
    }

    [Fact]
    public void Le_transazioni_con_chiave_contigua_si_fondono_in_un_passo()
    {
        var stack = new UndoStack();
        var log = new List<string>();
        foreach (var index in new[] { 0, 1, 2 })
            using (stack.BeginTransaction("typing", new Run(index, index + 1))) stack.Push(Action(log, index.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        using (stack.BeginTransaction("typing", new Run(9, 10))) stack.Push(Action(log, "x"));

        stack.Undo();
        Assert.Equal(["-x"], log);
        stack.Undo();
        Assert.Equal(["-x", "-2", "-1", "-0"], log);
        Assert.False(stack.CanUndo);
    }
}
