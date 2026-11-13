using Microsoft.Extensions.DependencyInjection;
using RadarPlural.Application.Auth;
using RadarPlural.Application.Moderation;
using RadarPlural.Application.News;
using RadarPlural.Application.Posts;

namespace RadarPlural.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services) => services
        .AddScoped<AuthService>()
        .AddScoped<PostService>()
        .AddScoped<NewsService>()
        .AddScoped<ModerationService>();
}
