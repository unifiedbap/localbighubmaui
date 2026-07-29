namespace BigLocalHub.Services;

/// <summary>
/// Shell's default route registration constructs pages with
/// Activator.CreateInstance, which throws for any page whose constructor takes
/// an injected view model. This factory resolves through the DI container
/// instead, so pushed routes get the same wiring as the tab pages.
/// </summary>
public class DiRouteFactory : RouteFactory
{
    private readonly IServiceProvider _services;
    private readonly Type _pageType;

    public DiRouteFactory(IServiceProvider services, Type pageType)
    {
        _services = services;
        _pageType = pageType;
    }

    public override Element GetOrCreate() => (Element)_services.GetRequiredService(_pageType);

    public override Element GetOrCreate(IServiceProvider services) =>
        (Element)(services ?? _services).GetRequiredService(_pageType);
}
