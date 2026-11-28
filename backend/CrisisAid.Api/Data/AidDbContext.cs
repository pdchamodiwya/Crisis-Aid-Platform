
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Data;

public class AidDbContext(
    DbContextOptions<AidDbContext> options
) : DbContext(options)
{
    // ==========================================
    // USERS, ORGANISATIONS AND PERMISSIONS
    // ==========================================

    public DbSet<Organisation> Organisations
        => Set<Organisation>();

    public DbSet<RoleDefinition> RoleDefinitions
        => Set<RoleDefinition>();

    public DbSet<PermissionDefinition> PermissionDefinitions
        => Set<PermissionDefinition>();

    public DbSet<RolePermission> RolePermissions
        => Set<RolePermission>();

    public DbSet<UserAccount> Users
        => Set<UserAccount>();

    // ==========================================
    // CRISIS AND ASSISTANCE REQUESTS
    // ==========================================

    public DbSet<CrisisEvent> Crises
        => Set<CrisisEvent>();

    public DbSet<ServiceArea> ServiceAreas
        => Set<ServiceArea>();

    public DbSet<AssistanceRequest> Requests
        => Set<AssistanceRequest>();

    public DbSet<RequestHistory> RequestHistories
        => Set<RequestHistory>();

    public DbSet<AiRecommendation> AiRecommendations
        => Set<AiRecommendation>();

    // ==========================================
    // RESOURCE AND LOGISTICS MANAGEMENT
    // ==========================================

    public DbSet<ResourceItem> Resources
        => Set<ResourceItem>();

    public DbSet<ResourceAllocation> ResourceAllocations
        => Set<ResourceAllocation>();

    // ==========================================
    // SHELTER MANAGEMENT
    // ==========================================

    public DbSet<Shelter> Shelters
        => Set<Shelter>();

    public DbSet<ShelterAllocation> ShelterAllocations
        => Set<ShelterAllocation>();

    // ==========================================
    // VOLUNTEER MANAGEMENT
    // ==========================================

    public DbSet<VolunteerProfile> Volunteers
        => Set<VolunteerProfile>();

    public DbSet<VolunteerTask> VolunteerTasks
        => Set<VolunteerTask>();

    // ==========================================
    // MEMBER 05 - DONATION MANAGEMENT
    // ==========================================

    public DbSet<DonationCampaign> Campaigns
        => Set<DonationCampaign>();

    public DbSet<Donation> Donations
        => Set<Donation>();

    public DbSet<MaterialDonationItem> MaterialDonationItems
        => Set<MaterialDonationItem>();

    public DbSet<DonationAllocation> DonationAllocations
        => Set<DonationAllocation>();

    public DbSet<CampaignExpenditure> CampaignExpenditures
        => Set<CampaignExpenditure>();

    public DbSet<DonationFraudFlag> DonationFraudFlags
        => Set<DonationFraudFlag>();

    public DbSet<DonationReceipt> DonationReceipts
        => Set<DonationReceipt>();

    // ==========================================
    // MEMBER 05 - COMMUNICATION SERVICES
    // ==========================================

    public DbSet<CommunicationPreference> CommunicationPreferences
        => Set<CommunicationPreference>();

    public DbSet<PushSubscription> PushSubscriptions
        => Set<PushSubscription>();

    public DbSet<Notification> Notifications
        => Set<Notification>();

    public DbSet<OutboxMessage> Outbox
        => Set<OutboxMessage>();

    // ==========================================
    // OTHER PLATFORM MODULES
    // ==========================================

    public DbSet<AuditEvent> Audits
        => Set<AuditEvent>();

    public DbSet<PublicAlert> PublicAlerts
        => Set<PublicAlert>();

    public DbSet<HouseholdProfile> Households
        => Set<HouseholdProfile>();

    public DbSet<AssistanceFeedback> Feedback
        => Set<AssistanceFeedback>();

    public DbSet<PartnerOffer> PartnerOffers
        => Set<PartnerOffer>();


    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);

        // ======================================
        // EXISTING INDEX CONFIGURATIONS
        // ======================================

        model.Entity<HouseholdProfile>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        model.Entity<AssistanceFeedback>()
            .HasIndex(x => x.RequestId)
            .IsUnique();

        model.Entity<PublicAlert>()
            .HasIndex(x => new { x.CrisisId, x.CreatedAt });

        model.Entity<UserAccount>()
            .HasIndex(x => x.Email)
            .IsUnique();

        model.Entity<RoleDefinition>()
            .HasIndex(x => x.Name)
            .IsUnique();

        model.Entity<PermissionDefinition>()
            .HasIndex(x => x.Code)
            .IsUnique();

        model.Entity<RolePermission>()
            .HasKey(x => new
            {
                x.RoleDefinitionId,
                x.PermissionDefinitionId
            });

        model.Entity<OutboxMessage>()
            .HasIndex(x => x.EventKey)
            .IsUnique();

        model.Entity<AssistanceRequest>()
            .HasIndex(x => new
            {
                x.CrisisId,
                x.Status,
                x.CreatedAt
            });

        model.Entity<AiRecommendation>()
            .HasIndex(x => new { x.RequestId, x.CreatedAt });

        model.Entity<RequestHistory>()
            .HasIndex(x => new { x.RequestId, x.At });

        model.Entity<AuditEvent>()
            .HasIndex(x => new
            {
                x.EntityType,
                x.EntityId,
                x.At
            });

        // ======================================
        // DECIMAL PRECISION
        // ======================================

        model.Entity<Donation>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        model.Entity<DonationCampaign>()
            .Property(x => x.TargetAmount)
            .HasPrecision(18, 2);

        model.Entity<DonationAllocation>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        model.Entity<CampaignExpenditure>()
            .Property(x => x.Amount)
            .HasPrecision(18, 2);

        // ======================================
        // FIELD LENGTH CONFIGURATION
        // ======================================

        model.Entity<AssistanceRequest>()
            .Property(x => x.Description)
            .HasMaxLength(2000);

        model.Entity<UserAccount>()
            .Property(x => x.Email)
            .HasMaxLength(160);

        model.Entity<OutboxMessage>()
            .Property(x => x.EventKey)
            .HasMaxLength(240);

        model.Entity<Notification>()
            .Property(x => x.EventKey)
            .HasMaxLength(240);

        model.Entity<PushSubscription>()
            .Property(x => x.Endpoint)
            .HasMaxLength(850);

        // ======================================
        // MEMBER 05 - DONATION FIELD LIMITS
        // ======================================

        model.Entity<DonationCampaign>()
            .Property(x => x.Name)
            .HasMaxLength(120);

        model.Entity<DonationCampaign>()
            .Property(x => x.Currency)
            .HasMaxLength(3);

        model.Entity<Donation>()
            .Property(x => x.Currency)
            .HasMaxLength(3);

        model.Entity<Donation>()
            .Property(x => x.Kind)
            .HasMaxLength(30);

        model.Entity<Donation>()
            .Property(x => x.Status)
            .HasMaxLength(40);

        model.Entity<Donation>()
            .Property(x => x.TransactionReference)
            .HasMaxLength(150);

        model.Entity<MaterialDonationItem>()
            .Property(x => x.Category)
            .HasMaxLength(50);

        model.Entity<MaterialDonationItem>()
            .Property(x => x.ItemName)
            .HasMaxLength(150);

        model.Entity<MaterialDonationItem>()
            .Property(x => x.Unit)
            .HasMaxLength(30);

        model.Entity<MaterialDonationItem>()
            .Property(x => x.Condition)
            .HasMaxLength(50);

        model.Entity<MaterialDonationItem>()
            .Property(x => x.ReceivingLocation)
            .HasMaxLength(200);

        model.Entity<DonationReceipt>()
            .Property(x => x.ReceiptNumber)
            .HasMaxLength(100);

        // ======================================
        // MEMBER 05 - UNIQUE INDEXES
        // ======================================

        model.Entity<DonationReceipt>()
            .HasIndex(x => x.ReceiptNumber)
            .IsUnique();

        model.Entity<DonationReceipt>()
            .HasIndex(x => x.DonationId)
            .IsUnique();

        model.Entity<CommunicationPreference>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        model.Entity<PushSubscription>()
            .HasIndex(x => x.Endpoint)
            .IsUnique();

        model.Entity<Notification>()
            .HasIndex(x => x.EventKey)
            .IsUnique()
            .HasFilter("[EventKey] IS NOT NULL");

        // ======================================
        // MEMBER 05 - PERFORMANCE INDEXES
        // ======================================

        model.Entity<OutboxMessage>()
            .HasIndex(x => new
            {
                x.Status,
                x.NextAttemptAt
            });

        model.Entity<Donation>()
            .HasIndex(x => new
            {
                x.CampaignId,
                x.Status
            });

        model.Entity<Donation>()
            .HasIndex(x => new
            {
                x.DonorId,
                x.At
            });

        model.Entity<Donation>()
            .HasIndex(x => x.TransactionReference);

        model.Entity<DonationFraudFlag>()
            .HasIndex(x => new
            {
                x.DonationId,
                x.Status
            });

        model.Entity<DonationAllocation>()
            .HasIndex(x => new
            {
                x.DonationId,
                x.Status
            });

        // ======================================
        // EXISTING FOREIGN KEY RELATIONSHIPS
        // ======================================

        model.Entity<RolePermission>()
            .HasOne<RoleDefinition>()
            .WithMany()
            .HasForeignKey(x => x.RoleDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<RolePermission>()
            .HasOne<PermissionDefinition>()
            .WithMany()
            .HasForeignKey(x => x.PermissionDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<UserAccount>()
            .HasOne<RoleDefinition>()
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<UserAccount>()
            .HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(x => x.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<AssistanceRequest>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.CitizenId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<AssistanceRequest>()
            .HasOne<CrisisEvent>()
            .WithMany()
            .HasForeignKey(x => x.CrisisId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<ServiceArea>()
            .HasOne<CrisisEvent>()
            .WithMany()
            .HasForeignKey(x => x.CrisisId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<RequestHistory>()
            .HasOne<AssistanceRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<AiRecommendation>()
            .HasOne<AssistanceRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<AiRecommendation>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<ResourceItem>()
            .HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(x => x.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<ResourceAllocation>()
            .HasOne<ResourceItem>()
            .WithMany()
            .HasForeignKey(x => x.ResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<ResourceAllocation>()
            .HasOne<AssistanceRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<ShelterAllocation>()
            .HasOne<Shelter>()
            .WithMany()
            .HasForeignKey(x => x.ShelterId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<ShelterAllocation>()
            .HasOne<AssistanceRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<VolunteerProfile>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<VolunteerTask>()
            .HasOne<VolunteerProfile>()
            .WithMany()
            .HasForeignKey(x => x.VolunteerId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<VolunteerTask>()
            .HasOne<AssistanceRequest>()
            .WithMany()
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<DonationCampaign>()
            .HasOne<CrisisEvent>()
            .WithMany()
            .HasForeignKey(x => x.CrisisId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<Donation>()
            .HasOne<DonationCampaign>()
            .WithMany()
            .HasForeignKey(x => x.CampaignId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<Donation>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.DonorId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<Notification>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<OutboxMessage>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<AuditEvent>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        // ======================================
        // MEMBER 05 - DONATION RELATIONSHIPS
        // ======================================

        model.Entity<DonationCampaign>()
            .HasOne<Organisation>()
            .WithMany()
            .HasForeignKey(x => x.OrganisationId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<DonationCampaign>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<Donation>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ConfirmedBy)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<MaterialDonationItem>()
            .HasOne<Donation>()
            .WithMany()
            .HasForeignKey(x => x.DonationId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<MaterialDonationItem>()
            .HasOne<ResourceItem>()
            .WithMany()
            .HasForeignKey(x => x.InventoryResourceId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<DonationAllocation>()
            .HasOne<Donation>()
            .WithMany()
            .HasForeignKey(x => x.DonationId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<DonationAllocation>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.AllocatedBy)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<CampaignExpenditure>()
            .HasOne<DonationAllocation>()
            .WithMany()
            .HasForeignKey(x => x.AllocationId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<CampaignExpenditure>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<DonationReceipt>()
            .HasOne<Donation>()
            .WithMany()
            .HasForeignKey(x => x.DonationId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<DonationFraudFlag>()
            .HasOne<Donation>()
            .WithMany()
            .HasForeignKey(x => x.DonationId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<DonationFraudFlag>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // ======================================
        // MEMBER 05 - COMMUNICATION RELATIONSHIPS
        // ======================================

        model.Entity<CommunicationPreference>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<PushSubscription>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        model.Entity<OutboxMessage>()
            .HasOne<PushSubscription>()
            .WithMany()
            .HasForeignKey(x => x.PushSubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ======================================
        // MEMBER 05 - DATABASE CHECK CONSTRAINTS
        // ======================================

        model.Entity<MaterialDonationItem>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_MaterialDonationItem_Quantity",
                    "[Quantity] > 0");

                t.HasCheckConstraint(
                    "CK_MaterialDonationItem_ReceivedQuantity",
                    "[ReceivedQuantity] IS NULL OR " +
                    "([ReceivedQuantity] >= 0 AND " +
                    "[ReceivedQuantity] <= [Quantity])");
            });

        model.Entity<DonationAllocation>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_DonationAllocation_Amount",
                    "[Amount] > 0");
            });

        model.Entity<CampaignExpenditure>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_CampaignExpenditure_Amount",
                    "[Amount] > 0");
            });

        model.Entity<Donation>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_Donation_Amount",
                    "[Amount] >= 0");
            });

        model.Entity<DonationCampaign>()
            .ToTable(t =>
            {
                t.HasCheckConstraint(
                    "CK_DonationCampaign_TargetAmount",
                    "[TargetAmount] > 0");
            });
    }
}
