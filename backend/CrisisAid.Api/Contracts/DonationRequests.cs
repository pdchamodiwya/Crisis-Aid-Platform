namespace CrisisAid.Api.Contracts;

public record CampaignInput(
    string Name,
    string Description,
    Guid CrisisId,
    decimal TargetAmount,
    string Currency,
    DateTime StartDate,
    DateTime? EndDate
);

public record MonetaryDonationInput(
    Guid CampaignId,
    decimal Amount,
    string Currency,
    string? Notes
);

public record MaterialDonationInput(
    Guid CampaignId,
    string Category,
    string ItemName,
    int Quantity,
    string Unit,
    string Condition,
    string ReceivingLocation
);

public record ConfirmDonationInput(
    string? TransactionReference,
    string? PaymentMethod,
    int? ReceivedQuantity,
    string? Notes,
    bool ReceiptVerified = false
);

public record RejectDonationInput(
    string Reason
);

public record DonationAllocationInput(
    Guid DonationId,
    string Category,
    string Purpose,
    decimal Amount
);

public record ExpenditureInput(
    Guid AllocationId,
    string Description,
    decimal Amount,
    string Reference
);

public record FraudReviewInput(
    string Status,
    string InvestigationNotes
);