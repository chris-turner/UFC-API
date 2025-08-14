using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.XPath;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace UFCDataImport
{
    public class DataImport
    {
        public static async Task GetUFCStatsAsync()
        {
            var config = Configuration.Default.WithDefaultLoader();
            var context = BrowsingContext.New(config);

            string startURL = "http://ufcstats.com/statistics/events/completed?page=all";
            var document = await context.OpenAsync(startURL);

            var fightCardEls = document.Body.SelectNodes("//a[contains(@href, 'http://ufcstats.com/event-details/')]");
            if (fightCardEls == null || fightCardEls.Count == 0) return;

            var cards = new List<FightCard>();
            var fights = new List<Fight>();
            var fightStats = new List<FightStats>();
            var sigStrikes = new List<SignificantStrikes>();

            int fightCounter = 1;

            try
            {
                int count = 10; // value for debugging
                //int count = fightCardEls.Count; // full run

                for (int i = 0; i < count; i++)
                {
                    var eventLink = (IHtmlAnchorElement)fightCardEls[i];
                    document = await context.OpenAsync(eventLink.Href);

                    var card = new FightCard
                    {
                        fightCardID = i + 1,
                        date = SafeText(document.Body.SelectSingleNode(@"//i[contains(text(), 'Date:')]/parent::li"), "Date:"),
                        fightCardName = SafeText(document.Body.SelectSingleNode("//span[@class = 'b-content__title-highlight']")),
                        location = SafeText(document.Body.SelectSingleNode(@"//i[contains(text(), 'Location:')]/parent::li"), "Location:")
                    };
                    cards.Add(card);

                    // fights
                    var fightLinks = document.Body.SelectNodes("//a[contains(@href, 'http://ufcstats.com/fight-details/')]")
                                                  ?.OfType<IHtmlAnchorElement>()
                                                  .Select(a => a.Href)
                                                  .Distinct()
                                                  .ToList() ?? new List<string>();

                    foreach (var fightUrl in fightLinks)
                    {
                        await Task.Delay(120); // delay
                        document = await context.OpenAsync(fightUrl);

                        // fighter data / outcome
                        var fighterNameNodes = document.QuerySelectorAll("h3.b-fight-details__person-name a.b-link");
                        string f1Name = fighterNameNodes.ElementAtOrDefault(0)?.TextContent?.Trim() ?? "";
                        string f2Name = fighterNameNodes.ElementAtOrDefault(1)?.TextContent?.Trim() ?? "";

                        if (string.IsNullOrWhiteSpace(f1Name) || string.IsNullOrWhiteSpace(f2Name))
                        {
                            var fallbacks = document.QuerySelectorAll("a.b-link.b-link_style_black")
                                                    .Select(a => a.TextContent.Trim())
                                                    .Where(t => !string.IsNullOrWhiteSpace(t))
                                                    .Distinct()
                                                    .Take(2)
                                                    .ToList();
                            if (fallbacks.Count == 2)
                            {
                                if (string.IsNullOrWhiteSpace(f1Name)) f1Name = fallbacks[0];
                                if (string.IsNullOrWhiteSpace(f2Name)) f2Name = fallbacks[1];
                            }
                        }

                        var statusBadges = document.QuerySelectorAll("i.b-fight-details__person-status")
                                                   .Select(n => n.TextContent?.Trim() ?? "")
                                                   .ToList();
                        string s1 = statusBadges.ElementAtOrDefault(0) ?? "";
                        string s2 = statusBadges.ElementAtOrDefault(1) ?? "";

                        var fight = new Fight
                        {
                            fightCard = card,
                            fightID = fightCounter++,
                            fighter1 = f1Name,
                            fighter2 = f2Name,
                            fighter1Outcome = (s1 == "W") ? "Win" : (s1 == "L") ? "Loss" : s1,
                            fighter2Outcome = (s2 == "W") ? "Win" : (s2 == "L") ? "Loss" : s2,
                            bonus = "None",
                            weightClass = (document.QuerySelector("i.b-fight-details__fight-title")?.TextContent ?? "").Replace(" Bout", "").Trim(),
                            method = SafeText(document.Body.SelectSingleNode(@"//i[contains(text(),'Method:')]/following::i[1]")),
                            roundFinished = SafeText(document.Body.SelectSingleNode(@"//i[contains(text(),'Round:')]/parent::i"), "Round:"),
                            timeFinished = SafeText(document.Body.SelectSingleNode(@"//i[contains(text(),'Time:')]/parent::i"), "Time:"),
                            format = SafeText(document.Body.SelectSingleNode(@"//i[contains(text(),'Time format:') or contains(text(),'Time:')]/following::i[1]"), "Time format:"),
                            referee = SafeText(document.Body.SelectSingleNode(@"//i[contains(text(),'Referee:')]/parent::i"), "Referee:"),
                            details = SafeText(document.Body.SelectSingleNode(@"//i[contains(text(),'Details:')]/parent::i/parent::p"), "Details:")
                        };

                        var bonusImg = document.QuerySelector("i.b-fight-details__fight-title img");
                        if (bonusImg != null)
                        {
                            var src = bonusImg.GetAttribute("src") ?? "";
                            if (src.Contains("/")) fight.bonus = src[(src.LastIndexOf('/') + 1)..].Replace(".png", "");
                        }

                        fights.Add(fight);

                        // fight totals
                        var totalsTable = document.QuerySelectorAll("section.b-fight-details__section table")
                                                  .OfType<IHtmlTableElement>()
                                                  .FirstOrDefault(t => t.QuerySelector("thead.b-fight-details__table-head") != null);
                        if (totalsTable != null)
                        {
                            var row = totalsTable.QuerySelector("tbody tr");
                            if (row != null)
                            {
                                var ps = row.QuerySelectorAll("p.b-fight-details__table-text").OfType<Element>().ToList();
                                // Columns
                                // 0=Fighter, 1=KD, 2=Sig. str., 3=Sig. str. %, 4=Total str., 5=Td, 6=Td %, 7=Sub. att, 8=Rev., 9=Ctrl
                                var fs1 = new FightStats { fight = fight, fighterName = f1Name, round = 0 };
                                var fs2 = new FightStats { fight = fight, fighterName = f2Name, round = 0 };

                                fs1.KD = GetIntFromPairCell(ps, 1, true);
                                fs2.KD = GetIntFromPairCell(ps, 1, false);

                                var (s1L, s1A) = GetPairFromPairCell(ps, 2, true);
                                var (s2L, s2A) = GetPairFromPairCell(ps, 2, false);
                                fs1.landedSigStrikes = s1L; fs1.totalSigStrikes = s1A;
                                fs2.landedSigStrikes = s2L; fs2.totalSigStrikes = s2A;

                                var (t1L, t1A) = GetPairFromPairCell(ps, 4, true);
                                var (t2L, t2A) = GetPairFromPairCell(ps, 4, false);
                                fs1.landedStrikes = t1L; fs1.totalStrikes = t1A;
                                fs2.landedStrikes = t2L; fs2.totalStrikes = t2A;

                                var (td1L, td1A) = GetPairFromPairCell(ps, 5, true);
                                var (td2L, td2A) = GetPairFromPairCell(ps, 5, false);
                                fs1.takedowns = td1L; fs1.takedownAttempts = td1A;
                                fs2.takedowns = td2L; fs2.takedownAttempts = td2A;

                                fs1.subAtt = GetIntFromPairCell(ps, 7, true);
                                fs2.subAtt = GetIntFromPairCell(ps, 7, false);
                                fs1.rev = GetIntFromPairCell(ps, 8, true);
                                fs2.rev = GetIntFromPairCell(ps, 8, false);

                                fs1.ctrl = GetTextFromPairCell(ps, 9, true);
                                fs2.ctrl = GetTextFromPairCell(ps, 9, false);

                                fightStats.Add(fs1);
                                fightStats.Add(fs2);
                            }
                        }

                        // sig strikes
                        var sigTotalsTable = document.QuerySelectorAll("section.b-fight-details__section + table, section.b-fight-details__section table")
                                                     .OfType<IHtmlTableElement>()
                                                     .FirstOrDefault(t =>
                                                     {
                                                         var ths = t.QuerySelectorAll("thead.b-fight-details__table-head th.b-fight-details__table-col")
                                                                    .Select(th => Clean(th.TextContent)).ToList();
                                                         string[] must = { "Fighter", "Sig. str", "Sig. str. %", "Head", "Body", "Leg", "Distance", "Clinch", "Ground" };
                                                         return ths.Count > 0 && must.All(m => ths.Any(x => x.Equals(m, StringComparison.OrdinalIgnoreCase)));
                                                     });

                        if (sigTotalsTable != null)
                        {
                            var row = sigTotalsTable.QuerySelector("tbody tr");
                            if (row != null)
                            {
                                var ps = row.QuerySelectorAll("p.b-fight-details__table-text").OfType<Element>().ToList();
                                // Columns: 0=Fighter, 1=Sig. str, 2=Sig. str. %, 3=Head, 4=Body, 5=Leg, 6=Distance, 7=Clinch, 8=Ground
                                var ss1 = new SignificantStrikes { fight = fight, fighterName = f1Name, round = 0 };
                                var ss2 = new SignificantStrikes { fight = fight, fighterName = f2Name, round = 0 };

                                var (h1L, h1A) = GetPairFromPairCell(ps, 3, true); var (h2L, h2A) = GetPairFromPairCell(ps, 3, false);
                                ss1.headStrikesLanded = h1L; ss1.headStrikesAttempted = h1A;
                                ss2.headStrikesLanded = h2L; ss2.headStrikesAttempted = h2A;

                                var (b1L, b1A) = GetPairFromPairCell(ps, 4, true); var (b2L, b2A) = GetPairFromPairCell(ps, 4, false);
                                ss1.bodyStrikesLanded = b1L; ss1.bodyStrikesAttempted = b1A;
                                ss2.bodyStrikesLanded = b2L; ss2.bodyStrikesAttempted = b2A;

                                var (l1L, l1A) = GetPairFromPairCell(ps, 5, true); var (l2L, l2A) = GetPairFromPairCell(ps, 5, false);
                                ss1.legStrikeslanded = l1L; ss1.legStrikesAttempted = l1A;
                                ss2.legStrikeslanded = l2L; ss2.legStrikesAttempted = l2A;

                                var (d1L, d1A) = GetPairFromPairCell(ps, 6, true); var (d2L, d2A) = GetPairFromPairCell(ps, 6, false);
                                ss1.distanceStrikesLanded = d1L; ss1.distanceStrikesAttempted = d1A;
                                ss2.distanceStrikesLanded = d2L; ss2.distanceStrikesAttempted = d2A;

                                var (c1L, c1A) = GetPairFromPairCell(ps, 7, true); var (c2L, c2A) = GetPairFromPairCell(ps, 7, false);
                                ss1.clinchStrikesLanded = c1L; ss1.clinchStrikesAttempted = c1A;
                                ss2.clinchStrikesLanded = c2L; ss2.clinchStrikesAttempted = c2A;

                                var (g1L, g1A) = GetPairFromPairCell(ps, 8, true); var (g2L, g2A) = GetPairFromPairCell(ps, 8, false);
                                ss1.groundStrikesLanded = g1L; ss1.groundStrikesAttempted = g1A;
                                ss2.groundStrikesLanded = g2L; ss2.groundStrikesAttempted = g2A;

                                sigStrikes.Add(ss1);
                                sigStrikes.Add(ss2);
                            }
                        }

                        // rounds data
                        var perRoundTotalsHeads = document.QuerySelectorAll("table.b-fight-details__table.js-fight-table thead.b-fight-details__table-row.b-fight-details__table-row_type_head").OfType<Element>().ToList();
                        foreach (var head in perRoundTotalsHeads)
                        {
                            var tbody = head.NextElementSibling as IHtmlTableSectionElement;
                            if (tbody == null) continue;
                            var row = tbody.QuerySelector("tr");
                            if (row == null) continue;

                            var ps = row.QuerySelectorAll("p.b-fight-details__table-text").OfType<Element>().ToList();
                            if (ps.Count == 0) continue;

                            // Columns (header)
                            // 0=Fighter, 1=KD, 2=Sig. str., 3=Sig. str. %, 4=Total str., 5=Td, 6=Td %, 7=Sub. att, 8=Rev., 9=Ctrl
                            // Round index should start at 1 (0 is full fight totals)
                            int roundNumber = perRoundTotalsHeads.IndexOf(head) + 1;

                            var fs1 = new FightStats { fight = fight, fighterName = f1Name, round = roundNumber };
                            var fs2 = new FightStats { fight = fight, fighterName = f2Name, round = roundNumber };

                            fs1.KD = GetIntFromPairCell(ps, 1, true);
                            fs2.KD = GetIntFromPairCell(ps, 1, false);

                            var (ps1L, ps1A) = GetPairFromPairCell(ps, 2, true);
                            var (ps2L, ps2A) = GetPairFromPairCell(ps, 2, false);
                            fs1.landedSigStrikes = ps1L; fs1.totalSigStrikes = ps1A;
                            fs2.landedSigStrikes = ps2L; fs2.totalSigStrikes = ps2A;

                            var (pt1L, pt1A) = GetPairFromPairCell(ps, 4, true);
                            var (pt2L, pt2A) = GetPairFromPairCell(ps, 4, false);
                            fs1.landedStrikes = pt1L; fs1.totalStrikes = pt1A;
                            fs2.landedStrikes = pt2L; fs2.totalStrikes = pt2A;

                            var (ptd1L, ptd1A) = GetPairFromPairCell(ps, 5, true);
                            var (ptd2L, ptd2A) = GetPairFromPairCell(ps, 5, false);
                            fs1.takedowns = ptd1L; fs1.takedownAttempts = ptd1A;
                            fs2.takedowns = ptd2L; fs2.takedownAttempts = ptd2A;

                            fs1.subAtt = GetIntFromPairCell(ps, 7, true);
                            fs2.subAtt = GetIntFromPairCell(ps, 7, false);
                            fs1.rev = GetIntFromPairCell(ps, 8, true);
                            fs2.rev = GetIntFromPairCell(ps, 8, false);
                            
                            fs1.ctrl = GetTextFromPairCell(ps, 9, true);
                            fs2.ctrl = GetTextFromPairCell(ps, 9, false);

                            fightStats.Add(fs1);
                            fightStats.Add(fs2);
                        }

                        
                        var sigPerRoundTable = document.QuerySelectorAll("section.b-fight-details__section table.b-fight-details__table.js-fight-table")
                                                       .OfType<IHtmlTableElement>()
                                                       .FirstOrDefault(t =>
                                                       {
                                                           var head = t.QuerySelector("thead.b-fight-details__table-head_rnd");
                                                           if (head == null) return false;
                                                           var labels = head.QuerySelectorAll("th.b-fight-details__table-col")
                                                                            .Select(th => Clean(th.TextContent))
                                                                            .ToList();
                                                           string[] must = { "Fighter", "Sig. str", "Sig. str. %", "Head", "Body", "Leg", "Distance", "Clinch", "Ground" };
                                                           return must.All(m => labels.Any(l => l.Equals(m, StringComparison.OrdinalIgnoreCase)));
                                                       });

                        if (sigPerRoundTable != null)
                        {
                            var header = sigPerRoundTable.QuerySelector("thead.b-fight-details__table-head_rnd");
                            var headerCols = header.QuerySelectorAll("th.b-fight-details__table-col")
                                                   .Select((th, idx) => (label: Clean(th.TextContent), idx))
                                                   .ToDictionary(k => k.label, v => v.idx, StringComparer.OrdinalIgnoreCase);

                            int colHead = GetCol(headerCols, "Head");
                            int colBody = GetCol(headerCols, "Body");
                            int colLeg = GetCol(headerCols, "Leg");
                            int colDistance = GetCol(headerCols, "Distance");
                            int colClinch = GetCol(headerCols, "Clinch");
                            int colGround = GetCol(headerCols, "Ground");

                            var roundHeads = sigPerRoundTable.QuerySelectorAll("thead.b-fight-details__table-row.b-fight-details__table-row_type_head")
                                                             .OfType<Element>()
                                                             .ToList();

                            for (int idx = 0; idx < roundHeads.Count; idx++)
                            {
                                int roundNumber = idx + 1; 
                                var tbody = roundHeads[idx].NextElementSibling as IHtmlTableSectionElement;
                                if (tbody == null) continue;
                                var row = tbody.QuerySelector("tr");
                                if (row == null) continue;

                                var pCells = row.QuerySelectorAll("p.b-fight-details__table-text").OfType<Element>().ToList();
                                if (pCells.Count == 0) continue;

                                (int L, int A) ReadPair(int col, bool first) => ParseOfPair(GetTextFromPairCell(pCells, col, first));

                                var ss1 = new SignificantStrikes { fight = fight, fighterName = f1Name, round = roundNumber };
                                var ss2 = new SignificantStrikes { fight = fight, fighterName = f2Name, round = roundNumber };

                                var (h1L, h1A) = ReadPair(colHead, true); var (h2L, h2A) = ReadPair(colHead, false);
                                ss1.headStrikesLanded = h1L; ss1.headStrikesAttempted = h1A;
                                ss2.headStrikesLanded = h2L; ss2.headStrikesAttempted = h2A;

                                var (b1L, b1A) = ReadPair(colBody, true); var (b2L, b2A) = ReadPair(colBody, false);
                                ss1.bodyStrikesLanded = b1L; ss1.bodyStrikesAttempted = b1A;
                                ss2.bodyStrikesLanded = b2L; ss2.bodyStrikesAttempted = b2A;

                                var (l1L, l1A) = ReadPair(colLeg, true); var (l2L, l2A) = ReadPair(colLeg, false);
                                ss1.legStrikeslanded = l1L; ss1.legStrikesAttempted = l1A;
                                ss2.legStrikeslanded = l2L; ss2.legStrikesAttempted = l2A;

                                var (d1L, d1A) = ReadPair(colDistance, true); var (d2L, d2A) = ReadPair(colDistance, false);
                                ss1.distanceStrikesLanded = d1L; ss1.distanceStrikesAttempted = d1A;
                                ss2.distanceStrikesLanded = d2L; ss2.distanceStrikesAttempted = d2A;

                                var (c1L, c1A) = ReadPair(colClinch, true); var (c2L, c2A) = ReadPair(colClinch, false);
                                ss1.clinchStrikesLanded = c1L; ss1.clinchStrikesAttempted = c1A;
                                ss2.clinchStrikesLanded = c2L; ss2.clinchStrikesAttempted = c2A;

                                var (g1L, g1A) = ReadPair(colGround, true); var (g2L, g2A) = ReadPair(colGround, false);
                                ss1.groundStrikesLanded = g1L; ss1.groundStrikesAttempted = g1A;
                                ss2.groundStrikesLanded = g2L; ss2.groundStrikesAttempted = g2A;

                                sigStrikes.Add(ss1);
                                sigStrikes.Add(ss2);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var _ = ex.ToString(); 
            }

            UpdateDatabase(cards, fights, fightStats, sigStrikes);
        }

        private static void UpdateDatabase(List<FightCard> cards, List<Fight> fights, List<FightStats> fightStats, List<SignificantStrikes> sigStrikes)
        {
            //writing to json files for now

            var outputRoot = Path.Combine(@"C:\Users\Chris\Desktop\Code\UFCDB");
            Directory.CreateDirectory(outputRoot);

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // Cards
            var cardDtos = cards
                .Select(c => new FightCardDto
                {
                    fightCardID = c.fightCardID,
                    date = c.date,
                    fightCardName = c.fightCardName,
                    location = c.location
                })
                .OrderBy(c => c.fightCardID).ToList();
            WriteJson(outputRoot, "cards.json", cardDtos, jsonOptions);

            // Fights
            var fightDtos = fights
                .Select(f => new FightDto
                {
                    fightID = f.fightID,
                    fightCardID = f.fightCard?.fightCardID ?? 0,
                    fighter1 = f.fighter1,
                    fighter1Outcome = f.fighter1Outcome,
                    fighter2 = f.fighter2,
                    fighter2Outcome = f.fighter2Outcome,
                    bonus = f.bonus,
                    weightClass = f.weightClass,
                    method = f.method,
                    roundFinished = f.roundFinished,
                    timeFinished = f.timeFinished,
                    format = f.format,
                    referee = f.referee,
                    details = f.details
                })
                .OrderBy(f => f.fightID).ToList();
            WriteJson(outputRoot, "fights.json", fightDtos, jsonOptions);

            // FightStats
            var fsDtos = fightStats
                .Select(s => new FightStatsDto
                {
                    fightID = s.fight?.fightID ?? 0,
                    fighterName = s.fighterName,
                    round = s.round,
                    KD = s.KD,
                    landedSigStrikes = s.landedSigStrikes,
                    totalSigStrikes = s.totalSigStrikes,
                    landedStrikes = s.landedStrikes,
                    totalStrikes = s.totalStrikes,
                    takedowns = s.takedowns,
                    takedownAttempts = s.takedownAttempts,
                    subAtt = s.subAtt,
                    rev = s.rev,
                    ctrl = s.ctrl
                })
                .OrderBy(s => s.fightID).ThenBy(s => s.round).ThenBy(s => s.fighterName)
                .ToList();
            WriteJson(outputRoot, "fightStats.json", fsDtos, jsonOptions);

            // Significant Strikes
            var ssDtos = sigStrikes
                .Select(s => new SignificantStrikesDto
                {
                    fightID = s.fight?.fightID ?? 0,
                    fighterName = s.fighterName,
                    round = s.round,
                    headStrikesLanded = s.headStrikesLanded,
                    headStrikesAttempted = s.headStrikesAttempted,
                    bodyStrikesLanded = s.bodyStrikesLanded,
                    bodyStrikesAttempted = s.bodyStrikesAttempted,
                    legStrikeslanded = s.legStrikeslanded,
                    legStrikesAttempted = s.legStrikesAttempted,
                    distanceStrikesLanded = s.distanceStrikesLanded,
                    distanceStrikesAttempted = s.distanceStrikesAttempted,
                    clinchStrikesLanded = s.clinchStrikesLanded,
                    clinchStrikesAttempted = s.clinchStrikesAttempted,
                    groundStrikesLanded = s.groundStrikesLanded,
                    groundStrikesAttempted = s.groundStrikesAttempted
                })
                .OrderBy(s => s.fightID).ThenBy(s => s.round).ThenBy(s => s.fighterName)
                .ToList();
            WriteJson(outputRoot, "significantStrikes.json", ssDtos, jsonOptions);

            var index = new
            {
                generatedUtc = DateTime.UtcNow,
                counts = new
                {
                    cards = cardDtos.Count,
                    fights = fightDtos.Count,
                    fightStats = fsDtos.Count,
                    significantStrikes = ssDtos.Count
                }
            };
            WriteJson(outputRoot, "_index.json", index, jsonOptions);
        }

        // DTOs
        private sealed class FightCardDto
        {
            public int fightCardID { get; set; }
            public string date { get; set; }
            public string fightCardName { get; set; }
            public string location { get; set; }
        }

        private sealed class FightDto
        {
            public int fightID { get; set; }
            public int fightCardID { get; set; }
            public string fighter1 { get; set; }
            public string fighter1Outcome { get; set; }
            public string fighter2 { get; set; }
            public string fighter2Outcome { get; set; }
            public string bonus { get; set; }
            public string weightClass { get; set; }
            public string method { get; set; }
            public string roundFinished { get; set; }
            public string timeFinished { get; set; }
            public string format { get; set; }
            public string referee { get; set; }
            public string details { get; set; }
        }

        private sealed class FightStatsDto
        {
            public int fightID { get; set; }
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
            public int rev { get; set; }
            public string ctrl { get; set; }
        }

        private sealed class SignificantStrikesDto
        {
            public int fightID { get; set; }
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

        // helper methods
        private static void WriteJson<T>(string folder, string fileName, T data, JsonSerializerOptions opts)
        {
            var path = Path.Combine(folder, fileName);
            var json = JsonSerializer.Serialize(data, opts);
            File.WriteAllText(path, json);
        }

        private static string SafeText(INode node, string trimPrefix = null)
        {
            if (node == null) return "";
            var t = (node as Element)?.TextContent?.Replace("\n", "").Trim() ?? "";
            if (!string.IsNullOrEmpty(trimPrefix))
                t = t.Replace(trimPrefix, "").Trim();
            return t;
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var noNewlines = s.Replace("\n", " ").Replace("\r", " ").Trim();
            return Regex.Replace(noNewlines, @"\s+", " ");
        }

        private static int GetCol(Dictionary<string, int> map, string key)
        {
            return map.TryGetValue(key, out var idx) ? idx : -1;
        }

        // read table cells where each column has two <p> tags: one for fighter 1, one for fighter 2.
        
        // returns the text for the chosen fighter in the given column.
        private static string GetTextFromPairCell(List<Element> flatPs, int colIndex, bool first)
        {
            // Each column contributes two <p> → position = colIndex * 2 + (0 or 1)
            int pIndex = colIndex * 2 + (first ? 0 : 1);
            if (pIndex < 0 || pIndex >= flatPs.Count) return "";
            return Clean(flatPs[pIndex].TextContent);
        }

        private static int GetIntFromPairCell(List<Element> flatPs, int colIndex, bool first)
        {
            var txt = GetTextFromPairCell(flatPs, colIndex, first);
            return int.TryParse(txt, out var v) ? v : 0;
        }

        private static (int landed, int attempted) GetPairFromPairCell(List<Element> flatPs, int colIndex, bool first)
        {
            var txt = GetTextFromPairCell(flatPs, colIndex, first);
            return ParseOfPair(txt);
        }

        // parses landed/attempted from string
        private static (int landed, int attempted) ParseOfPair(string s)
        {
            var parts = s.Split(new[] { " of " }, StringSplitOptions.None);
            if (parts.Length == 2
                && int.TryParse(parts[0].Trim(), out var landed)
                && int.TryParse(Regex.Match(parts[1], @"\d+").Value, out var attempted))
            {
                return (landed, attempted);
            }

            var m = Regex.Matches(s, @"\d+");
            if (m.Count >= 2)
            {
                int.TryParse(m[0].Value, out var l);
                int.TryParse(m[1].Value, out var a);
                return (l, a);
            }
            return (0, 0);
        }
    }
}
