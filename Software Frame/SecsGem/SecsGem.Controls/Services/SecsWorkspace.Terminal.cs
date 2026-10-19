namespace SecsGem.Controls.Services;

public sealed partial class SecsWorkspace
{
    private void RefreshTerminalMessages()
    {
        if (_disposed) return;
        var selected = Model.SelectedTerminalMessage?.Message.Id;
        Model.TerminalMessages = _runtime.GetTerminalMessageHistory();
        Model.SelectedTerminalMessage = Model.TerminalMessages.FirstOrDefault(entry => entry.Message.Id == selected);
    }

    public void AcknowledgeTerminal()
    {
        if (_disposed || Model.SelectedTerminalMessage is not { } selected) return;
        _runtime.AcknowledgeTerminalMessage(selected.Message.Id);
        RefreshTerminalMessages();
    }
}
