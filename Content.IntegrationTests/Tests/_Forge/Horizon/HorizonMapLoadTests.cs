using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Forge.Horizon;

[TestFixture]
public sealed class HorizonMapLoadTests
{
    private static readonly ResPath[] DedicatedGrids =
    [
        new("/Maps/_Forge/Horizon/rtr.yml"),
        new("/Maps/_Forge/Horizon/Shuttles/ams01_kamenshchik.yml"),
        new("/Maps/_Forge/Horizon/Stations/o01.yml"),
        new("/Maps/_Forge/Horizon/Stations/d04_uglich.yml"),
    ];

    [Test]
    public async Task DedicatedGridsAreLoadable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.ResolveDependency<IEntityManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var mapLoader = entities.System<MapLoaderSystem>();
        var maps = entities.System<SharedMapSystem>();

        Assert.That(configuration.GetCVar(CCVars.GridFill), Is.False);

        await server.WaitAssertion(() =>
        {
            foreach (var path in DedicatedGrids)
            {
                maps.CreateMap(out var mapId);
                try
                {
                    Assert.That(mapLoader.TryLoadGrid(mapId, path, out var grid), Is.True,
                        $"Failed to load Horizon grid {path}.");
                    Assert.That(grid, Is.Not.Null, $"Horizon grid {path} loaded without a grid entity.");
                }
                finally
                {
                    maps.DeleteMap(mapId);
                }
            }
        });

        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }
}
