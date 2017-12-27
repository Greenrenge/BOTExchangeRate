//////////////////////////////////////////////////////////////
///// Created by Sorasak S. (greenrenge@gmail.com)
/////////////////////////////////////////////////////////////
//// Log file : Log4net_DEBUG.txt
///  Database : JsonLog.json
///  Config file : BOTExchangeRate.exe.config
/////////////////////////////////////////////////////

////////////////////////////////////////////////////
/// HOW IT WORKS
///////////////////////////////////////////////////
Automatic BOT ExchangeRate send through SAP by BAPI
Overview : 
	- It uses Json as Database for tracking exchange rate (for API call / SAP call tracking)
	- In public hoilday, BOT will not provide the data(DB both Buy and Sell value are 0),so this program will try to call last known rate for that day and keep it in (Sell_SAP and Buy_SAP value), this is called "Patching"
	- Patching is done by only current sync currency is configured on execution time.
	- After API calling is made successfully, It will then find out all non-sync SAP currencies data in DB again and collect them all for SAP calling (only configured sync currency pairs) 
	- SAP is then call and SAP will send EXCEL through persons who responsible for this task, and if no exception is found then program will mark it as completed.
	- if program found that there is missing days running program (from today compare with the last log date) and it will recovery date automatically

////////////////////////////////////////////////////
///  Config Detail
///////////////////////////////////////////////////
1.BOTServiceEndPoint  =  https://iapi.bot.or.th/Stat/Stat-ExchangeRate/DAILY_AVG_EXG_RATE_V1/ 
Description : End point of the BOT RESTful API (found in BOT Website)

2.BOTAPIKey  =  U9G1L457H6DCugT7VmBaEacbHV9RX0PySO05cYaGsm
Description : API Key for calling RESTful API  (found in BOT Website)

3.SyncCurrency  =  USD,NZD
Description : Currency pairs we want to sync (delimited by comma [,] and all pair is to THB)

4.CurrencyRatio = JPY=100
Description : Ratio set in SAP (for now all currencies is 1 but JPY is 100) (delimited by comma [,])

5.BOTHourUpdate  =  18
Description : Time of updating BOT service (18:00 each day) 

6.BuyValue  =  buying_sight
Description : what property in BOT response will be SAP Buy value (select one from buying_sight,buying_transfer,selling,mid_rate)

7.SellValue  =  selling
Description : what property in BOT response will be SAP Sell value (select one from buying_sight,buying_transfer,selling,mid_rate)

8.RecoveryMode  =  false
Description : Normally if Recovery Mode is "false", when program is executed it will look for today (if execution time exceed time 18.00) or yesterday (if execution time is before time 18.00) 
	     if Recovery Mode is "true", it will read "RecoveryDate" and runs for those days' value, override today value.
    
9.RecoveryDate = 23/12/2017,24/12/2017
Description : use for Recovery Mode =  "true",Format is d/MM/yyyy,d/MM/yyyy (delimited by comma [,]) 
    
    
10.LOG4NET_DEBUG  =  .\Log4net_DEBUG.txt
Description : Path for debugging log

11.LOG4NET_ERROR  =  .\Log4net_ERROR.txt
Description : Path for error log

12.JsonLog  =  .\JsonLog.json
Description : Path for Database json file