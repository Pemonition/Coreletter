using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;

using Microsoft.AspNetCore.Authentication.Cookies;

using Microsoft.AspNetCore.DataProtection;

using Microsoft.AspNetCore.Identity;

using Microsoft.EntityFrameworkCore;

using RadarPlural.Api;

var builder = WebApplication.CreateBuilder(args);

// Plataformas de hospedagem informam a porta em PORT; localmente a API continua em localhost:5080.
var port = Environment.GetEnvironmentVariable("PORT");
builder.WebHost.UseUrls(string.IsNullOrWhiteSpace(port) ? "http://localhost:5080" : $"http://0.0.0.0:{port}");
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32768);

builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(DatabaseUrl.ToNpgsql(builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Configure ConnectionStrings__Default para PostgreSQL."))));

// Chaves que protegem o cookie ficam no banco: reinício ou novo deploy do container não derruba as sessões.
builder.Services.AddDataProtection().PersistKeysToDbContext<AppDb>().SetApplicationName("coreletter");

builder.Services.AddSingleton<IPasswordHasher<Member>, PasswordHasher<Member>>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "radarplural.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Events.OnRedirectToLogin = c =>
    {
        c.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = c =>
    {
        c.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", context =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Reject browser cross-origin mutations, including requests made with same-site cookies.
app.Use(async (context, next) =>
{
    if (context.Request.Method is not ("GET" or "HEAD" or "OPTIONS") && context.Request.Headers.TryGetValue("Origin", out var origins))
    {
        if (!Uri.TryCreate(origins.ToString(), UriKind.Absolute, out var origin) || origin.Authority != context.Request.Host.Value)
        {
            context.Response.StatusCode = 403;
            return;
        }
    }
    await next();
});

// Em produção o build Angular é copiado para wwwroot: front e API na mesma origem, sem CORS.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Migrações versionadas em todos os ambientes; o administrador é promovido só se a conta já existir.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
    var adminEmail = builder.Configuration["BootstrapAdminEmail"];
    if (!string.IsNullOrWhiteSpace(adminEmail))
    {
        var admin = await db.Members.SingleOrDefaultAsync(x => x.Email == adminEmail.ToLowerInvariant());
        if (admin is not null)
        {
            admin.IsAdmin = true;
            await db.SaveChangesAsync();
        }
    }
}
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/auth/register", async (RegisterInput input, AppDb db, IPasswordHasher<Member> hasher, HttpContext ctx) =>
{
    var email = input.Email?.Trim().ToLowerInvariant() ?? "";
    if (email.Length > 254 || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email || string.IsNullOrWhiteSpace(input.DisplayName) || input.DisplayName.Length > 80 || input.Password is null || input.Password.Length < 10 || input.Password.Length > 128) return Error("Informe e-mail, nome (até 80) e senha de 10 a 128 caracteres.");
    if (await db.Members.AnyAsync(x => x.Email == email)) return Error("Não foi possível cadastrar esse e-mail.");
    var user = new Member { Email = email, DisplayName = input.DisplayName.Trim() };
    user.PasswordHash = hasher.HashPassword(user, input.Password);
    db.Members.Add(user);
    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        return Error("Não foi possível cadastrar esse e-mail.");
    }
    await SignIn(ctx, user);
    return Results.Ok(UserDto(user));
}).RequireRateLimiting("api");

app.MapPost("/api/auth/login", async (LoginInput input, AppDb db, IPasswordHasher<Member> hasher, HttpContext ctx) =>
{
    var user = await db.Members.SingleOrDefaultAsync(x => x.Email == (input.Email ?? "").Trim().ToLowerInvariant());
    if (user is null || input.Password is null || input.Password.Length > 128 || hasher.VerifyHashedPassword(user, user.PasswordHash, input.Password) == PasswordVerificationResult.Failed) return Results.Json(new { error = "Credenciais inválidas." }, statusCode: 401);
    await SignIn(ctx, user);
    return Results.Ok(UserDto(user));
}).RequireRateLimiting("api");

app.MapPost("/api/auth/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("api");

app.MapGet("/api/auth/me", async (HttpContext ctx, AppDb db) =>
{
    var user = await db.Members.FindAsync(Id(ctx));
    return user is null ? Results.Unauthorized() : Results.Ok(UserDto(user));
}).RequireAuthorization().RequireRateLimiting("api");

app.MapGet("/api/posts", async (HttpContext ctx, AppDb db, int page = 1, int pageSize = 20, string? topic = null, string? type = null) =>
{
    if (page < 1 || page > 1000000) return Error("Página deve estar entre 1 e 1000000.");
    pageSize = Math.Clamp(pageSize, 1, 50);
    var q = db.Posts.AsNoTracking().Where(x => !x.Hidden);
    if (!string.IsNullOrWhiteSpace(topic)) q = q.Where(x => x.Topic.ToLower().Contains(topic.ToLower()));
    if (!string.IsNullOrWhiteSpace(type)) q = q.Where(x => x.Type == type);
    var total = await q.CountAsync();
    var posts = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    var memberId = ctx.User.Identity?.IsAuthenticated == true ? Id(ctx) : Guid.Empty;
    var postIds = posts.Select(x => x.Id).ToList();
    var saved = await db.Saved.Where(x => x.MemberId == memberId && postIds.Contains(x.PostId)).Select(x => x.PostId).ToListAsync();
    return Results.Ok(new { items = posts.Select(p => PostDto(p, saved.Contains(p.Id))), page, pageSize, total });
});

app.MapGet("/api/posts/saved", async (HttpContext ctx, AppDb db) =>
{
    var memberId = Id(ctx);
    var posts = await db.Posts.AsNoTracking().Where(x => !x.Hidden && db.Saved.Any(s => s.PostId == x.Id && s.MemberId == memberId)).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(100).ToListAsync();
    return Results.Ok(new { items = posts.Select(p => PostDto(p, true)), page = 1, pageSize = 100, total = posts.Count });
}).RequireAuthorization().RequireRateLimiting("api");

app.MapPost("/api/posts", async (PostInput input, HttpContext ctx, AppDb db) =>
{
    if (!Rules.ValidUrl(input.Url) || input.Url.Length > 2048 || string.IsNullOrWhiteSpace(input.Title) || input.Title.Length > 180 || string.IsNullOrWhiteSpace(input.Topic) || input.Topic.Length > 60 || input.Type is not ("article" or "podcast" or "music") || (input.Comment?.Length ?? 0) > 2000) return Error("Link http(s), título (180), tema (60), tipo válido e comentário (2000) são necessários.");
    var user = await db.Members.FindAsync(Id(ctx));
    if (user is null) return Results.Unauthorized();
    var post = new Post { Url = input.Url.Trim(), Title = input.Title.Trim(), Topic = input.Topic.Trim(), Type = input.Type, Comment = input.Comment?.Trim() ?? "", AuthorId = user.Id, AuthorName = user.DisplayName };
    db.Posts.Add(post);
    await db.SaveChangesAsync();
    return Results.Created($"/api/posts/{post.Id}", PostDto(post, false));
}).RequireAuthorization().RequireRateLimiting("api");

app.MapDelete("/api/posts/{id:guid}", async (Guid id, HttpContext ctx, AppDb db) =>
{
    var post = await db.Posts.FindAsync(id);
    if (post is null) return Results.NotFound();
    if (post.AuthorId != Id(ctx)) return Results.Forbid();
    db.Posts.Remove(post);
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("api");

app.MapPut("/api/posts/{id:guid}/save", async (Guid id, HttpContext ctx, AppDb db) =>
{
    if (!await db.Posts.AnyAsync(x => x.Id == id && !x.Hidden)) return Results.NotFound();
    var memberId = Id(ctx);
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Saved\" (\"MemberId\", \"PostId\") VALUES ({memberId}, {id}) ON CONFLICT DO NOTHING");
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("api");

app.MapDelete("/api/posts/{id:guid}/save", async (Guid id, HttpContext ctx, AppDb db) =>
{
    var memberId = Id(ctx);
    await db.Saved.Where(x => x.PostId == id && x.MemberId == memberId).ExecuteDeleteAsync();
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("api");

app.MapPost("/api/posts/{id:guid}/reports", async (Guid id, ReportInput input, HttpContext ctx, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 1000) return Error("Informe motivo de até 1000 caracteres.");
    if (!await db.Posts.AnyAsync(x => x.Id == id && !x.Hidden)) return Results.NotFound();
    db.Reports.Add(new Report { PostId = id, MemberId = Id(ctx), Reason = input.Reason.Trim() });
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization().RequireRateLimiting("api");

app.MapGet("/api/moderation/reports", async (AppDb db) => Results.Ok(await db.Reports.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Take(100).Select(x => new { x.Id, x.PostId, x.Reason, x.CreatedAt }).ToListAsync())).RequireAuthorization(p => p.RequireRole("admin"));

app.MapPut("/api/moderation/posts/{id:guid}/hide", async (Guid id, AppDb db) =>
{
    var post = await db.Posts.FindAsync(id);
    if (post is null) return Results.NotFound();
    post.Hidden = true;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).RequireAuthorization(p => p.RequireRole("admin"));

app.Run();

static Guid Id(HttpContext ctx) => Guid.Parse(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!);

static object UserDto(Member user) => new { user.Id, user.Email, user.DisplayName, user.IsAdmin };

static object PostDto(Post p, bool saved) => new { p.Id, p.Url, p.Title, p.Topic, p.Type, p.Comment, p.AuthorId, p.AuthorName, p.CreatedAt, isSaved = saved };

static IResult Error(string message) => Results.BadRequest(new { error = message });

static Task SignIn(HttpContext ctx, Member user)
{
    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.DisplayName) };
    if (user.IsAdmin) claims.Add(new(ClaimTypes.Role, "admin"));
    return ctx.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
}
public partial class Program { }
