namespace PulseOffice.UI.Controls.Services;

public interface IDialogResultAdmission
{
    IDialogAdmissionTicket Open();
}

public interface IDialogAdmissionTicket
{
    bool Admits();
}
