using CrisisAid.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public class DonationService
{
    private readonly AidDbContext _db;

    public DonationService(AidDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetAvailableFundsAsync(Guid donationId)
    {
        var donation = await _db.Donations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == donationId);

        if (donation == null ||
            donation.Kind != "Monetary" ||
            donation.Status != "Confirmed")
        {
            return 0m;
        }

        var allocatedAmount = await _db.DonationAllocations
            .Where(x => x.DonationId == donationId &&
                        x.Status == "Approved")
            .SumAsync(x => (decimal?)x.Amount) ?? 0m;

        return Math.Max(0m, donation.Amount - allocatedAmount);
    }
}
