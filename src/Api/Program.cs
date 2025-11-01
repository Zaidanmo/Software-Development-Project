using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.OpenApi.Models;
using Realworlddotnet.Api.Features.Articles;
using Realworlddotnet.Api.Features.Profiles;
using Realworlddotnet.Api.Features.Tags;
using Realworlddotnet.Api.Features.Users;
using Realworlddotnet.Core.Repositories;
using Microsoft.Extensions.Configuration;
using Realworlddotnet.Api.Features.Search;
using Realworlddotnet.Api.Features.Images;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// allow any cross-origin resource sharing via localhost
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
            .AllowAnyHeader()
            .AllowAnyMethod();
    })
);

// add logging
builder.Host.UseSerilog((hostBuilderContext, services, loggerConfiguration) =>
{
    loggerConfiguration.ConfigureBaseLogging("realworldDotnet");
    loggerConfiguration.AddApplicationInsightsLogging(services, hostBuilderContext.Configuration);
});

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().AddCommandLine(args).Build();

// setup database connection (used for in memory SQLite).
// SQLite in memory requires an open connection during the application lifetime
#pragma warning disable S125
// to use a file based SQLite use: "Filename=../realworld.db";
// in:memory  configuration.GetConnectionString("Sqlite") ?? "Filename=:memory:";
#pragma warning restore S125
var connectionString = "Filename=../../realworld.db";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SupportNonNullableReferenceTypes();
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "realworlddotnet", Version = "v1" });
    c.AddSecurityDefinition("Bearer",
        new OpenApiSecurityScheme
        {
            In = ParameterLocation.Header,
            Description = "Please enter the token",
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            BearerFormat = "JWT",
            Scheme = "bearer"
        });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddScoped<IConduitRepository, ConduitRepository>();
builder.Services.AddScoped<IUserHandler, UserHandler>();
builder.Services.AddScoped<IArticlesHandler, ArticlesHandler>();
builder.Services.AddScoped<ITagsHandler, TagsHandler>();
builder.Services.AddScoped<IProfilesHandler, ProfilesHandler>();
builder.Services.AddScoped<ISearchHandler, SearchHandler>();
builder.Services.AddScoped<IImagesHandler, ImagesHandler>();

builder.Services.Configure<PasswordHasherOptions>(opt =>
    opt.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddSingleton<CertificateProvider>();

builder.Services.AddSingleton<ITokenGenerator>(container =>
{
    var logger = container.GetRequiredService<ILogger<CertificateProvider>>();
    var certificateProvider = new CertificateProvider(logger);
    var cert = certificateProvider.LoadFromFile("identityserver_testing.pfx", "password");

    return new TokenGenerator(cert);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<ILogger<CertificateProvider>>((o, logger) =>
    {
        var certificateProvider = new CertificateProvider(logger);
        var cert = certificateProvider.LoadFromFile("identityserver_testing.pfx", "password");

        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            IssuerSigningKey = new RsaSecurityKey(cert.GetRSAPublicKey()),
            ValidateLifetime = true             // new
        };
        o.Events = new JwtBearerEvents { OnMessageReceived = CustomOnMessageReceivedHandler.OnMessageReceived };
    });

// for SQLite in memory a connection is provided rather than a connection string
builder.Services.AddDbContext<ConduitContext>(options => { options.UseSqlite(connectionString); });

builder.Services.AddProblemDetails((Hellang.Middleware.ProblemDetails.ProblemDetailsOptions options) => { });
builder.Services.ConfigureOptions<ProblemDetailsLogging>();
builder.Services.AddCarter();

var app = builder.Build();

// Configure the HTTP request pipeline.
Log.Information("Start configuring http request pipeline");
Log.Information($"connectionString is {connectionString}");

// when using in memory SQLite ensure the tables are created
using (var scope = app.Services.CreateScope())
{
    using var context = scope.ServiceProvider.GetService<ConduitContext>();
    context?.Database.EnsureCreated();
}

app.UseSerilogRequestLogging(options =>
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "")
);


app.UsePathBase("/api");
app.UseRouting();
app.UseStaticFiles(); //new

var articleDir = Path.Combine(app.Environment.ContentRootPath, "Images", "Articles"); //new
Directory.CreateDirectory(articleDir);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(articleDir),
    RequestPath  = "/images/articles" 
});

var profileDir = Path.Combine(app.Environment.ContentRootPath, "Images", "ProfilePictures"); //new
Directory.CreateDirectory(profileDir);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(profileDir),
    RequestPath  = "/images" 
});

app.UseProblemDetails();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "realworlddotnet v1");
});
app.MapCarter();


try
{
    Log.Information("Starting web host");
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

