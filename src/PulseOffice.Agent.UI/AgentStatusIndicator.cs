using Avalonia.Threading;
using PulseOffice.Agent.Security;
using PulseOffice.Agent.Transport;
using PulseOffice.Localization;
using PulseOffice.UI.Controls.Services;

namespace PulseOffice.Agent.UI;

public interface IAgentStatusIndicator
{
    void Start();
}

public sealed class AgentStatusIndicator(
    IStatusBarService status,
    IAgentServer server,
    IAgentSessions sessions,
    ITextCatalog texts) : IAgentStatusIndicator
{
    private const string SegmentId = "agent.connection";

    public void Start()
    {
        server.StateChanged += (_, _) => Dispatcher.UIThread.Post(Update);
        sessions.Changed += (_, _) => Dispatcher.UIThread.Post(Update);
        texts.LanguageChanged += (_, _) => Dispatcher.UIThread.Post(Update);
        Update();
    }

    private void Update()
    {
        if (!server.IsRunning)
        {
            status.Remove(SegmentId);
            return;
        }

        var active = sessions.Active;
        var segment = status.Register(SegmentId, StatusSegmentKind.Permanent);
        segment.Text = active.Count > 0
            ? texts.Format("agent.status.connected", string.Join(", ", active.Select(session => session.Client).Distinct(StringComparer.Ordinal)))
            : texts["agent.status.listening"];
        segment.Tooltip = texts.Format("agent.status.tooltip", server.Address?.ToString() ?? string.Empty);
        segment.AccessibleName = segment.Text;
    }
}
