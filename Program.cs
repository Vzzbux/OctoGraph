using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using OctoGraph.Models;

var builder = WebApplication.CreateBuilder(args);

//Add in our custom settings object, so that services can get at it
builder.Services.AddOptions<OctoGraphOptions>()
    .Bind(builder.Configuration.GetSection(OctoGraphOptions.ConfigSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

//...then build it again, so we can access it here, i.e. before the call to builder.Build()
var settings = builder.Configuration.GetSection(OctoGraphOptions.ConfigSection).Get<OctoGraphOptions>();

if (settings.AuthenticationEnabled)
{
    // Add services to the container.
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddAuthorization(options =>
    {
        // By default, all incoming requests will be authorized according to the default policy.
        options.FallbackPolicy = options.DefaultPolicy;
    });
}

builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        //options.JsonSerializerOptions.IgnoreNullValues = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

foreach (var octopusInstance in settings.OctopusInstances)
{
    builder.Services.AddHttpClient(octopusInstance.Label, c =>
    {
        c.BaseAddress = new Uri($"https://{octopusInstance.Hostname}/");
        c.DefaultRequestHeaders.Add("User-Agent", "HttpClientFactory-Sample");
        c.DefaultRequestHeaders.Add("X-Octopus-ApiKey", octopusInstance.ApiKey);
    });
}

var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .Build();
            //.WithOrigins($"https://{Dns.GetHostEntry("LocalHost").HostName}", "https://localhost:44370");
        });
});

builder.Services.AddMemoryCache();

var app = builder.Build();

//var octoGraphConfig = app.Services.GetRequiredService<IOptions<OctoGraphOptions>>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseCors(MyAllowSpecificOrigins);

app.UseAuthentication();
app.UseAuthorization();

if (settings.AuthenticationEnabled)
{
    app.MapRazorPages().RequireAuthorization();
    app.MapControllers().RequireAuthorization();
}
else
{
    app.MapRazorPages().AllowAnonymous();
    app.MapControllers().AllowAnonymous();
}

app.Run();
