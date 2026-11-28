using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text;
using CrisisAid.Api.Contracts;
using CrisisAid.Api.Data;
using CrisisAid.Api.Domain;
using CrisisAid.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Set Jwt__Key via environment (minimum 32 characters)");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32) throw new InvalidOperationException("Jwt__Key must be 32 or more bytes");
var connection = builder.Configuration.GetConnectionString("Default") ?? "";
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Set ConnectionStrings__Default for SQL Server");
builder.Services.AddDbContext<AidDbContext>(x => x.UseSqlServer(connection));
builder.Services.AddScoped<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
builder.Services.AddHttpClient("ai", c => c.Timeout = TimeSpan.FromSeconds(6));
builder.Services.AddHttpClient("google-oauth", c => c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddHttpClient("map", c => { c.Timeout = TimeSpan.FromSeconds(5); c.DefaultRequestHeaders.UserAgent.ParseAdd("CrisisAidStudentPrototype/1.0 (coursework demo)"); });
builder.Services.AddScoped<IAiDecisionService, AiDecisionService>();
builder.Services.AddSingleton<IShelterRecommendationService, ShelterRecommendationService>();
builder.Services.AddScoped<IRequestRepository, EfRequestRepository>();
builder.Services.AddSingleton<IResourceMatchStrategyFactory, ResourceMatchStrategyFactory>();
builder.Services.AddRateLimiter(opts => {
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", k => new FixedWindowRateLimiterOptions {
        PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    opts.AddPolicy("geocode", _ => RateLimitPartition.GetSlidingWindowLimiter("nominatim-provider", _ => new SlidingWindowRateLimiterOptions {
        PermitLimit = 1, Window = TimeSpan.FromSeconds(1), SegmentsPerWindow = 4, QueueLimit = 0, AutoReplenishment = true }));
});
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<VolunteerSafetyWorker>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "CrisisAid",
        ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"] ?? "CrisisAid.Web",
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromSeconds(20)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCors(o => o.AddPolicy("web", p =>
{
    var origins = (builder.Configuration["Cors:Origins"] ?? "http://localhost:5173").Split(',', StringSplitOptions.RemoveEmptyEntries);
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o => {
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "CrisisAid Humanitarian API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement { {
        new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});
var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async ctx => {
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { error = "Unexpected server error; see server log" });
}));
app.UseRouting();
app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AidDbContext>();
    await database.Database.EnsureCreatedAsync();
    await ExtendedModules.EnsureSchemaAsync(database);
    await SeedData.InitialiseAsync(database, app.Configuration);
}

app.MapGet("/api/health", () => Results.Ok(new { service = "CrisisAid", status = "ok", utc = DateTime.UtcNow })).AllowAnonymous();
GoogleOAuth.Map(app);

app.MapPost("/api/auth/register", async (RegisterDto input, AidDbContext db, IPasswordHasher<UserAccount> hasher) => {
    if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 120 ||
        !input.Email.Contains('@') || input.Password.Length < 10)
        return Results.BadRequest(new { error = "Name, valid email and password of at least 10 characters required" });
    var role = input.Role is Roles.Citizen or Roles.Volunteer or Roles.Donor ? input.Role : Roles.Citizen;
    var email = input.Email.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(x => x.Email == email)) return Results.Conflict(new { error = "Email already registered" });
    var roleDefinition = await db.RoleDefinitions.FirstOrDefaultAsync(r => r.Name == role);
    var user = new UserAccount { DisplayName = input.Name.Trim(), Email = email, Role = role,
        RoleId = roleDefinition?.Id };
    user.PasswordHash = hasher.HashPassword(user, input.Password);
    db.Users.Add(user);
    if (role == Roles.Volunteer) db.Volunteers.Add(new VolunteerProfile { UserId = user.Id });
    TraceLog.Add(db, "User", user.Id, "Registered", user.Id);
    await db.SaveChangesAsync();
    return Results.Created($"/api/users/{user.Id}", new { user.Id, user.Email, user.Role });
}).AllowAnonymous().RequireRateLimiting("auth");

app.MapPost("/api/auth/login", async (LoginDto input, AidDbContext db, IPasswordHasher<UserAccount> hasher, IConfiguration config) => {
    var email = input.Email.Trim().ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email && x.IsActive);
    if (user == null || user.PasswordHash == "GOOGLE_ONLY" || hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed)
        return Results.Unauthorized();
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.DisplayName), new(ClaimTypes.Email, user.Email), new(ClaimTypes.Role, user.Role) };
    var expires = DateTime.UtcNow.AddHours(6);
    var jwt = new JwtSecurityToken(config["Jwt:Issuer"] ?? "CrisisAid", config["Jwt:Audience"] ?? "CrisisAid.Web",
        claims, expires: expires, signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt), expires, user = new { user.Id, user.DisplayName, user.Email, user.Role } });
}).AllowAnonymous().RequireRateLimiting("auth");

var api = app.MapGroup("/api").RequireAuthorization();
ExtendedModules.Map(api);
OperationsEndpoints.Map(api);
api.MapGet("/me", async (HttpContext ctx, AidDbContext db) => {
    var id = Access.UserId(ctx.User);
    return await db.Users.Where(x => x.Id == id).Select(x => new { x.Id, x.DisplayName, x.Email, x.Role, x.OrganisationId }).FirstOrDefaultAsync() is { } me
        ? Results.Ok(me) : Results.NotFound();
});
api.MapGet("/crises", async (AidDbContext db) => await db.Crises.OrderByDescending(c => c.StartedAt).ToListAsync());
api.MapPost("/crises", async (CrisisDto dto, HttpContext ctx, AidDbContext db) => {
    if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Area)) return Results.BadRequest(new { error = "Name and area required" });
    var entity = new CrisisEvent { Name = dto.Name, Type = dto.Type, Country = dto.Country, Area = dto.Area };
    db.Crises.Add(entity); TraceLog.Add(db, "Crisis", entity.Id, "Created", Access.UserId(ctx.User));
    await db.SaveChangesAsync(); return Results.Created($"/api/crises/{entity.Id}", entity);
}).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Admin));

