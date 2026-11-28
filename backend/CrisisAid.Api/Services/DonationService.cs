using CrisisAid.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public class DonationService(AidDbContext db)
{
    public async Task<decimal> GetAvailableFundsAsync(
        Guid donationId)
    {
        var donation = await db.Donations
            .FindAsync(donationId);

        if (donation == null ||
            donation.Status != "Confirmed" ||
            donation.Kind != "Monetary")
        {
            return 0;
        }

        var allocated = await db.DonationAllocations
            .Where(x =>
                x.DonationId == donationId &&
                x.Status == "Approved")
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        return donation.Amount - allocated;
    }

    public async Task<decimal> GetRemainingAllocationAsync(
        Guid allocationId)
    {
        var allocation = await db.DonationAllocations
            .FindAsync(allocationId);

        if (allocation == null ||
            allocation.Status != "Approved")
        {
            return 0;
        }

        var spent = await db.CampaignExpenditures
            .Where(x => x.AllocationId == allocationId)
            .SumAsync(x => (decimal?)x.Amount) ?? 0;

        return allocation.Amount - spent;
    }
}