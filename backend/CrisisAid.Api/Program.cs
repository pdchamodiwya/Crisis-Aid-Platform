using CrisisAid.Api.Endpoints;
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
builder.Services.AddScoped<IRequestRepository, EfRequestRepository>();
builder.Services.AddSingleton<IResourceMatchStrategyFactory, ResourceMatchStrategyFactory>();
builder.Services.AddRateLimiter(opts => {
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    opts.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", k => new FixedWindowRateLimiterOptions {
        PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    opts.AddPolicy("geocode", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", k => new FixedWindowRateLimiterOptions {
        PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
});
builder.Services.AddHostedService<OutboxWorker>();
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
    await SeedData.InitialiseAsync(database, app.Configuration);
    await ExtendedModules.EnsureSchemaAsync(database);
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
DonationEndpoints.Map(api);
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
    if (dto.Capacity < 1 || !Access.Coordinates(dto.Latitude, dto.Longitude)) return Results.BadRequest();
    var shelter = new Shelter { Name = dto.Name, Area = dto.Area, Capacity = dto.Capacity,
        Accessible = dto.Accessible, Facilities = dto.Facilities, Latitude = dto.Latitude, Longitude = dto.Longitude };
    db.Shelters.Add(shelter); TraceLog.Add(db, "Shelter", shelter.Id, "Created", Access.UserId(ctx.User));
    await db.SaveChangesAsync(); return Results.Created($"/api/shelters/{shelter.Id}", shelter);
}).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin));
shelters.MapPost("/allocate", async (AllocateShelterDto dto, HttpContext ctx, AidDbContext db) => {
    if (dto.PeopleCount < 1) return Results.BadRequest();
    await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
    var req = await db.Requests.FindAsync(dto.RequestId); var shelter = await db.Shelters.FindAsync(dto.ShelterId);
    if (req == null || shelter == null) return Results.NotFound();
    if (req.Status != "Verified" && req.Status != "Assigned") return Results.Conflict(new { error = "Human approval required" });
    if (req.Category != "Shelter" || shelter.Status != "Open" || (req.AccessibilityNeeded && !shelter.Accessible) ||
        dto.PeopleCount > req.PeopleAffected || dto.PeopleCount > shelter.Capacity - shelter.Occupancy)
        return Results.Conflict(new { error = "Capacity, suitability or request constraint failed" });
    if (await db.ShelterAllocations.AnyAsync(x => x.RequestId == req.Id)) return Results.Conflict(new { error = "Shelter already allocated to request" });
    shelter.Occupancy += dto.PeopleCount; req.Status = "Assigned"; req.UpdatedAt = DateTime.UtcNow;
    var actor = Access.UserId(ctx.User);
    var allocation = new ShelterAllocation { ShelterId = shelter.Id, RequestId = req.Id, PeopleCount = dto.PeopleCount, ApprovedBy = actor };
    db.ShelterAllocations.Add(allocation);
    TraceLog.Add(db, "ShelterAllocation", allocation.Id, "Approved", actor, new { dto.PeopleCount, dto.ShelterId });
    TraceLog.History(db, req.Id, actor, "Shelter assigned", shelter.Name);
    TraceLog.Queue(db, req.CitizenId, $"shelter:{allocation.Id}", "Shelter assigned", $"Your household was assigned to {shelter.Name}.");
    await db.SaveChangesAsync(); await tx.CommitAsync(); return Results.Ok(allocation);
}).RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin));
shelters.MapGet("/allocations", async (AidDbContext db) => await db.ShelterAllocations.AsNoTracking().ToListAsync())
    .RequireAuthorization(p => p.RequireRole(Roles.ShelterAdmin, Roles.Coordinator, Roles.Admin));

var volunteers = api.MapGroup("/volunteers");
volunteers.MapGet("/", async (AidDbContext db) => await db.Volunteers.AsNoTracking().ToListAsync())
    .RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin));