var requests = api.MapGroup("/requests");
requests.MapGet("/", async (HttpContext ctx, IRequestRepository repo, string? status, Guid? crisisId, CancellationToken ct) =>
    await repo.ListVisibleAsync(Access.UserId(ctx.User), Access.Staff(ctx.User), status, crisisId, ct));
requests.MapPost("/", async (AssistanceDto dto, HttpContext ctx, AidDbContext db) => {
    if (!Access.Coordinates(dto.Latitude, dto.Longitude) || dto.PeopleAffected < 1 || dto.RequestedQuantity < 1 ||
        string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 2000 ||
        !new[] { "Low", "Medium", "High", "Critical" }.Contains(dto.Urgency) ||
        (dto.EvidenceUrl is not null && (!Uri.TryCreate(dto.EvidenceUrl, UriKind.Absolute, out var u) || u.Scheme != "https")))
        return Results.BadRequest(new { error = "Invalid coordinates, people, quantity, urgency, description or evidence URL" });
    if (!await db.Crises.AnyAsync(c => c.Id == dto.CrisisId && c.Status == "Active")) return Results.BadRequest(new { error = "Active crisis required" });
    var req = new AssistanceRequest { CitizenId = Access.UserId(ctx.User), CrisisId = dto.CrisisId,
        Category = dto.Category, Description = dto.Description, Urgency = dto.Urgency,
        PeopleAffected = dto.PeopleAffected, RequestedQuantity = dto.RequestedQuantity,
        MedicalEmergency = dto.MedicalEmergency, AccessibilityNeeded = dto.AccessibilityNeeded,
        Latitude = dto.Latitude, Longitude = dto.Longitude, EvidenceUrl = dto.EvidenceUrl };
    db.Requests.Add(req);
    TraceLog.History(db, req.Id, req.CitizenId, "Submitted", "Request submitted for screening");
    TraceLog.Add(db, "AssistanceRequest", req.Id, "Submitted", req.CitizenId, new { req.Category, req.Urgency });
    await db.SaveChangesAsync();
    return Results.Created($"/api/requests/{req.Id}", new { req.Id, req.Status, message = "Submitted. An authorised coordinator will review any AI assessment." });
});
requests.MapGet("/{id:guid}", async (Guid id, HttpContext ctx, AidDbContext db) => {
    var req = await db.Requests.FindAsync(id);
    if (req == null) return Results.NotFound();
    if (!Access.CanRead(ctx.User, req)) return Results.Forbid();
    var history = await db.RequestHistories.Where(x => x.RequestId == id).OrderByDescending(x => x.At).ToListAsync();
    var recommendations = await db.AiRecommendations.Where(x => x.RequestId == id).OrderByDescending(x => x.CreatedAt).ToListAsync();
    return Results.Ok(new { request = req, history, recommendations });
});
requests.MapPost("/{id:guid}/assess", async (Guid id, HttpContext ctx, AidDbContext db, IAiDecisionService ai, CancellationToken ct) => {
    var req = await db.Requests.FindAsync([id], ct);
    if (req == null) return Results.NotFound();
    var result = await ai.EvaluateAsync(req, ct);
    var duplicateCandidates = await db.Requests.Where(r => r.Id != id && r.CrisisId == req.CrisisId &&
        r.Category == req.Category && r.Status != "Closed").OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync(ct);
    var duplicate = duplicateCandidates.FirstOrDefault(x =>
        (x.CitizenId == req.CitizenId && x.Description.Trim().Equals(req.Description.Trim(), StringComparison.OrdinalIgnoreCase)) ||
        (Distances.Km(x.Latitude, x.Longitude, req.Latitude, req.Longitude) < 0.3 &&
         x.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Intersect(req.Description.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase).Count() >= 4));
    var rec = new AiRecommendation { RequestId = id, RecommendedCategory = result.Category,
        Priority = result.Priority, Confidence = result.Confidence, Rationale = result.Rationale,
        Model = result.Model, PossibleDuplicateId = duplicate?.Id.ToString() };
    db.AiRecommendations.Add(rec);
    req.Status = "PendingReview"; req.UpdatedAt = DateTime.UtcNow;
    TraceLog.Add(db, "AiRecommendation", rec.Id, "Generated", Access.UserId(ctx.User),
        new { rec.Priority, rec.Confidence, rec.Model, rec.Rationale, rec.PossibleDuplicateId });
    TraceLog.History(db, id, Access.UserId(ctx.User), "AI assessed", "Recommendation awaiting human approval");
    await db.SaveChangesAsync(ct);
    return Results.Ok(rec);
}).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Admin));
requests.MapPost("/{id:guid}/review", async (Guid id, ReviewDto dto, HttpContext ctx, AidDbContext db) => {
    if (!new[] { "Approved", "Modified", "Rejected", "Deferred" }.Contains(dto.Decision) ||
        (dto.Decision == "Modified" && (dto.FinalPriority is null or < 1 or > 100)) ||
        ((dto.Decision == "Modified" || dto.Decision == "Rejected") && string.IsNullOrWhiteSpace(dto.Reason)) ||
        (dto.FinalCategory is not null && !new[] { "Food", "Water", "Medicine", "Shelter", "Transport", "Accessibility", "Other" }.Contains(dto.FinalCategory)))
        return Results.BadRequest(new { error = "Invalid decision/priority; reason required for modification or rejection" });
    var req = await db.Requests.FindAsync(id); if (req == null) return Results.NotFound();
    var rec = await db.AiRecommendations.Where(x => x.RequestId == id && x.ReviewStatus == "Pending")
        .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync();
    if (rec == null) return Results.Conflict(new { error = "No pending AI recommendation to review" });
    var actor = Access.UserId(ctx.User); var originalCategory = req.Category;
    rec.ReviewStatus = dto.Decision; rec.ReviewedBy = actor; rec.ReviewedAt = DateTime.UtcNow;
    rec.FinalPriority = dto.Decision == "Modified" ? dto.FinalPriority : dto.Decision == "Approved" ? rec.Priority : null;
    rec.HumanReason = dto.Reason;
    req.Status = dto.Decision is "Approved" or "Modified" ? "Verified" : "PendingReview";
    if (req.Status == "Verified") req.Category = dto.Decision == "Modified" && dto.FinalCategory is not null
        ? dto.FinalCategory : rec.RecommendedCategory;
    req.UpdatedAt = DateTime.UtcNow;
    TraceLog.Add(db, "AiRecommendation", rec.Id, "HumanReview", actor,
        new { Original = rec.Priority, OriginalCategory = originalCategory, ProposedCategory = rec.RecommendedCategory,
            rec.Confidence, dto.Decision, rec.FinalPriority, FinalCategory = req.Category, dto.Reason });
    TraceLog.History(db, id, actor, "Human review: " + dto.Decision, dto.Reason);
    await db.SaveChangesAsync(); return Results.Ok(rec);
}).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Admin));
requests.MapPatch("/{id:guid}/status", async (Guid id, RequestStatusDto dto, HttpContext ctx, AidDbContext db) => {
    var req = await db.Requests.FindAsync(id); if (req == null) return Results.NotFound();
    var transitions = new Dictionary<string, string[]> {
        ["Verified"] = ["Assigned", "Escalated", "Closed"],
        ["Assigned"] = ["Dispatched", "Escalated"], ["Dispatched"] = ["Fulfilled", "Escalated"],
        ["Fulfilled"] = ["Closed", "Escalated"], ["Escalated"] = ["Verified", "Assigned", "Closed"] };
    if (!transitions.TryGetValue(req.Status, out var allowed) || !allowed.Contains(dto.Status))
        return Results.Conflict(new { error = $"Cannot move from {req.Status} to {dto.Status}" });
    var actor = Access.UserId(ctx.User); var previous = req.Status;
    req.Status = dto.Status; req.UpdatedAt = DateTime.UtcNow;
    var shelterSupply = await db.ShelterSupplyRequests.SingleOrDefaultAsync(x => x.AssistanceRequestId == req.Id);
    if (shelterSupply != null)
    {
        shelterSupply.Status = dto.Status switch
        {
            "Dispatched" => "Dispatched",
            "Fulfilled" => "Delivered",
            "Closed" => "Cancelled",
            _ => shelterSupply.Status
        };
        shelterSupply.UpdatedAt = DateTime.UtcNow;
        if (shelterSupply.Status != "Approved")
            TraceLog.Add(db, "ShelterSupplyRequest", shelterSupply.Id, shelterSupply.Status, actor,
                new { AssistanceRequestId = req.Id });
    }
    TraceLog.History(db, req.Id, actor, "Status -> " + dto.Status, dto.Notes);
    TraceLog.Add(db, "AssistanceRequest", id, "StatusChanged", actor, new { previous, dto.Status, dto.Notes });
    if (dto.Status == "Fulfilled") TraceLog.Queue(db, req.CitizenId, $"request:{id}:fulfilled", "Assistance delivered", "Your assistance request has been marked fulfilled.");
    await db.SaveChangesAsync(); return Results.Ok(req);
}).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Admin));
requests.MapGet("/{id:guid}/resource-matches", async (Guid id, bool? allowPartial, AidDbContext db, IResourceMatchStrategyFactory strategies) => {
    var req = await db.Requests.FindAsync(id); if (req == null) return Results.NotFound();
    return Results.Ok(strategies.Create(allowPartial ?? true).Match(req, await db.Resources.ToListAsync()));
}).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Logistics, Roles.Admin));

