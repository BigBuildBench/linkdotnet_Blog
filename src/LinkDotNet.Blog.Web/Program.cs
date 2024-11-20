using System;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Blazored.Toast;
using Blazorise;
using Blazorise.Bootstrap5;
using HealthChecks.UI.Client;
using LinkDotNet.Blog.Web.Authentication.OpenIdConnect;
using LinkDotNet.Blog.Web.Authentication.Dummy;
using LinkDotNet.Blog.Web.RegistrationExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LinkDotNet.Blog.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        RegisterServices(builder);

        await using var app = builder.Build();
        ConfigureApp(app);

        await app.RunAsync();
    }

    private static void RegisterServices(WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor(s =>
        {
#if DEBUG
            s.DetailedErrors = true;
#endif
        });
        builder.Services.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 1024 * 1024;
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy<string>("ip", httpContext =>

                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1) })
            );
        });

        builder.Services.AddConfiguration();

        builder.Services.AddBlazoredToast();
        builder.Services
            .AddBlazorise()
            .AddBootstrap5Providers();

        builder.Services.RegisterServices();
        builder.Services.AddStorageProvider(builder.Configuration);
        builder.Services.AddResponseCompression();

        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("Database");

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.UseDummyAuthentication();
        }
        else
        {
            builder.Services.UseAuthentication();
        }
    }

    private static void ConfigureApp(WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseResponseCompression();
        app.UseHttpsRedirection();
        app.MapStaticAssets();

        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
        })
        .RequireAuthorization();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseRateLimiter();
        app.MapControllers();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        app.MapFallbackToPage("/searchByTag/{tag}", "/_Host");
        app.MapFallbackToPage("/search/{searchTerm}", "/_Host");
    }
}