volunteers.MapPut("/me", async (VolunteerProfileDto dto, HttpContext ctx, AidDbContext db) => {
    if (Access.Role(ctx.User) != Roles.Volunteer) return Results.Forbid();
    var id = Access.UserId(ctx.User);
    var profile = await db.Volunteers.SingleOrDefaultAsync(v => v.UserId == id);
    if (profile == null) { profile = new VolunteerProfile { UserId = id }; db.Volunteers.Add(profile); }
    profile.Skills = dto.Skills; profile.Area = dto.Area; profile.Available = dto.Available;
    TraceLog.Add(db, "Volunteer", profile.Id, "ProfileUpdated", id);
    await db.SaveChangesAsync(); return Results.Ok(profile);
});
volunteers.MapPatch("/{id:guid}/verify", async (Guid id, VerifyVolunteerDto dto, HttpContext ctx, AidDbContext db) => {
    var v = await db.Volunteers.FindAsync(id); if (v == null) return Results.NotFound();
    v.Verified = dto.Verified; TraceLog.Add(db, "Volunteer", id, "VerificationUpdated", Access.UserId(ctx.User), dto);
    await db.SaveChangesAsync(); return Results.Ok(v);
}).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin));
volunteers.MapPost("/tasks", async (AssignVolunteerDto dto, HttpContext ctx, AidDbContext db) => {
    var v = await db.Volunteers.FindAsync(dto.VolunteerId);
    if (v == null) return Results.NotFound();
    if (!v.Verified || !v.Available || !v.Skills.Contains(dto.RequiredSkill, StringComparison.OrdinalIgnoreCase))
        return Results.Conflict(new { error = "Volunteer verification, availability or required skill constraint failed" });
    if (dto.RequestId is Guid reqid && !await db.Requests.AnyAsync(r => r.Id == reqid && (r.Status == "Verified" || r.Status == "Assigned")))
        return Results.Conflict(new { error = "Request not approved" });
    var task = new VolunteerTask { VolunteerId = v.Id, RequestId = dto.RequestId, RequiredSkill = dto.RequiredSkill,
        Description = dto.Description, AssignedBy = Access.UserId(ctx.User) };
    db.VolunteerTasks.Add(task); v.Available = false;
    TraceLog.Add(db, "VolunteerTask", task.Id, "Assigned", Access.UserId(ctx.User));
    TraceLog.Queue(db, v.UserId, $"volunteer:{task.Id}", "Volunteer task assigned", dto.Description);
    await db.SaveChangesAsync(); return Results.Ok(task);
}).RequireAuthorization(p => p.RequireRole(Roles.VolunteerCoordinator, Roles.Coordinator, Roles.Admin));
volunteers.MapGet("/tasks", async (HttpContext ctx, AidDbContext db) => {
        if (Access.CanManageVolunteers(ctx.User)) return Results.Ok(await db.VolunteerTasks.OrderByDescending(x => x.At).ToListAsync());
    var id = Access.UserId(ctx.User);
    var volunteerIds = db.Volunteers.Where(x => x.UserId == id).Select(x => x.Id);
    return Results.Ok(await db.VolunteerTasks.Where(x => volunteerIds.Contains(x.VolunteerId)).ToListAsync());
});
volunteers.MapPatch("/tasks/{id:guid}", async (Guid id, TaskStatusDto dto, HttpContext ctx, AidDbContext db) => {
    var task = await db.VolunteerTasks.FindAsync(id); if (task == null) return Results.NotFound();
    var profile = await db.Volunteers.FindAsync(task.VolunteerId); if (profile == null) return Results.NotFound();
        if (!Access.CanManageVolunteers(ctx.User) && profile.UserId != Access.UserId(ctx.User)) return Results.Forbid();
    if (!((task.Status == "Assigned" && dto.Status == "Accepted") || (task.Status == "Accepted" && dto.Status == "Completed")))
        return Results.Conflict(new { error = "Invalid task state transition" });
    task.Status = dto.Status; if (dto.Status == "Completed") { task.CompletedAt = DateTime.UtcNow; profile.Available = true; }
    TraceLog.Add(db, "VolunteerTask", id, dto.Status, Access.UserId(ctx.User));
    await db.SaveChangesAsync(); return Results.Ok(task);
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

api.MapGet("/map/geocode", async (string query, IHttpClientFactory factory, CancellationToken ct) => {
    if (query.Length is < 3 or > 100) return Results.BadRequest(new { error = "Search term must be 3-100 characters" });
    try {
        var url = "https://nominatim.openstreetmap.org/search?format=json&limit=4&q=" + Uri.EscapeDataString(query);
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