var resources = api.MapGroup("/resources");
resources.MapGet("/", async (AidDbContext db) => await db.Resources.AsNoTracking().OrderBy(x => x.Category).ToListAsync());
resources.MapPost("/", async (ResourceDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.AvailableQuantity < 0 || string.IsNullOrWhiteSpace(dto.Name) || !Access.Coordinates(dto.Latitude, dto.Longitude)) return Results.BadRequest();
    var item = new ResourceItem { Name = dto.Name, Category = dto.Category, Unit = dto.Unit, Area = dto.Area,
        AvailableQuantity = dto.AvailableQuantity, Accessible = dto.Accessible, ExpiresAt = dto.ExpiresAt,
        Latitude = dto.Latitude, Longitude = dto.Longitude };
    db.Resources.Add(item); TraceLog.Add(db, "Resource", item.Id, "Created", Access.UserId(ctx.User));
    await db.SaveChangesAsync(); return Results.Created($"/api/resources/{item.Id}", item);
}).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));
resources.MapPost("/allocate", async (AllocateResourceDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.Quantity < 1) return Results.BadRequest(new { error = "Quantity must be positive" });
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var request = await db.Requests.FindAsync(dto.RequestId); var resource = await db.Resources.FindAsync(dto.ResourceId);
    if (request == null || resource == null) return Results.NotFound();
    if (request.Status is not ("Verified" or "Assigned")) return Results.Conflict(new { error = "Human approval required before allocation" });
    if (!resource.Category.Equals(request.Category, StringComparison.OrdinalIgnoreCase) ||
        (request.AccessibilityNeeded && !resource.Accessible) || (resource.ExpiresAt <= DateTime.UtcNow))
        return Results.Conflict(new { error = "Category/accessibility/expiry constraint failed" });
    if (resource.AvailableQuantity < dto.Quantity) return Results.Conflict(new { error = "Shortage: insufficient stock; try partial allocation or another resource" });
    var reserved = await db.ResourceAllocations.Where(x => x.RequestId == request.Id && x.Status != "Cancelled").SumAsync(x => (int?)x.Quantity) ?? 0;
    if (reserved + dto.Quantity > request.RequestedQuantity) return Results.Conflict(new { error = "Requested quantity would be exceeded" });
    resource.AvailableQuantity -= dto.Quantity; request.Status = "Assigned"; request.UpdatedAt = DateTime.UtcNow;
    var actor = Access.UserId(ctx.User);
    var allocation = new ResourceAllocation { RequestId = request.Id, ResourceId = resource.Id, Quantity = dto.Quantity, ApprovedBy = actor };
    db.ResourceAllocations.Add(allocation); TraceLog.History(db, request.Id, actor, "Resources reserved", $"{dto.Quantity} {resource.Unit}: {resource.Name}");
    TraceLog.Add(db, "ResourceAllocation", allocation.Id, "Reserved", actor, new { dto.RequestId, dto.ResourceId, dto.Quantity });
    TraceLog.Queue(db, request.CitizenId, $"allocation:{allocation.Id}", "Aid allocated", $"{dto.Quantity} {resource.Unit} have been allocated to your request.");
    await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(allocation);
}).RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));
resources.MapGet("/allocations", async (AidDbContext db) => await db.ResourceAllocations.AsNoTracking().OrderByDescending(x => x.At).Take(200).ToListAsync())
    .RequireAuthorization(p => p.RequireRole(Roles.Logistics, Roles.Coordinator, Roles.Admin));

