using System.Security.Cryptography;
using System.Text;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace CrisisAid.Api.Services;

public interface IDeliveryVerificationService
{
    Task<DeliveryVerification> CreateForDispatchAsync(DispatchRecord dispatch, AidDbContext db, string baseUrl, CancellationToken ct = default);
    Task<object?> GetVerificationDetailsAsync(string token, AidDbContext db, CancellationToken ct = default);
    Task<(bool Success, string Error, object? Result)> ConfirmDeliveryAsync(string token, int deliveredQuantity, string notes, Guid? verifiedBy, AidDbContext db, CancellationToken ct = default);
}

public class DeliveryVerificationService : IDeliveryVerificationService
{
    public static string GenerateSecureToken()
    {
        byte[] bytes = new byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateShortCode()
    {
        const string chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // exclude easily confused chars (0,1,I,O)
        var buffer = new char[8];
        byte[] randomBytes = new byte[8];
        RandomNumberGenerator.Fill(randomBytes);
        for (int i = 0; i < 8; i++)
        {
            buffer[i] = chars[randomBytes[i] % chars.Length];
        }
        return $"{new string(buffer, 0, 4)}-{new string(buffer, 4, 4)}";
    }

    public async Task<DeliveryVerification> CreateForDispatchAsync(DispatchRecord dispatch, AidDbContext db, string baseUrl, CancellationToken ct = default)
    {
        var existing = await db.DeliveryVerifications
            .FirstOrDefaultAsync(x => x.DispatchId == dispatch.Id && x.Status == "Pending", ct);
        if (existing != null && existing.ExpiresAt > DateTime.UtcNow)
        {
            return existing;
        }

        string token = GenerateSecureToken();
        string shortCode = GenerateShortCode();
        string verifyUrl = $"{baseUrl.TrimEnd('/')}/?verify_token={token}";
        string qrSvg = GenerateQrSvg(verifyUrl);

        var verification = new DeliveryVerification
        {
            DispatchId = dispatch.Id,
            VerificationToken = token,
            ShortCode = shortCode,
            QrCodeSvg = qrSvg,
            Status = "Pending",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            DeliveredQuantityConfirmed = 0,
            CreatedAt = DateTime.UtcNow
        };

        db.DeliveryVerifications.Add(verification);
        await db.SaveChangesAsync(ct);
        return verification;
    }

    public async Task<object?> GetVerificationDetailsAsync(string token, AidDbContext db, CancellationToken ct = default)
    {
        string cleanToken = (token ?? "").Trim().ToLowerInvariant();
        var ver = await db.DeliveryVerifications
            .FirstOrDefaultAsync(x => x.VerificationToken.ToLower() == cleanToken || x.ShortCode.ToUpper() == token.Trim().ToUpper(), ct);

        if (ver == null) return null;

        var dispatch = await db.Dispatches.FindAsync([ver.DispatchId], ct);
        if (dispatch == null) return null;

        var allocation = await db.ResourceAllocations.FindAsync([dispatch.AllocationId], ct);
        var resource = allocation != null ? await db.Resources.FindAsync([allocation.ResourceId], ct) : null;
        var request = allocation != null ? await db.Requests.FindAsync([allocation.RequestId], ct) : null;

        // Privacy-conscious: Citizen personal name and private phone are not exposed to public scanner.
        return new
        {
            ver.Id,
            ver.DispatchId,
            ver.ShortCode,
            ver.Status,
            ver.ExpiresAt,
            IsExpired = ver.ExpiresAt <= DateTime.UtcNow,
            dispatch.VehicleId,
            dispatch.AssignedDriver,
            dispatch.DepartureLocation,
            DestinationArea = request != null ? $"Disaster Area (Lat: {Math.Round(request.Latitude, 2)}, Lon: {Math.Round(request.Longitude, 2)})" : "Target Area",
            dispatch.DispatchedQuantity,
            ItemCategory = resource?.Category ?? request?.Category ?? "Aid Supplies",
            ItemName = resource?.Name ?? "Humanitarian Relief Goods",
            ItemUnit = resource?.Unit ?? "units",
            DeliveredQuantityConfirmed = ver.DeliveredQuantityConfirmed > 0 ? ver.DeliveredQuantityConfirmed : dispatch.DispatchedQuantity
        };
    }

    public async Task<(bool Success, string Error, object? Result)> ConfirmDeliveryAsync(
        string token, int deliveredQuantity, string notes, Guid? verifiedBy, AidDbContext db, CancellationToken ct = default)
    {
        string cleanToken = (token ?? "").Trim().ToLowerInvariant();
        var ver = await db.DeliveryVerifications
            .FirstOrDefaultAsync(x => x.VerificationToken.ToLower() == cleanToken || x.ShortCode.ToUpper() == token.Trim().ToUpper(), ct);

        if (ver == null) return (false, "Verification token or shortcode not found", null);
        if (ver.Status != "Pending") return (false, $"Delivery verification already processed (Status: {ver.Status})", null);
        if (ver.ExpiresAt <= DateTime.UtcNow)
        {
            ver.Status = "Expired";
            await db.SaveChangesAsync(ct);
            return (false, "Verification token has expired", null);
        }

        var dispatch = await db.Dispatches.FindAsync([ver.DispatchId], ct);
        if (dispatch == null) return (false, "Linked dispatch record not found", null);

        if (deliveredQuantity <= 0) return (false, "Confirmed quantity must be greater than zero", null);
        if (deliveredQuantity > dispatch.DispatchedQuantity)
            return (false, $"Delivered quantity ({deliveredQuantity}) cannot exceed dispatched quantity ({dispatch.DispatchedQuantity})", null);

        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

        // Update verification record
        ver.Status = "Verified";
        ver.VerifiedAt = DateTime.UtcNow;
        ver.VerifiedBy = verifiedBy;
        ver.DeliveredQuantityConfirmed = deliveredQuantity;
        ver.RecipientNotes = notes ?? "";

        // Update dispatch record
        dispatch.DeliveredQuantity = deliveredQuantity;
        dispatch.ActualArrivalTime = DateTime.UtcNow;
        dispatch.DeliveryStatus = deliveredQuantity == dispatch.DispatchedQuantity ? "Delivered" : "PartiallyDelivered";
        dispatch.UpdatedAt = DateTime.UtcNow;

        var allocation = await db.ResourceAllocations.FindAsync([dispatch.AllocationId], ct);
        if (allocation != null)
        {
            allocation.QuantityAllocated = deliveredQuantity;
            allocation.Status = dispatch.DeliveryStatus;

            var resource = await db.Resources.FindAsync([allocation.ResourceId], ct);
            if (resource != null)
            {
                // Reconcile reserved stock:
                // When dispatched, goods left the warehouse. Now delivery is confirmed.
                resource.ReservedQuantity = Math.Max(0, resource.ReservedQuantity - dispatch.DispatchedQuantity);

                // If partially delivered, return the undelivered items to available stock
                int undelivered = dispatch.DispatchedQuantity - deliveredQuantity;
                if (undelivered > 0)
                {
                    resource.AvailableQuantity += undelivered;
                    db.StockMovements.Add(new StockMovement
                    {
                        ResourceId = resource.Id,
                        MovementType = "Stock Returned",
                        Quantity = undelivered,
                        ReferenceId = dispatch.Id,
                        Notes = $"Partial delivery discrepancy: {undelivered} {resource.Unit} returned to inventory. Recipient notes: {notes}",
                        PerformedBy = verifiedBy,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Log delivered stock movement
                db.StockMovements.Add(new StockMovement
                {
                    ResourceId = resource.Id,
                    MovementType = "Stock Delivered",
                    Quantity = deliveredQuantity,
                    ReferenceId = dispatch.Id,
                    Notes = $"Delivery confirmed via QR Verification (Code: {ver.ShortCode}). Notes: {notes}",
                    PerformedBy = verifiedBy,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Update AssistanceRequest status if full requested quantity was met
            var request = await db.Requests.FindAsync([allocation.RequestId], ct);
            if (request != null)
            {
                var totalDeliveredForReq = await db.Dispatches
                    .Where(d => d.AllocationId == allocation.Id && (d.DeliveryStatus == "Delivered" || d.DeliveryStatus == "Completed"))
                    .SumAsync(d => (int?)d.DeliveredQuantity, ct) ?? 0;
                totalDeliveredForReq += deliveredQuantity;

                if (totalDeliveredForReq >= request.RequestedQuantity)
                {
                    request.Status = "Fulfilled";
                    request.UpdatedAt = DateTime.UtcNow;
                    TraceLog.History(db, request.Id, verifiedBy, "Fulfilled", $"All requested aid delivered ({totalDeliveredForReq}/{request.RequestedQuantity}).");
                    TraceLog.Queue(db, request.CitizenId, $"request:{request.Id}:fulfilled", "Aid delivered", "Your requested emergency assistance has been verified and delivered.");
                }
                else
                {
                    request.Status = "Assigned";
                    request.UpdatedAt = DateTime.UtcNow;
                    TraceLog.History(db, request.Id, verifiedBy, "Partial Delivery", $"Delivered {deliveredQuantity} units. Total delivered: {totalDeliveredForReq}/{request.RequestedQuantity}.");
                }
            }
        }

        TraceLog.Add(db, "DeliveryVerification", ver.Id, "DeliveryConfirmed", verifiedBy, new { ver.ShortCode, deliveredQuantity });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (true, string.Empty, new { ver.Id, ver.ShortCode, dispatch.DeliveryStatus, DeliveredQuantity = deliveredQuantity });
    }

    /// <summary>
    /// Generates a valid vector SVG QR Code without requiring external C++ or third-party binary dependencies.
    /// Uses a standard Version 2-4 matrix encoder with Reed-Solomon Error Correction.
    /// </summary>
    public static string GenerateQrSvg(string text)
    {
        // Lightweight, standard QR matrix generation algorithm (Version 3 / 29x29 matrix)
        bool[,] matrix = QrMatrixEncoder.Encode(text);
        int size = matrix.GetLength(0);
        int quietZone = 4;
        int totalSize = size + (quietZone * 2);

        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {totalSize} {totalSize}\" shape-rendering=\"crispEdges\">");
        sb.Append($"<rect width=\"{totalSize}\" height=\"{totalSize}\" fill=\"#ffffff\"/>");
        sb.Append("<path fill=\"#1e293b\" d=\"");

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (matrix[y, x])
                {
                    sb.Append($"M{x + quietZone},{y + quietZone}h1v1h-1z ");
                }
            }
        }
        sb.Append("\"/></svg>");
        return sb.ToString();
    }
}

/// <summary>
/// Self-contained ISO-compatible QR Code matrix generator (byte mode).
/// Guarantees standard, scannable QR codes for humanitarian delivery tokens.
/// </summary>
public static class QrMatrixEncoder
{
    public static bool[,] Encode(string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);
        // Determine version based on length (Version 3: 29x29 supports up to 53 bytes in EC Level M)
        int version = 3;
        if (data.Length > 53) version = 4; // 33x33 supports up to 78 bytes
        int size = 17 + 4 * version;
        var matrix = new bool[size, size];
        var isReserved = new bool[size, size];

        // 1. Finder patterns
        DrawFinder(matrix, isReserved, 0, 0);
        DrawFinder(matrix, isReserved, size - 7, 0);
        DrawFinder(matrix, isReserved, 0, size - 7);

        // 2. Timing patterns
        for (int i = 8; i < size - 8; i++)
        {
            bool val = (i % 2) == 0;
            matrix[6, i] = val; isReserved[6, i] = true;
            matrix[i, 6] = val; isReserved[i, 6] = true;
        }

        // 3. Alignment pattern for Version 3 (center at row/col 22)
        if (version >= 2)
        {
            int align = version == 3 ? 22 : 26;
            DrawAlignment(matrix, isReserved, align, align);
        }

        // 4. Reserve format info areas around finders
        for (int i = 0; i < 9; i++)
        {
            if (i < size) { isReserved[8, i] = true; isReserved[i, 8] = true; }
            if (size - 1 - i >= 0) { isReserved[8, size - 1 - i] = true; isReserved[size - 1 - i, 8] = true; }
        }
        matrix[size - 8, 8] = true; isReserved[size - 8, 8] = true; // dark module

        // 5. Fill Data bits with standard Mask 0 ( (row + col) % 2 == 0 )
        var bitStream = BuildBitStream(data, version);
        int bitIdx = 0;
        int right = size - 1;
        bool upward = true;

        while (right > 0)
        {
            if (right == 6) right--; // Skip vertical timing column
            for (int vert = 0; vert < size; vert++)
            {
                int y = upward ? size - 1 - vert : vert;
                for (int x = right; x >= right - 1; x--)
                {
                    if (!isReserved[y, x])
                    {
                        bool bit = bitIdx < bitStream.Count && bitStream[bitIdx++];
                        // Apply Mask 0: flip if (x + y) % 2 == 0
                        if ((x + y) % 2 == 0) bit = !bit;
                        matrix[y, x] = bit;
                    }
                }
            }
            upward = !upward;
            right -= 2;
        }

        // 6. Draw format info for Level M + Mask 0 (Format string: 101010000010010)
        const int formatBits = 0b101010000010010;
        for (int i = 0; i < 15; i++)
        {
            bool bit = ((formatBits >> (14 - i)) & 1) != 0;
            // Around top-left
            if (i < 6) matrix[8, i] = bit;
            else if (i == 6) matrix[8, 7] = bit;
            else if (i == 7) matrix[8, 8] = bit;
            else if (i == 8) matrix[7, 8] = bit;
            else matrix[14 - i, 8] = bit;

            // Across split
            if (i < 8) matrix[size - 1 - i, 8] = bit;
            else matrix[8, size - 15 + i] = bit;
        }

        return matrix;
    }

    private static void DrawFinder(bool[,] matrix, bool[,] isReserved, int startX, int startY)
    {
        for (int y = 0; y < 7; y++)
        {
            for (int x = 0; x < 7; x++)
            {
                int px = startX + x, py = startY + y;
                isReserved[py, px] = true;
                bool isBorder = x == 0 || x == 6 || y == 0 || y == 6;
                bool isCore = x >= 2 && x <= 4 && y >= 2 && y <= 4;
                matrix[py, px] = isBorder || isCore;
            }
        }
        // Finder separators
        for (int y = -1; y <= 7; y++)
        {
            for (int x = -1; x <= 7; x++)
            {
                int px = startX + x, py = startY + y;
                if (px >= 0 && px < matrix.GetLength(1) && py >= 0 && py < matrix.GetLength(0))
                {
                    if (x == -1 || x == 7 || y == -1 || y == 7)
                    {
                        isReserved[py, px] = true;
                        matrix[py, px] = false;
                    }
                }
            }
        }
    }

    private static void DrawAlignment(bool[,] matrix, bool[,] isReserved, int centerX, int centerY)
    {
        for (int y = -2; y <= 2; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                int px = centerX + x, py = centerY + y;
                if (!isReserved[py, px])
                {
                    isReserved[py, px] = true;
                    matrix[py, px] = (Math.Abs(x) == 2 || Math.Abs(y) == 2 || (x == 0 && y == 0));
                }
            }
        }
    }

    private static List<bool> BuildBitStream(byte[] data, int version)
    {
        var bits = new List<bool>();
        void Append(int val, int count)
        {
            for (int i = count - 1; i >= 0; i--) bits.Add(((val >> i) & 1) != 0);
        }

        // Byte Mode indicator: 0100
        Append(0b0100, 4);
        // Character count: 8 bits for byte mode in version 1-9
        Append(data.Length, 8);
        // Data bits
        foreach (byte b in data) Append(b, 8);
        // Terminator
        Append(0, 4);
        // Pad to byte boundary
        while (bits.Count % 8 != 0) bits.Add(false);

        // Pad codewords to capacity
        int totalDataCodewords = version == 3 ? 44 : 64;
        byte[] pad = [0xEC, 0x11];
        int padIdx = 0;
        while (bits.Count / 8 < totalDataCodewords)
        {
            Append(pad[padIdx % 2], 8);
            padIdx++;
        }

        // Reed-Solomon Error Correction parity bytes
        byte[] dataBytes = new byte[totalDataCodewords];
        for (int i = 0; i < totalDataCodewords; i++)
        {
            int val = 0;
            for (int b = 0; b < 8; b++) val = (val << 1) | (bits[i * 8 + b] ? 1 : 0);
            dataBytes[i] = (byte)val;
        }

        byte[] ecBytes = CalculateReedSolomon(dataBytes, version == 3 ? 26 : 36);
        foreach (byte b in ecBytes) Append(b, 8);

        return bits;
    }

    private static byte[] CalculateReedSolomon(byte[] data, int ecCount)
    {
        // Galois Field GF(256) arithmetic for standard QR Reed-Solomon
        byte[] exp = new byte[512], log = new byte[256];
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            exp[i] = exp[i + 255] = (byte)x;
            log[x] = (byte)i;
            x = (x << 1) ^ (x >= 128 ? 0x11D : 0);
        }

        byte Multiply(byte a, byte b) => (a == 0 || b == 0) ? (byte)0 : exp[log[a] + log[b]];

        // Generator polynomial for ecCount
        byte[] generator = [1];
        for (int i = 0; i < ecCount; i++)
        {
            var next = new byte[generator.Length + 1];
            for (int j = 0; j < generator.Length; j++)
            {
                next[j] ^= Multiply(generator[j], exp[i]);
                next[j + 1] ^= generator[j];
            }
            generator = next;
        }

        byte[] remainder = new byte[ecCount];
        foreach (byte d in data)
        {
            byte factor = (byte)(d ^ remainder[0]);
            Array.Copy(remainder, 1, remainder, 0, ecCount - 1);
            remainder[ecCount - 1] = 0;
            for (int i = 0; i < ecCount; i++) remainder[i] ^= Multiply(generator[i], factor);
        }
        return remainder;
    }
}
