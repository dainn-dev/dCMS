namespace dCMS.Order.Api.Contracts;

public sealed class UpdateRefundCaseRequest
{
    public string? Status { get; set; }
    public string? Remark { get; set; }
}

public sealed record RefundCaseListItemDto(
    string RefundNo,
    string ReferenceHash,
    string OrderId,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string PaymentReferenceNo,
    string RequestDate,
    string? RefundDate,
    string Status,
    string Remark);