var shelters = api.MapGroup("/shelters");
shelters.MapGet("/", async (AidDbContext db) => await db.Shelters.AsNoTracking().ToListAsync());
shelters.MapPost("/", async (ShelterDto dto, HttpContext ctx, AidDbContext db) => {
    if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Area) ||
        dto.Capacity < 1 || !Access.Coordinates(dto.Latitude, dto.Longitude)) return Results.BadRequest();
    var shelter = new Shelter { Name = dto.Name, Area = dto.Area, Capacity = dto.Capacity,
        Accessible = dto.Accessible, Facilities = dto.Facilities, Latitude = dto.Latitude, Longitude = dto.Longitude,
        ContactName = dto.ContactName, ContactPhone = dto.ContactPhone, ContactEmail = dto.ContactEmail,
        OpeningHours = dto.OpeningHours, MedicalFacilities = dto.MedicalFacilities,
        SanitationFacilities = dto.SanitationFacilities,         FoodAvailable = dto.FoodAvailable,
        AdministratorId = Access.Role(ctx.User) == Roles.ShelterAdmin ? Access.UserId(ctx.User) : null };
    db.Shelters.Add(shelter); TraceLog.Add(db, "Shelter", shelter.Id, "Created", Access.UserId(ctx.User));
    await db.SaveChangesAsync(); return Results.Created($"/api/shelters/{shelter.Id}", shelter);
}).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin));
shelters.MapPost("/allocate", async (AllocateShelterDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.PeopleCount < 1) return Results.BadRequest();
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var shelter = await ShelterConcurrencyLock.AcquireAsync(db, dto.ShelterId);
    var req = await db.Requests.FindAsync(dto.RequestId);
    if (req == null || shelter == null) return Results.NotFound();
    if (Access.Role(ctx.User) == Roles.ShelterAdmin && shelter.AdministratorId != null &&
        shelter.AdministratorId != Access.UserId(ctx.User)) return Results.Forbid();
    if (req.Status != "Verified" && req.Status != "Assigned") return Results.Conflict(new { error = "Human approval required" });
    var reserved = await db.ShelterAllocations.Where(x => x.ShelterId == shelter.Id && x.Status == "Reserved")
        .SumAsync(x => (int?)x.PeopleCount) ?? 0;
    if (req.Category != "Shelter" || shelter.Status != "Open" || (req.AccessibilityNeeded && !shelter.Accessible) ||
        dto.PeopleCount > req.PeopleAffected ||
        !ShelterCapacity.CanReserve(shelter.Capacity, shelter.Occupancy, reserved, dto.PeopleCount))
        return Results.Conflict(new { error = "Capacity, suitability or request constraint failed" });
    if (await db.ShelterAllocations.AnyAsync(x => x.RequestId == req.Id)) return Results.Conflict(new { error = "Shelter already allocated to request" });
    req.Status = "Assigned"; req.UpdatedAt = DateTime.UtcNow;
    var actor = Access.UserId(ctx.User);
    var allocation = new ShelterAllocation { ShelterId = shelter.Id, RequestId = req.Id, PeopleCount = dto.PeopleCount,
        ApprovedBy = actor, Status = "Reserved" };
    db.ShelterAllocations.Add(allocation);
    TraceLog.Add(db, "ShelterAllocation", allocation.Id, "Reserved", actor, new { dto.PeopleCount, dto.ShelterId });
    TraceLog.History(db, req.Id, actor, "Shelter assigned", shelter.Name);
    TraceLog.Queue(db, req.CitizenId, $"shelter:{allocation.Id}", "Shelter assigned", $"Your household was assigned to {shelter.Name}.");
    await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(allocation);
}).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin));
shelters.MapGet("/allocations", async (HttpContext ctx, AidDbContext db) => {
    var query = db.ShelterAllocations.AsNoTracking().AsQueryable();
    if (Access.Role(ctx.User) == Roles.ShelterAdmin)
    {
        var actorId = Access.UserId(ctx.User);
        query = query.Where(x => db.Shelters.Any(s => s.Id == x.ShelterId &&
            (s.AdministratorId == null || s.AdministratorId == actorId)));
    }
    return await query.ToListAsync();
})
    .RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin));

