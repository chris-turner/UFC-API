using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.XPath;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;

namespace UFCDataImport
{
    public class DataImport
    {
        public static async Task getUFCStatsAsync()
        {
            var config = Configuration.Default.WithDefaultLoader();

            string startURL = "http://ufcstats.com/statistics/events/completed?page=all";
            var context = BrowsingContext.New(config);
            var document = await context.OpenAsync(startURL);

            List<INode> fightCardEls = document.Body.SelectNodes("//a[contains(@href, 'http://ufcstats.com/event-details/')]");

            List<FightCard> cards = new List<FightCard>();
            List<Fight> fights = new List<Fight>();
            List<FightStats> fightStats = new List<FightStats>();
            List<SignificantStrikes> sigStrikes = new List<SignificantStrikes>();

            int fightCounter = 1;
            //for (int i = 0; i < 26; i++)
            try
            {
                int count = fightCardEls.Count;
                count = 10;
                for (int i = 0; i < count; i++)
                {
                    IHtmlAnchorElement currentEl = (IHtmlAnchorElement)fightCardEls[i];
                    document = await context.OpenAsync(currentEl.Href);
                    FightCard card = new FightCard();

                    card.fightCardID = i + 1;
                    card.date = ((Element)document.Body.SelectSingleNode(@"//i[contains(text(), 'Date:')]//parent::li[1]")).TextContent.Replace("\n", "").Replace("Date:", "").Trim();
                    card.fightCardName = ((Element)document.Body.SelectSingleNode("//span[@class = 'b-content__title-highlight']")).TextContent.Replace("\n", "").Trim();
                    card.location = ((Element)document.Body.SelectSingleNode(@"//i[contains(text(), 'Location:')]//parent::li[1]")).TextContent.Replace("\n", "").Replace("Location:", "").Trim();
                    cards.Add(card);

                    List<INode> fightEls = document.Body.SelectNodes("//a[contains(@href, 'http://ufcstats.com/fight-details/')]");

                    for (int x = 0; x < fightEls.Count; x++)
                    {

                        IHtmlAnchorElement currentFightEl = (IHtmlAnchorElement)fightEls[x];
                        document = await context.OpenAsync(currentFightEl.Href);
                        Fight fight = new Fight();
                        fight.fightCard = card;
                        fight.fightID = fightCounter;
                        var cellSelector = "h2 a";
                        var cells = document.QuerySelectorAll(cellSelector);

                        //Error nullreference exception here
                        string result = ((Element)(document.Body.SelectSingleNode("//i[@class = 'b-fight-details__person-status b-fight-details__person-status_style_gray']"))).InnerHtml.Replace("\n", "").Trim();

                        if (result != "D" && result != "NC")
                        {
                            fight.fighter1 = ((Element)document.Body.SelectSingleNode("//i[@class = 'b-fight-details__person-status b-fight-details__person-status_style_green']//following::a[1]")).InnerHtml;
                            fight.fighter1Outcome = "Win";
                            fight.fighter2 = ((Element)document.Body.SelectSingleNode("//i[@class = 'b-fight-details__person-status b-fight-details__person-status_style_gray']//following::a[1]")).InnerHtml;
                            fight.fighter2Outcome = "Loss";
                        }
                        else if (result == "D")
                        {
                            List<INode> fighters = document.Body.SelectNodes("//h3[@class='b-fight-details__person-name']//following::a");
                            fight.fighter1 = ((Element)fighters[0]).InnerHtml;
                            fight.fighter1Outcome = "Draw";
                            fight.fighter2 = ((Element)fighters[1]).InnerHtml;
                            fight.fighter2Outcome = "Draw";
                        }
                        else if (result == "NC")
                        {
                            List<INode> fighters = document.Body.SelectNodes("//h3[@class='b-fight-details__person-name']//following::a");
                            fight.fighter1 = ((Element)fighters[0]).InnerHtml;
                            fight.fighter1Outcome = "NC";
                            fight.fighter2 = ((Element)fighters[1]).InnerHtml;
                            fight.fighter2Outcome = "NC";
                        }
                        else
                        {
                            throw new Exception("Unhandled Result");
                        }

                        fight.bonus = "None";

                        Element bonusImage = ((Element)document.Body.SelectSingleNode("//i[@class = 'b-fight-details__fight-title']//child::img[1]"));

                        if (bonusImage != null)
                        {
                            string bonusImgURL = ((Element)document.Body.SelectSingleNode("//i[@class = 'b-fight-details__fight-title']//child::img[1]")).GetAttribute("src");
                            fight.bonus = bonusImgURL.Substring(bonusImgURL.LastIndexOf('/') + 1).Replace(".png", "");
                        }

                        fight.weightClass = ((Element)document.Body.SelectSingleNode("//i[@class = 'b-fight-details__fight-title']")).TextContent.Replace(" Bout", "").Replace("\n", "").Trim();
                        fight.method = ((Element)document.Body.SelectSingleNode(@"//i[contains(text(), 'Method:')]//following::i[1]")).TextContent.Replace("\n", "").Trim();
                        fight.roundFinished = ((Element)document.Body.SelectSingleNode(@"//i[contains(text(), 'Round:')]//parent::i[1]")).TextContent.Replace("\n", "").Replace("Round:", "").Trim();
                        fight.timeFinished = ((Element)document.Body.SelectSingleNode(@"//i[contains(text(), 'Time:')]//parent::i[1]")).TextContent.Replace("\n", "").Replace("Time:", "").Trim();
                        fight.format = ((Element)document.Body.SelectSingleNode(@"//i[contains(text(), 'Time:')]//following::i[1]")).TextContent.Replace("\n", "").Replace("Time format:", "").Trim();
                        fight.referee = ((Element)document.Body.SelectSingleNode(@"//i[contains(text(), 'Referee:')]//parent::i[1]")).TextContent.Replace("\n", "").Replace("Referee:", "").Trim();
                        fight.details = ((Element)document.Body.SelectSingleNode(@"//i[contains(text(), 'Details:')]//parent::i[1]//parent::p[1]")).TextContent.Replace("\n", "").Replace("Details:", "").Trim();

                        fights.Add(fight);
                        fightCounter++;
                        FightStats fs1 = new FightStats();
                        FightStats fs2 = new FightStats();

                        SignificantStrikes ss1 = new SignificantStrikes();
                        SignificantStrikes ss2 = new SignificantStrikes();

                        fs1.fighterName = ((Element)document.Body.SelectSingleNode(@"//a[@class ='b-link b-link_style_black']")).TextContent.Replace("\n", "").Trim();
                        fs2.fighterName = ((Element)document.Body.SelectNodes(@"//a[@class ='b-link b-link_style_black']")[1]).TextContent.Replace("\n", "").Trim();

                        ss1.fighterName = ((Element)document.Body.SelectSingleNode(@"//a[@class ='b-link b-link_style_black']")).TextContent.Replace("\n", "").Trim();
                        ss2.fighterName = ((Element)document.Body.SelectNodes(@"//a[@class ='b-link b-link_style_black']")[1]).TextContent.Replace("\n", "").Trim();

                        fs1.fight = fight;
                        fs2.fight = fight;

                        ss1.fight = fight;
                        ss2.fight = fight;

                        List<INode> statsEls = document.Body.SelectNodes("//tr[@class='b-fight-details__table-row']");

                        bool ssStats = false;
                        int currRd = 0;

                        for (int y = 0; y < statsEls.Count; y++)
                        {
                            Element statsEl = (Element)(statsEls[y]);
                            if (!ssStats)
                            {
                                if (statsEl.InnerHtml.Contains("Distance"))
                                {
                                    ssStats = true;
                                    currRd = 0;
                                }
                                else if ((!statsEl.InnerHtml.Contains("Total str.")))
                                {
                                    fs1.round = currRd;
                                    fs2.round = currRd;

                                    var stats = statsEl.SelectNodes("//p[@class='b-fight-details__table-text']");

                                    fs1.KD = Convert.ToInt32(stats[2].TextContent.Replace("\n", "").Trim());
                                    fs2.KD = Convert.ToInt32(stats[3].TextContent.Replace("\n", "").Trim());


                                    string sigStrikesStr = stats[4].TextContent.Replace("\n", "").Trim();
                                    string[] sigStrikesArr = sigStrikesStr.Split(" of ");

                                    fs1.landedSigStrikes = Convert.ToInt32(sigStrikesArr[0]);
                                    fs1.totalSigStrikes = Convert.ToInt32(sigStrikesArr[1]);

                                    string sigStrikesStr2 = stats[5].TextContent.Replace("\n", "").Trim();
                                    string[] sigStrikesArr2 = sigStrikesStr2.Split(" of ");

                                    fs2.landedSigStrikes = Convert.ToInt32(sigStrikesArr2[0]);
                                    fs2.totalSigStrikes = Convert.ToInt32(sigStrikesArr2[1]);

                                    string totStrikesStr = stats[8].TextContent.Replace("\n", "").Trim();
                                    string[] totStrikesArr = totStrikesStr.Split(" of ");

                                    fs1.landedStrikes = Convert.ToInt32(totStrikesArr[0]);
                                    fs1.totalStrikes = Convert.ToInt32(totStrikesArr[1]);

                                    string totStrikesStr2 = stats[9].TextContent.Replace("\n", "").Trim();
                                    string[] totStrikesArr2 = totStrikesStr2.Split(" of ");

                                    fs2.landedStrikes = Convert.ToInt32(totStrikesArr2[0]);
                                    fs2.totalStrikes = Convert.ToInt32(totStrikesArr2[1]);

                                    string takeDownsStr = stats[10].TextContent.Replace("\n", "").Trim();
                                    string[] takeDownsArr = takeDownsStr.Split(" of ");

                                    fs1.takedowns = Convert.ToInt32(takeDownsArr[0]);
                                    fs1.takedownAttempts = Convert.ToInt32(takeDownsArr[1]);

                                    string takeDownsStr2 = stats[11].TextContent.Replace("\n", "").Trim();
                                    string[] takeDownsArr2 = takeDownsStr2.Split(" of ");

                                    fs2.takedowns = Convert.ToInt32(takeDownsArr2[0]);
                                    fs2.takedownAttempts = Convert.ToInt32(takeDownsArr2[1]);


                                    fs1.subAtt = Convert.ToInt32(stats[14].TextContent.Replace("\n", "").Trim());
                                    fs2.subAtt = Convert.ToInt32(stats[15].TextContent.Replace("\n", "").Trim());


                                    fs1.rev = Convert.ToInt32(stats[16].TextContent.Replace("\n", "").Trim());
                                    fs2.rev = Convert.ToInt32(stats[17].TextContent.Replace("\n", "").Trim());


                                    fs1.ctrl = stats[18].TextContent.Replace("\n", "").Trim();
                                    fs2.ctrl = stats[19].TextContent.Replace("\n", "").Trim();
                                    currRd++;
                                }

                                if (y != 0)
                                {


                                }
                                fightStats.Add(fs1);
                                fightStats.Add(fs2);

                            }
                            else
                            {
                                if (!(statsEl.InnerHtml.Contains("Distance")))
                                {
                                    ss1.round = currRd;
                                    ss2.round = currRd;

                                    var stats = statsEl.SelectNodes("//p[@class='b-fight-details__table-text']");
                                    string headStrikesStr = stats[46].TextContent.Replace("\n", "").Trim();
                                    string[] headStrikesArr = headStrikesStr.Split(" of ");

                                    ss1.headStrikesLanded = Convert.ToInt32(headStrikesArr[0]);
                                    ss1.headStrikesAttempted = Convert.ToInt32(headStrikesArr[1]);

                                    string headStrikesStr2 = stats[47].TextContent.Replace("\n", "").Trim();
                                    string[] headStrikesArr2 = headStrikesStr2.Split(" of ");

                                    ss2.headStrikesLanded = Convert.ToInt32(headStrikesArr2[0]);
                                    ss2.headStrikesAttempted = Convert.ToInt32(headStrikesArr2[1]);

                                    currRd++;
                                }
                                sigStrikes.Add(ss1);
                                sigStrikes.Add(ss2);

                            }
                        }


                    }

                }
            }
            catch (Exception ex)
            {
                var xf = ex.Message;
            }


            var sx = "";// updateDatabase(cards, fights, fightStats, sigStrikes);
        }
    }
}
