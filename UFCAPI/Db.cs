namespace UFCData.DB;

public class Fighter
{
    public string name { get; set; }
    public string height { get; set; }
    public string weight { get; set; }
    public string reach { get; set; }
    public string stance { get; set; }
    public DateTime dob { get; set; }
    public string nickname { get; set; }
    public int wins { get; set; }
    public int losses { get; set; }
    public int draws { get; set; }

}

public class FightCard
{
    public int fightCardID { get; set; }
    public string fightCardName { get; set; }
    public string date { get; set; }
    public string location { get; set; }
}

public class Fight
{
    public int fightID { get; set; }
    public FightCard fightCard { get; set; }
    public string fighter1 { get; set; }
    public string fighter2 { get; set; }
    public string fighter1Outcome { get; set; }
    public string fighter2Outcome { get; set; }
    public string weightClass { get; set; }
    public string method { get; set; }
    public string roundFinished { get; set; }
    public string timeFinished { get; set; }
    public string format { get; set; }
    public string referee { get; set; }
    public string bonus { get; set; }
    public string details { get; set; }
}


public class FightStats
{
    public Fight fight { get; set; }
    public string fighterName { get; set; }
    public int round { get; set; }
    public int KD { get; set; }
    public int landedSigStrikes { get; set; }
    public int totalSigStrikes { get; set; }
    public int landedStrikes { get; set; }
    public int totalStrikes { get; set; }
    public int takedowns { get; set; }
    public int takedownAttempts { get; set; }
    public int subAtt { get; set; }
    public int pass { get; set; }
    public int rev { get; set; }
    public string ctrl { get; set; }

}
public class SignificantStrikes
{
    public Fight fight { get; set; }
    public string fighterName { get; set; }
    public int round { get; set; }
    public int headStrikesLanded { get; set; }
    public int headStrikesAttempted { get; set; }
    public int bodyStrikesLanded { get; set; }
    public int bodyStrikesAttempted { get; set; }
    public int legStrikeslanded { get; set; }
    public int legStrikesAttempted { get; set; }
    public int distanceStrikesLanded { get; set; }
    public int distanceStrikesAttempted { get; set; }
    public int clinchStrikesLanded { get; set; }
    public int clinchStrikesAttempted { get; set; }
    public int groundStrikesLanded { get; set; }
    public int groundStrikesAttempted { get; set; }


}
public class UFCDataDB
{
    private static List<FightCard> _fightcards = new List<FightCard>()
{
    new FightCard{ fightCardID = 1, fightCardName = "Test 1", date = "2024-12-30", location = "New York, NY" },
    new FightCard{ fightCardID = 2, fightCardName = "Test 2", date = "2024-12-31", location = "Los Angeles, CA" },
    new FightCard{ fightCardID = 3, fightCardName = "Test 3", date = "2025-01-05", location = "Chicago, IL" }
};

    private static List<Fight> _fightsInFightCard = new List<Fight>();


    private static List<Fight> _fights = new List<Fight>();

    private static List<FightStats> _fightStats = new List<FightStats>();

    public static List<FightCard> GetFightCards()
    {
        return _fightcards;
    }

    public static FightCard? GetFightCard(int fightCardID)
    {
        return _fightcards.SingleOrDefault(FightCard => FightCard.fightCardID == fightCardID);
    }

    public static List<Fight> GetFightsOnFightCard(int fightCardID)
    {
        return _fightsInFightCard;
    }

    public static List<Fight> GetFights()
    {
        return _fights;
    }

    public static Fight? GetFight(int fightID)
    {
        return _fights.SingleOrDefault(Fight=> Fight.fightID == fightID);
    }

    public static List<FightStats> GetFightStats(int fightID)
    {
        return _fightStats;
    }
}