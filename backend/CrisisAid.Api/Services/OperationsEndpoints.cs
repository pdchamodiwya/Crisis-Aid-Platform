using Microsoft.AspNetCore.Routing;

namespace CrisisAid.Api.Services;

public static class OperationsEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        ShelterOperationsEndpoints.Map(api);
        VolunteerOperationsEndpoints.Map(api);
        MapOperationsEndpoints.Map(api);
    }
}
