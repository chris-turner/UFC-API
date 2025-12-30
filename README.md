# UFC API
 UFC Web scraper, database, and API
 
 Scrapes UFC/MMA statistics into an MSSQL database and makes them accessible via an API, returning the data in JSON.

# API Methods

# Fight Cards
GET<br/>
/fightcards
<br/>
Returns all fight cards and IDs

/fightcards/{fightCardID}<br/>
Returns fight card(s) for supplied ID(s)<br/>
Multiple IDs can be separated by commas
<br/><br/>
/fightcards/fights/{fightCardID}<br/>
Returns fights for card for supplied ID<br/>

![fightcards](https://i.imgur.com/BaT1shA.png)


# Fight
GET<br/>
/fights<br/>
Returns all fights and IDs<br/><br/>

GET<br/>
/fights/{fightID}<br/>
Returns fight info for supplied ID(s)<br/>
Multiple IDs can be separated by commas<br/>

![fight](https://i.imgur.com/ZALSWHo.png)


# FightStats
GET<br/>
/fightstats/{fightID}<br/>

Returns fight stats for the supplied ID<br/>

![fight](https://i.imgur.com/JCCUoBf.png)