var volunteers = api.MapGroup("/volunteers");
volunteers.MapGet("/", async (AidDbContext db) => await db.Volunteers.AsNoTracking().ToListAsync())
    .RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin));
volunteers.MapPut("/me", async (VolunteerProfileDto dto, HttpContext ctx, AidDbContext db) => {
    if (Access.Role(ctx.User) != Roles.Volunteer) return Results.Forbid();
    if (string.IsNullOrWhiteSpace(dto.Skills) || dto.Skills.Length > 500 ||
        string.IsNullOrWhiteSpace(dto.Area) || dto.Area.Length > 160)
        return Results.BadRequest(new { error = "Skills and operating area are required and must be within limits" });
    if ((dto.Latitude.HasValue != dto.Longitude.HasValue) ||
        (dto.Latitude.HasValue && !Access.Coordinates(dto.Latitude.Value, dto.Longitude!.Value)))
        return Results.BadRequest(new { error = "Provide valid latitude and longitude together" });
    var id = Access.UserId(ctx.User);
    var profile = await db.Volunteers.SingleOrDefaultAsync(v => v.UserId == id);
    if (profile == null) { profile = new VolunteerProfile { UserId = id }; db.Volunteers.Add(profile); }
    profile.Skills = dto.Skills; profile.Area = dto.Area; profile.Available = dto.Available;
    profile.Latitude = dto.Latitude; profile.Longitude = dto.Longitude;
    var skills = dto.Skills.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => x.Trim()).Where(x => x.Length <= 80).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var previousSkills = await db.VolunteerSkills.Where(x => x.VolunteerId == profile.Id).ToListAsync();
    var retainedNames = new HashSet<string>(skills, StringComparer.OrdinalIgnoreCase);
    db.VolunteerSkills.RemoveRange(previousSkills.Where(x => !retainedNames.Contains(x.Name)));
    var existingNames = new HashSet<string>(previousSkills.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
    db.VolunteerSkills.AddRange(skills.Where(name => !existingNames.Contains(name))
        .Select(name => new VolunteerSkill { VolunteerId = profile.Id, Name = name }));
    TraceLog.Add(db, "Volunteer", profile.Id, "ProfileUpdated", id);
    await db.SaveChangesAsync(); return Results.Ok(profile);
});
volunteers.MapPatch("/{id:guid}/verify", async (Guid id, VerifyVolunteerDto dto, HttpContext ctx, AidDbContext db) => {
    var v = await db.Volunteers.FindAsync(id); if (v == null) return Results.NotFound();
    v.Verified = dto.Verified; TraceLog.Add(db, "Volunteer", id, "VerificationUpdated", Access.UserId(ctx.User), dto);
    await db.SaveChangesAsync(); return Results.Ok(v);
}).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin));
volunteers.MapPost("/tasks", async (AssignVolunteerDto dto, HttpContext ctx, AidDbContext db) => {
    if (string.IsNullOrWhiteSpace(dto.RequiredSkill) || dto.RequiredSkill.Length > 80 ||
        string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 2000 ||
        !new[] { "Low", "Medium", "High", "Critical" }.Contains(dto.Urgency) ||
        dto.DurationMinutes is <= 0 or > 1440 || dto.CheckInIntervalMinutes is < 1 or > 1440 ||
        (dto.ScheduledStartAt.HasValue != dto.DurationMinutes.HasValue) ||
        (dto.Latitude.HasValue != dto.Longitude.HasValue) ||
        (dto.Latitude.HasValue && !Access.Coordinates(dto.Latitude.Value, dto.Longitude!.Value)))
        return Results.BadRequest(new { error = "Invalid task details or schedule" });
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var v = await db.Volunteers.FindAsync(dto.VolunteerId);
    if (v == null) return Results.NotFound();
    var skillRows = await db.VolunteerSkills.Where(x => x.VolunteerId == v.Id).ToListAsync();
    var hasSkill = VolunteerSkillMatcher.HasVerifiedSkill(v, skillRows, dto.RequiredSkill);
    if (!v.Verified || !v.Available || !hasSkill)
        return Results.Conflict(new { error = "Volunteer verification, availability or required skill constraint failed" });
    if (dto.ScheduledStartAt is DateTimeOffset start)
    {
        var startUtc = start.UtcDateTime;
        if (startUtc <= DateTime.UtcNow) return Results.BadRequest(new { error = "Task schedule must be in the future" });
        var endUtc = startUtc.AddMinutes(dto.DurationMinutes!.Value);
        var availability = await db.VolunteerAvailabilities.AnyAsync(x => x.VolunteerId == v.Id &&
            x.StartsAt <= startUtc && x.EndsAt >= endUtc);
        var existingTasks = await db.VolunteerTasks.Where(x => x.VolunteerId == v.Id)
            .ToListAsync();
        var overlapping = VolunteerSchedule.Overlaps(startUtc, dto.DurationMinutes!.Value, existingTasks);
        if (!availability || overlapping)
            return Results.Conflict(new { error = "Volunteer availability or schedule conflict" });
    }
    else if (await db.VolunteerTasks.AnyAsync(x => x.VolunteerId == v.Id &&
        x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Declined"))
        return Results.Conflict(new { error = "Volunteer already has an active assignment" });
    if (dto.RequestId is Guid reqid && !await db.Requests.AnyAsync(r => r.Id == reqid && (r.Status == "Verified" || r.Status == "Assigned")))
        return Results.Conflict(new { error = "Request not approved" });
    var task = new VolunteerTask { VolunteerId = v.Id, RequestId = dto.RequestId, RequiredSkill = dto.RequiredSkill,
        Description = dto.Description, AssignedBy = Access.UserId(ctx.User), ScheduledStartAt = dto.ScheduledStartAt?.UtcDateTime,
        DurationMinutes = dto.DurationMinutes, Urgency = dto.Urgency, Latitude = dto.Latitude, Longitude = dto.Longitude,
        CheckInIntervalMinutes = dto.CheckInIntervalMinutes };
    db.VolunteerTasks.Add(task);
    if (!dto.ScheduledStartAt.HasValue) v.Available = false;
    TraceLog.Add(db, "VolunteerTask", task.Id, "Assigned", Access.UserId(ctx.User));
    TraceLog.Queue(db, v.UserId, $"volunteer:{task.Id}", "Volunteer task assigned", dto.Description);
    await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(task);
}).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin));
volunteers.MapGet("/tasks", async (HttpContext ctx, AidDbContext db) => {
    if (Access.Role(ctx.User) is Roles.VolunteerCoordinator or Roles.Coordinator or Roles.Admin)
        return Results.Ok(await db.VolunteerTasks.OrderByDescending(x => x.At).ToListAsync());
    if (Access.Role(ctx.User) != Roles.Volunteer) return Results.Forbid();
    var id = Access.UserId(ctx.User);
    var volunteerIds = db.Volunteers.Where(x => x.UserId == id).Select(x => x.Id);
    return Results.Ok(await db.VolunteerTasks.Where(x => volunteerIds.Contains(x.VolunteerId)).ToListAsync());
});
volunteers.MapPatch("/tasks/{id:guid}", async (Guid id, TaskStatusDto dto, HttpContext ctx, AidDbContext db) => {
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var task = await db.VolunteerTasks.FindAsync(id); if (task == null) return Results.NotFound();
    var profile = await db.Volunteers.FindAsync(task.VolunteerId); if (profile == null) return Results.NotFound();
    var actor = Access.UserId(ctx.User);
    var coordinator = Access.Role(ctx.User) is Roles.VolunteerCoordinator or Roles.Coordinator or Roles.Admin;
    if (!coordinator && profile.UserId != actor) return Results.Forbid();
    if (dto.VerifyCompletion)
    {
        if (!coordinator || task.Status != "Completed") return Results.Forbid();
        task.CoordinatorVerified = true; task.VerifiedBy = actor; task.VerifiedAt = DateTime.UtcNow;
        db.VolunteerActivityLogs.Add(new VolunteerActivityLog { VolunteerId = profile.Id, TaskId = task.Id,
            ActorId = actor, Activity = "CoordinatorVerified", Notes = dto.Notes });
    }
    else if (dto.Status == "Reassigned")
    {
        if (!coordinator || dto.ReassignToVolunteerId is not Guid replacementId) return Results.Forbid();
        var replacement = await db.Volunteers.FindAsync(replacementId);
        if (replacement == null || replacement.Id == profile.Id || !replacement.Verified || !replacement.Available)
            return Results.Conflict(new { error = "Replacement volunteer is not eligible" });
        var replacementSkills = await db.VolunteerSkills.Where(x => x.VolunteerId == replacement.Id).ToListAsync();
        if (!VolunteerSkillMatcher.HasVerifiedSkill(replacement, replacementSkills, task.RequiredSkill))
            return Results.Conflict(new { error = "Replacement volunteer does not have the required skill" });
        var replacementTasks = await db.VolunteerTasks.Where(x => x.VolunteerId == replacement.Id && x.Id != task.Id)
            .ToListAsync();
        if (task.ScheduledStartAt is DateTime scheduledStart && task.DurationMinutes is int duration)
        {
            var available = await db.VolunteerAvailabilities.AnyAsync(x => x.VolunteerId == replacement.Id &&
                x.StartsAt <= scheduledStart && x.EndsAt >= scheduledStart.AddMinutes(duration));
            if (!available || VolunteerSchedule.Overlaps(scheduledStart, duration, replacementTasks))
                return Results.Conflict(new { error = "Replacement volunteer is unavailable or has an overlapping task" });
        }
        else if (replacementTasks.Any(x => x.Status is not ("Completed" or "Cancelled" or "Declined")))
            return Results.Conflict(new { error = "Replacement volunteer already has an active assignment" });
        profile.Available = !await db.VolunteerTasks.AnyAsync(x => x.VolunteerId == profile.Id && x.Id != task.Id &&
            x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Declined");
        task.VolunteerId = replacement.Id; task.Status = "Reassigned"; task.AssignedBy = actor;
        task.NextCheckInDueAt = null; task.OverdueAlertSentAt = null;
        if (!task.ScheduledStartAt.HasValue) replacement.Available = false;
        db.VolunteerActivityLogs.Add(new VolunteerActivityLog { VolunteerId = profile.Id, TaskId = task.Id,
            ActorId = actor, Activity = "Reassigned", Notes = dto.Reason });
        db.VolunteerActivityLogs.Add(new VolunteerActivityLog { VolunteerId = replacement.Id, TaskId = task.Id,
            ActorId = actor, Activity = "Assigned", Notes = dto.Reason });
        TraceLog.Queue(db, replacement.UserId, $"volunteer:{task.Id}:reassigned", "Volunteer task reassigned", task.Description);
    }
    else if (dto.Status == "Cancelled")
    {
        if (!coordinator || task.Status is "Completed" or "Cancelled") return Results.Forbid();
        task.Status = "Cancelled"; task.NextCheckInDueAt = null;
        profile.Available = !await db.VolunteerTasks.AnyAsync(x => x.VolunteerId == profile.Id && x.Id != task.Id &&
            x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Declined");
        db.VolunteerActivityLogs.Add(new VolunteerActivityLog { VolunteerId = profile.Id, TaskId = task.Id,
            ActorId = actor, Activity = "Cancelled", Notes = dto.Reason });
    }
    else
    {
        var validTransition = ((task.Status is "Assigned" or "Reassigned") && (dto.Status is "Accepted" or "Declined")) ||
            (task.Status == "Accepted" && (dto.Status is "In Progress" or "Completed")) ||
            (task.Status == "In Progress" && dto.Status == "Completed");
        if (!validTransition || coordinator) return Results.Conflict(new { error = "Invalid task state transition" });
        if (dto.Status == "Declined" && string.IsNullOrWhiteSpace(dto.Reason))
            return Results.BadRequest(new { error = "A reason is required to decline a task" });
        if (dto.HoursWorked is < 0 or > 24) return Results.BadRequest(new { error = "Hours worked must be between 0 and 24" });
        task.Status = dto.Status;
        if (dto.Status == "Declined")
        {
            task.DeclineReason = dto.Reason; task.NextCheckInDueAt = null;
            profile.Available = !await db.VolunteerTasks.AnyAsync(x => x.VolunteerId == profile.Id && x.Id != task.Id &&
                x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Declined");
        }
        if (dto.Status is "Accepted" or "In Progress")
            task.NextCheckInDueAt = DateTime.UtcNow.AddMinutes(task.CheckInIntervalMinutes);
        if (dto.Status == "Completed")
        {
            task.CompletedAt = DateTime.UtcNow; task.HoursWorked = dto.HoursWorked;
            task.CompletionNotes = dto.Notes?.Trim() ?? ""; task.NextCheckInDueAt = null;
            profile.Available = !await db.VolunteerTasks.AnyAsync(x => x.VolunteerId == profile.Id && x.Id != task.Id &&
                x.Status != "Completed" && x.Status != "Cancelled" && x.Status != "Declined");
        }
        db.VolunteerActivityLogs.Add(new VolunteerActivityLog { VolunteerId = profile.Id, TaskId = task.Id,
            ActorId = actor, Activity = dto.Status, Notes = dto.Status == "Declined" ? dto.Reason : dto.Notes?.Trim() ?? "",
            HoursWorked = dto.Status == "Completed" ? dto.HoursWorked : null });
    }
    TraceLog.Add(db, "VolunteerTask", id, dto.VerifyCompletion ? "CoordinatorVerified" : dto.Status, actor, dto);
    await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(task);
});

api.MapGet("/campaigns", async (AidDbContext db) => await db.Campaigns.ToListAsync());
api.MapPost("/campaigns", async (CampaignDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.TargetAmount <= 0 || !await db.Crises.AnyAsync(c => c.Id == dto.CrisisId)) return Results.BadRequest();
    var campaign = new DonationCampaign { Name = dto.Name, CrisisId = dto.CrisisId, TargetAmount = dto.TargetAmount };
    db.Campaigns.Add(campaign); TraceLog.Add(db, "Campaign", campaign.Id, "Created", Access.UserId(ctx.User));
    await db.SaveChangesAsync(); return Results.Ok(campaign);
}).RequireAuthorization(p => p.RequireRole(Roles.Funding, Roles.Coordinator, Roles.Admin));
api.MapPost("/donations", async (DonationDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.Amount <= 0 || !new[] { "LKR", "USD", "EUR" }.Contains(dto.Currency) ||
        !await db.Campaigns.AnyAsync(x => x.Id == dto.CampaignId && x.Status == "Open")) return Results.BadRequest();
    var donation = new Donation { DonorId = Access.UserId(ctx.User), CampaignId = dto.CampaignId,
        Amount = dto.Amount, Currency = dto.Currency, Kind = dto.Kind, Status = "PledgeRecorded" };
    db.Donations.Add(donation); TraceLog.Add(db, "Donation", donation.Id, "PledgeRecorded", donation.DonorId,
        new { donation.CampaignId, donation.Amount, donation.Currency });
    await db.SaveChangesAsync(); return Results.Ok(donation);
});
api.MapGet("/donations", async (HttpContext ctx, AidDbContext db) => {
    IQueryable<Donation> q = db.Donations.AsNoTracking();
    if (!Access.Staff(ctx.User)) q = q.Where(x => x.DonorId == Access.UserId(ctx.User));
    return await q.OrderByDescending(x => x.At).Take(200).ToListAsync();
});

api.MapGet("/notifications", async (HttpContext ctx, AidDbContext db) =>
    await db.Notifications.Where(x => x.RecipientId == Access.UserId(ctx.User)).OrderByDescending(x => x.At).Take(100).ToListAsync());
api.MapPatch("/notifications/{id:guid}/read", async (Guid id, HttpContext ctx, AidDbContext db) => {
    var n = await db.Notifications.FindAsync(id); if (n == null) return Results.NotFound();
    if (n.RecipientId != Access.UserId(ctx.User)) return Results.Forbid();
    n.Read = true; await db.SaveChangesAsync(); return Results.NoContent();
});
api.MapPost("/notifications/send", async (NotifyDto dto, HttpContext ctx, AidDbContext db) => {
    if (!await db.Users.AnyAsync(x => x.Id == dto.RecipientId)) return Results.NotFound();
    var actor = Access.UserId(ctx.User);
    TraceLog.Queue(db, dto.RecipientId, $"manual:{Guid.NewGuid()}", dto.Title, dto.Message, dto.Language);
    TraceLog.Add(db, "Notification", dto.RecipientId, "ManualQueued", actor);
    await db.SaveChangesAsync(); return Results.Accepted();
}).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Admin));

