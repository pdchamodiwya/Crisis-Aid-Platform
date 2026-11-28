using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Data;

public class AidDbContext(DbContextOptions<AidDbContext> options) : DbContext(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<RoleDefinition> RoleDefinitions => Set<RoleDefinition>();
    public DbSet<PermissionDefinition> PermissionDefinitions => Set<PermissionDefinition>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<CrisisEvent> Crises => Set<CrisisEvent>();
    public DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();
    public DbSet<AssistanceRequest> Requests => Set<AssistanceRequest>();
    public DbSet<RequestHistory> RequestHistories => Set<RequestHistory>();
    public DbSet<AiRecommendation> AiRecommendations => Set<AiRecommendation>();
    public DbSet<ResourceItem> Resources => Set<ResourceItem>();
    public DbSet<ResourceAllocation> ResourceAllocations => Set<ResourceAllocation>();
    public DbSet<Shelter> Shelters => Set<Shelter>();
    public DbSet<ShelterAllocation> ShelterAllocations => Set<ShelterAllocation>();
    public DbSet<VolunteerProfile> Volunteers => Set<VolunteerProfile>();
    public DbSet<VolunteerTask> VolunteerTasks => Set<VolunteerTask>();
    public DbSet<DonationCampaign> Campaigns => Set<DonationCampaign>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<AuditEvent> Audits => Set<AuditEvent>();
    public DbSet<PublicAlert> PublicAlerts => Set<PublicAlert>();
    public DbSet<HouseholdProfile> Households => Set<HouseholdProfile>();
    public DbSet<AssistanceFeedback> Feedback => Set<AssistanceFeedback>();
    public DbSet<PartnerOffer> PartnerOffers => Set<PartnerOffer>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<DispatchRecord> Dispatches => Set<DispatchRecord>();
    public DbSet<PartnerResourceRequest> PartnerRequests => Set<PartnerResourceRequest>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<DeliveryVerification> DeliveryVerifications => Set<DeliveryVerification>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<HouseholdProfile>().HasIndex(x => x.UserId).IsUnique();
        model.Entity<AssistanceFeedback>().HasIndex(x => x.RequestId).IsUnique();
        model.Entity<PublicAlert>().HasIndex(x => new { x.CrisisId, x.CreatedAt });
        model.Entity<StockMovement>().HasIndex(x => new { x.ResourceId, x.CreatedAt });
        model.Entity<DispatchRecord>().HasIndex(x => new { x.AllocationId, x.DeliveryStatus });
        model.Entity<DeliveryVerification>().HasIndex(x => x.VerificationToken).IsUnique();
        model.Entity<DeliveryVerification>().HasIndex(x => x.ShortCode);
        model.Entity<StockTransfer>().HasIndex(x => new { x.FromOrganisationId, x.ToOrganisationId, x.Status });
        model.Entity<PartnerResourceRequest>().HasIndex(x => new { x.TargetOrganisationId, x.Status });
        model.Entity<UserAccount>().HasIndex(x => x.Email).IsUnique();
        model.Entity<RoleDefinition>().HasIndex(x => x.Name).IsUnique();
        model.Entity<PermissionDefinition>().HasIndex(x => x.Code).IsUnique();
        model.Entity<RolePermission>().HasKey(x => new { x.RoleDefinitionId, x.PermissionDefinitionId });
        model.Entity<RolePermission>().HasOne<RoleDefinition>().WithMany().HasForeignKey(x => x.RoleDefinitionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RolePermission>().HasOne<PermissionDefinition>().WithMany().HasForeignKey(x => x.PermissionDefinitionId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OutboxMessage>().HasIndex(x => x.EventKey).IsUnique();
        model.Entity<AssistanceRequest>().HasIndex(x => new { x.CrisisId, x.Status, x.CreatedAt });
        model.Entity<AiRecommendation>().HasIndex(x => new { x.RequestId, x.CreatedAt });
        model.Entity<RequestHistory>().HasIndex(x => new { x.RequestId, x.At });
        model.Entity<AuditEvent>().HasIndex(x => new { x.EntityType, x.EntityId, x.At });
        model.Entity<Donation>().Property(x => x.Amount).HasPrecision(18, 2);
        model.Entity<DonationCampaign>().Property(x => x.TargetAmount).HasPrecision(18, 2);
        model.Entity<AssistanceRequest>().Property(x => x.Description).HasMaxLength(2000);
        model.Entity<UserAccount>().Property(x => x.Email).HasMaxLength(160);
        model.Entity<OutboxMessage>().Property(x => x.EventKey).HasMaxLength(240);
        // Explicit relational constraints (no cascade delete of humanitarian history).
        model.Entity<UserAccount>().HasOne<RoleDefinition>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<UserAccount>().HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AssistanceRequest>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CitizenId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AssistanceRequest>().HasOne<CrisisEvent>().WithMany().HasForeignKey(x => x.CrisisId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ServiceArea>().HasOne<CrisisEvent>().WithMany().HasForeignKey(x => x.CrisisId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RequestHistory>().HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AiRecommendation>().HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AiRecommendation>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ResourceItem>().HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ResourceAllocation>().HasOne<ResourceItem>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ResourceAllocation>().HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ShelterAllocation>().HasOne<Shelter>().WithMany().HasForeignKey(x => x.ShelterId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<ShelterAllocation>().HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VolunteerProfile>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VolunteerTask>().HasOne<VolunteerProfile>().WithMany().HasForeignKey(x => x.VolunteerId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<VolunteerTask>().HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<DonationCampaign>().HasOne<CrisisEvent>().WithMany().HasForeignKey(x => x.CrisisId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Donation>().HasOne<DonationCampaign>().WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Donation>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.DonorId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Notification>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OutboxMessage>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<AuditEvent>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);

        // Member 03: Resource, Logistics and Partner Coordination
        model.Entity<StockMovement>().HasOne<ResourceItem>().WithMany().HasForeignKey(x => x.ResourceId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<DispatchRecord>().HasOne<ResourceAllocation>().WithMany().HasForeignKey(x => x.AllocationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<DeliveryVerification>().HasOne<DispatchRecord>().WithMany().HasForeignKey(x => x.DispatchId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StockTransfer>().HasOne<Organisation>().WithMany().HasForeignKey(x => x.FromOrganisationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<StockTransfer>().HasOne<Organisation>().WithMany().HasForeignKey(x => x.ToOrganisationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<PartnerResourceRequest>().HasOne<Organisation>().WithMany().HasForeignKey(x => x.TargetOrganisationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<PartnerResourceRequest>().HasOne<Organisation>().WithMany().HasForeignKey(x => x.RequestedByOrganisationId).OnDelete(DeleteBehavior.Restrict);
    }
}
