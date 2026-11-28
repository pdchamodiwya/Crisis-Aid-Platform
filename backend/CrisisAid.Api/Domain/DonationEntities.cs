namespace CrisisAid.Api.Domain;

public class MaterialDonationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DonationId { get; set; }

    public string Category { get; set; } = "";
    public string ItemName { get; set; } = "";
    public int Quantity { get; set; }

    public string Unit { get; set; } = "";
    public string Condition { get; set; } = "Good";
    public string ReceivingLocation { get; set; } = "";

    public int? ReceivedQuantity { get; set; }
    public Guid? InventoryResourceId { get; set; }
    public DateTime? InventoryReceivedAt { get; set; }
}

public class DonationAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DonationId { get; set; }

    public string Category { get; set; } = "";
    public string Purpose { get; set; } = "";

    public decimal Amount { get; set; }

    public Guid AllocatedBy { get; set; }

    public string Status { get; set; } = "Approved";
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
}

public class CampaignExpenditure
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AllocationId { get; set; }

    public string Description { get; set; } = "";
    public decimal Amount { get; set; }

    public string Reference { get; set; } = "";

    public Guid RecordedBy { get; set; }
    public DateTime SpentAt { get; set; } = DateTime.UtcNow;
}

public class DonationFraudFlag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DonationId { get; set; }

    public string Reason { get; set; } = "";
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";

    public Guid? ReviewedBy { get; set; }
    public string? InvestigationNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public class DonationReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DonationId { get; set; }

    public string ReceiptNumber { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public string Signature { get; set; } = "";

    public string KeyVersion { get; set; } = "v1";

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? RevocationReason { get; set; }
}