api.MapGet("/dashboard", async (AidDbContext db) => {
    var all = await db.Requests.AsNoTracking().ToListAsync();
    var resources = await db.Resources.AsNoTracking().ToListAsync();
    var sheltersList = await db.Shelters.AsNoTracking().ToListAsync();
    var reviewed = await db.AiRecommendations.Where(x => x.ReviewStatus != "Pending").ToListAsync();
    return Results.Ok(new {
        totalRequests = all.Count,
        unresolved = all.Count(x => x.Status is not ("Fulfilled" or "Closed")),
        pendingReview = all.Count(x => x.Status == "PendingReview"),
        urgent = all.Count(x => (x.Urgency == "Critical" || x.Urgency == "High") && x.Status != "Fulfilled" && x.Status != "Closed"),
        fulfilled = all.Count(x => x.Status is "Fulfilled" or "Closed"),
        shelterOccupancyPercent = sheltersList.Sum(x => x.Capacity) == 0 ? 0 : Math.Round(100.0 * sheltersList.Sum(x => x.Occupancy) / sheltersList.Sum(x => x.Capacity), 1),
        resourceUnitsAvailable = resources.Sum(x => x.AvailableQuantity),
        aiOverrideFrequency = reviewed.Count == 0 ? 0 : Math.Round(100.0 * reviewed.Count(x => x.ReviewStatus == "Modified") / reviewed.Count, 1),
        byCategory = all.GroupBy(x => x.Category).Select(g => new { category = g.Key, count = g.Count() }),
        recentRequests = all.OrderByDescending(x => x.CreatedAt).Take(7).Select(x => new { x.Id, x.Category, x.Status, x.Urgency, x.CreatedAt })
    });
}).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.CaseManager, Roles.Logistics, Roles.ShelterAdmin, Roles.VolunteerCoordinator, Roles.Partner, Roles.Admin));
api.MapGet("/audit", async (AidDbContext db, string? entityType, Guid? entityId) => {
    IQueryable<AuditEvent> q = db.Audits.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(x => x.EntityType == entityType);
    if (entityId != null) q = q.Where(x => x.EntityId == entityId);
    return await q.OrderByDescending(x => x.At).Take(300).ToListAsync();
}).RequireAuthorization(p => p.RequireRole(Roles.Coordinator, Roles.Admin));
api.MapGet("/roles", async (AidDbContext db) => new {
    roles = await db.RoleDefinitions.OrderBy(r => r.Name).ToListAsync(),
    permissions = await db.PermissionDefinitions.OrderBy(p => p.Code).ToListAsync(),
    rolePermissions = await db.RolePermissions.ToListAsync()
}).RequireAuthorization(p => p.RequireRole(Roles.Admin));
api.MapGet("/users", async (AidDbContext db) =>
    await db.Users.Select(u => new { u.Id, u.Email, u.DisplayName, u.Role, u.IsActive, u.OrganisationId }).ToListAsync())
    .RequireAuthorization(p => p.RequireRole(Roles.Admin));
