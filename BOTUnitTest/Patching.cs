using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BOTExchangeRate;
using System.Collections.Generic;

namespace BOTUnitTest
{
    [TestClass]
    public class Patching
    {
        [TestMethod]
        public void APIExecutePatchingTesting()
        {
            List<DailyLog> testLog = new List<DailyLog>
            {
                { new DailyLog
                    {
                        Date = new DateTime(2018,1,6),
                        CurrenciesRate = new List<CurrencyRate>
                        {
                            {
                                new CurrencyRate
                                {
                                    Date =  new DateTime(2018,1,6),
                                    Currency = "USD",
                                    Buy = 0,
                                    Sell = 0,
                                    isAPIComplete = true,
                                    Buy_SAP = 0,
                                    Sell_SAP = 0,
                                    isSyncSAP = false
                                }
                            }
                        }
                    }
                },
                { new DailyLog
                    {
                        Date = new DateTime(2018,1,7),
                        CurrenciesRate = new List<CurrencyRate>
                        {
                            {
                                new CurrencyRate
                                {
                                    Date =  new DateTime(2018,1,6),
                                    Currency = "USD",
                                    Buy = 0,
                                    Sell = 0,
                                    isAPIComplete = true,
                                    Buy_SAP = 0,
                                    Sell_SAP = 0,
                                    isSyncSAP = false
                                }
                            }
                        }
                    }
                }
            };

            var testCurrency = new List<string> { "USD" };
            try
            {
                BOTExchangeRate.Program.Patching(testCurrency, testLog);
            }
            catch(Exception ex)
            {
                new AssertFailedException("exception occured", ex);
            }
            
        }
    }
}
