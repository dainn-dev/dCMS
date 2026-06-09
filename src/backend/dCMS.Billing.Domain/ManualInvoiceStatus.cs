namespace dCMS.Billing.Domain;

public enum ManualInvoiceStatus
{
    None = 0,
    Draft = 1,
    Sent = 2,
    Paid = 3,
    Overdue = 4,
    Waived = 5,
}

public static class ManualInvoiceStatusExtensions
{
    public static string ToPersistedValue(this ManualInvoiceStatus status) =>
        status switch
        {
            ManualInvoiceStatus.None => "none",
            ManualInvoiceStatus.Draft => "draft",
            ManualInvoiceStatus.Sent => "sent",
            ManualInvoiceStatus.Paid => "paid",
            ManualInvoiceStatus.Overdue => "overdue",
            ManualInvoiceStatus.Waived => "waived",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    public static ManualInvoiceStatus ParsePersisted(string value) =>
        value switch
        {
            "none" => ManualInvoiceStatus.None,
            "draft" => ManualInvoiceStatus.Draft,
            "sent" => ManualInvoiceStatus.Sent,
            "paid" => ManualInvoiceStatus.Paid,
            "overdue" => ManualInvoiceStatus.Overdue,
            "waived" => ManualInvoiceStatus.Waived,
            _ => throw new ArgumentException($"Unknown manual invoice status '{value}'.", nameof(value)),
        };
}