api.MapPatch("/users/{id:guid}/role", async (Guid id, RoleDto dto, HttpContext ctx, AidDbContext db) => {
    var roles = new[] { Roles.Citizen, Roles.Volunteer, Roles.Donor, Roles.Coordinator, Roles.CaseManager,
        Roles.Logistics, Roles.ShelterAdmin, Roles.VolunteerCoordinator, Roles.Funding, Roles.Partner, Roles.Admin };
    if (!roles.Contains(dto.Role)) return Results.BadRequest();
    if (id == Access.UserId(ctx.User)) return Results.Conflict(new { error = "Self role change is blocked" });
    var u = await db.Users.FindAsync(id); if (u == null) return Results.NotFound();
    var old = u.Role; u.Role = dto.Role;
    u.RoleId = await db.RoleDefinitions.Where(r => r.Name == dto.Role).Select(r => (Guid?)r.Id).FirstOrDefaultAsync();
    TraceLog.Add(db, "User", id, "RoleChanged", Access.UserId(ctx.User), new { old, dto.Role });
    await db.SaveChangesAsync(); return Results.Ok(new { u.Id, u.Role });
}).RequireAuthorization(p => p.RequireRole(Roles.Admin));

api.MapGet("/map/geocode", async (string query, IHttpClientFactory factory, IConfiguration configuration, CancellationToken ct) => {
    if (query.Length is < 3 or > 100) return Results.BadRequest(new { error = "Search term must be 3-100 characters" });
    var baseUrl = configuration["Map:ForwardGeocodingBaseUrl"] ?? "https://nominatim.openstreetmap.org/search";
    if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        return Results.Json(new { error = "Geocoding provider is not configured with a valid HTTPS URL" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    try {
        var url = $"{uri}?format=json&limit=4&q=" + Uri.EscapeDataString(query);
        var json = await factory.CreateClient("map").GetStringAsync(url, ct);
        return Results.Content(json, "application/json");
    } catch (HttpRequestException) { return Results.Json(new { error = "Mapping provider unavailable; enter coordinates manually" }, statusCode: 503); }
      catch (TaskCanceledException) { return Results.Json(new { error = "Mapping provider timeout; enter coordinates manually" }, statusCode: 503); }
}).RequireRateLimiting("geocode");
api.MapGet("/map/distance", (double fromLat, double fromLon, double toLat, double toLon) =>
    Access.Coordinates(fromLat, fromLon) && Access.Coordinates(toLat, toLon) ?
        Results.Ok(new { straightLineKm = Math.Round(Distances.Km(fromLat, fromLon, toLat, toLon), 2), provider = "Haversine fallback (not travel distance)" }) :
        Results.BadRequest(new { error = "Invalid coordinates" }));

app.Run();

public partial class Program { } // WebApplicationFactory integration tests can reference the entry point.
