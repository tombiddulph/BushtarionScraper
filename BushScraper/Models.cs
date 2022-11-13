namespace BushScraper;

public abstract class ScraperRun
{
    public int WorldTickId { get; set; }
    public int RoundNumber { get; set; }
    public DateTimeOffset TimeAdded { get; set; }
}

public class Alliance : ScraperRun
{
    public Alliance()
    {
    }

    public Alliance(string text)
    {
        var split = text.Split(',');
        Public = bool.TryParse(split[0], out var pub) && pub;
        Name = split[1];
        Members = int.TryParse(split[2], out var members) ? members : 0;
        Acres = int.TryParse(split[3], out var acres) ? acres : 0;
        Score = long.TryParse(split[4], out var score) ? score : 0;
        Logo = split[5];
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Public { get; set; }
    public string Name { get; set; }
    public int Members { get; set; }
    public int Acres { get; set; }
    public long Score { get; set; }
    public string Logo { get; set; }
    public string Pk => Name;
}

public class Player : ScraperRun
{
    public Player()
    {
    }

    public Player(string text)
    {
        var split = text.Split(',');
        PlayerId = split[0].ParseOrDefault();
        Name = split[1];
        Tag = split[2];
        Acres = split[3].ParseOrDefault();
        Locked = bool.TryParse(split[4], out var locked) && locked;
        Sleep = bool.TryParse(split[5], out var sleep) && sleep;
        Score = long.TryParse(split[6], out var score) ? score : 0;
        Rank = split[7].ParseOrDefault();
        Eff = long.TryParse(split[8], out var eff) ? eff : 0;
        EffectivenessRanking = split[9].ParseOrDefault();
        Bhunt = long.TryParse(split[10], out var bhunt) ? bhunt : 0;
        Bhuntrank = split[11].ParseOrDefault();
        Hfrating = decimal.TryParse(split[12], out var hfRating) ? hfRating : 0;
        Hftitle = split[13];
        Bounty = split[14].ParseOrDefault();
        Profile = split[15];
    }


    public Guid Id { get; set; } = Guid.NewGuid();
    public string Pk => $"{PlayerId}";
    public int PlayerId { get; set; }
    public string Name { get; set; }
    public string Tag { get; set; }
    public int Acres { get; set; }
    public bool Locked { get; set; }
    public bool Sleep { get; set; }
    public long Score { get; set; }
    public int Rank { get; set; }
    public long Eff { get; set; }
    public int EffectivenessRanking { get; set; }
    public long Bhunt { get; set; }
    public int Bhuntrank { get; set; }
    public decimal Hfrating { get; set; }
    public string Hftitle { get; set; }
    public int Bounty { get; set; }
    public string Profile { get; set; }
}

public class World : ScraperRun
{
    public World()
    {
    }

    public World(string line, string raw)
    {
        var data = line[(line.IndexOf(",", StringComparison.Ordinal) + 1)..].Split(",");
        CurrentTick = data[0].ParseOrDefault();
        FinalTick = data[1].ParseOrDefault();
        GameYear = data[2].ParseOrDefault();
        GameMonth = data[3].ParseOrDefault();
        GameDay = data[4].ParseOrDefault();
        GameTime = data[5].ParseOrDefault();
        WeatherId = data[6].ParseOrDefault();
        WeatherDescription = data[7];
        Description = data[8];
        Round = data[9].ParseOrDefault();
        DevMod = decimal.TryParse(data[10], out var devMod) ? devMod : 0;
        TickFrequency = data[11].ParseOrDefault();
        RawData = raw;
    }
    
    public string Id => CurrentTick.ToString();
    public int CurrentTick { get; set; }
    public int FinalTick { get; set; }
    public int GameYear { get; set; }
    public int GameMonth { get; set; }
    public int GameDay { get; set; }
    public int GameTime { get; set; }
    public int WeatherId { get; set; }
    public string WeatherDescription { get; set; }
    public string Description { get; set; }
    public int Round { get; set; }
    public decimal DevMod { get; set; }
    public int TickFrequency { get; set; }

    public string RawData { get; set; }
}


public class DbConfig
{
    public string DbName { get; set; }
    public string PlayersContainer { get; set; }
    public string AlliancesContainer { get; set; }
    public string WorldContainer { get; set; } = "world";
    public string Endpoint { get; set; }
    public string SubscriptionId { get; set; }
    public string ResourceGroupName { get; set; }
    public string AccountName { get; set; }
}

public class ResponseData<T>
{
    public string Id { get; set; }
    public IEnumerable<T> Data { get; set; }
}



public static class Extensions
{
    public static int ParseOrDefault(this string s) => int.TryParse(s, out var res) ? res : 0;
}