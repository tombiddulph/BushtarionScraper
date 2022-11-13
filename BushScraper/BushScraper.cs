using System.Text.RegularExpressions;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BushScraper;

public class BushScraper
{
    private readonly ILogger _logger;
    private readonly HttpClient _client;
    private const string Endpoint = "https://www.bushtarion.com/dumpdata1.txt";

    private static readonly DbConfig DbConfig = new()
    {
        DbName = "bushtarion",
        WorldContainer = "world",
        PlayersContainer = "players",
        AlliancesContainer = "alliances"
    };

    private readonly CosmosClient _cosmosClient;

    public BushScraper(ILoggerFactory loggerFactory, IHttpClientFactory clientFactory)
    {
        _logger = loggerFactory.CreateLogger<BushScraper>();
        _client = clientFactory.CreateClient() ?? throw new ArgumentNullException(nameof(clientFactory));
        _cosmosClient =
            new CosmosClient(Environment.GetEnvironmentVariable("COSMOS_ENDPOINT", EnvironmentVariableTarget.Process),
                new DefaultAzureCredential());
    }


    [Function("BushScraper")]
    public async Task Run([TimerTrigger("0 5 * * * *"
# if DEBUG
            , RunOnStartup = true
#endif
        )]
        TimerInfo myTimer)
    {
        var now = DateTimeOffset.Now;
        _logger.LogInformation("C# Timer trigger function executed at: {Now}", DateTime.Now);
        _logger.LogInformation("Next timer schedule at: {Time}", myTimer.ScheduleStatus.Next);
        
        var req = await _client.GetAsync(Endpoint);

        if (!req.IsSuccessStatusCode)
        {
            _logger.LogWarning("Request failed {StatusCode}", req.StatusCode);
            return;
        }

        var content = await req.Content.ReadAsStringAsync();
        var split = Regex.Split(content, "\r\n|\r|\n");

        List<Alliance> alliances = new();
        List<Player> players = new();
        World? world = null;


        foreach (var s in split.Where(x => !string.IsNullOrEmpty(x)))
        {
            try
            {
                var data = s[(s.IndexOf(",", StringComparison.Ordinal) + 1)..];
                world = s[0] switch
                {
                    'w' => new World(data, content) {TimeAdded = now},
                    _ => world
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occured parsing the response");
                throw;
            }
        }

        var db = _cosmosClient.GetDatabase(DbConfig.DbName);
        _ = await db.CreateContainerIfNotExistsAsync(
            new ContainerProperties($"{DbConfig.WorldContainer}-{world!.Round}", "/id"));
        var worldContainer = db.GetContainer($"{DbConfig.WorldContainer}-{world!.Round}");
        var worldExists = worldContainer.GetItemQueryIterator<World>($"select * from c where c.id = '{world.Id}'");

        if (worldExists.HasMoreResults)
        {
            var current = await worldExists.ReadNextAsync();
            if (current.Count == 1)
            {
                _logger.LogWarning("World already exists");
                return;
            }
        }

        foreach (var s in split.Where(x => !string.IsNullOrEmpty(x)))
        {
            try
            {
                var data = s[(s.IndexOf(",", StringComparison.Ordinal) + 1)..];
                switch (s[0])
                {
                    case 'a':
                    {
                        alliances.Add(new Alliance(data) {TimeAdded = now});
                        break;
                    }
                    case 'p':
                    {
                        players.Add(new Player(data) {TimeAdded = now});

                        break;
                    }
                    case 'w':
                    {
                        world = new World(data, content) {TimeAdded = now};
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "An error occured parsing the response");
                throw;
            }
        }


        await db.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = $"{DbConfig.PlayersContainer}-{world!.Round}",
            PartitionKeyPath = "/pk"
        });
        var playersContainer = db.GetContainer($"{DbConfig.PlayersContainer}-{world!.Round}");
        await db.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = $"{DbConfig.AlliancesContainer}-{world!.Round}",
            PartitionKeyPath = "/pk"
        });
        var allianceContainer = db.GetContainer($"{DbConfig.AlliancesContainer}-{world!.Round}");
        await db.CreateContainerIfNotExistsAsync(new ContainerProperties
        {
            Id = $"{DbConfig.WorldContainer}-{world!.Round}",
            PartitionKeyPath = "/id"
        });


        players.ForEach(x => x.WorldTickId = world.CurrentTick);
        alliances.ForEach(x => x.WorldTickId = world.CurrentTick);
        var playersTask = UpdateContainer(players, now, player => player.Pk, playersContainer);
        var alliancesTask = UpdateContainer(alliances, now, alliance => alliance.Pk, allianceContainer);
        var worldTask = UpdateContainer(world, now, worldContainer);

        await Task.WhenAll(playersTask, alliancesTask, worldTask);


        _logger.LogInformation("Got {Count} alliances", alliances.Count);
        _logger.LogInformation("Got {Count} players", players.Count);
        var executionTime = DateTimeOffset.UtcNow - now;
        _logger.LogInformation("Execution time: {ExecutionTime}", executionTime);
    }

    private async Task UpdateContainer<T>(IEnumerable<T> items, DateTimeOffset now, Func<T, string> pkSelector,
        Container container)
    {
        var enumerable = items.ToList();
        var name = enumerable.First()!.GetType().Name;
        foreach (var item in enumerable)
        {
            await container.UpsertItemAsync(item, new PartitionKey(pkSelector(item)));
            _logger.LogInformation("Added {Name} {Pk}", name, pkSelector(item));
        }
    }

    private static async Task UpdateContainer<T>(T item, DateTimeOffset now, Container container)
    {
        await container.UpsertItemAsync(item);
    }

    public class TimerInfo
    {
        public MyScheduleStatus ScheduleStatus { get; set; }

        public bool IsPastDue { get; set; }
    }

    public class MyScheduleStatus
    {
        public DateTime Last { get; set; }

        public DateTime Next { get; set; }

        public DateTime LastUpdated { get; set; }
    }
}