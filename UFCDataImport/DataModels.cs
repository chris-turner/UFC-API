using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UFCDataImport
{
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
}
