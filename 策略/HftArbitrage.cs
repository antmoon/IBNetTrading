using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;
using UsStockTime;

namespace IBNetTrading.Strategy
{
    class HftArbitrage : TradingInterface
    {
        Contract A;//假定为LI
        Contract B;//假定为NIO
        Dictionary<Contract, Dictionary<DateTime, bar>> barsList = new Dictionary<Contract, Dictionary<DateTime, bar>>();
        Dictionary<Contract, List<double>> closePrices = new Dictionary<Contract, List<double>>();
        Dictionary<Contract, double> averagePrice = new Dictionary<Contract, double>();
        Dictionary<Contract, double> closeStd = new Dictionary<Contract, double>();

        double usdAmount = 10000;

        double volatility_ratio = 0;
        double beta = 0;
        int barsMinute;

        public HftArbitrage(string tickerA, string tickerB, string secType, IBClient ibclient,int minuteSize) : base(ibclient)
        {
            barsMinute = minuteSize;
            switch (secType)
            {
                case "STK":
                    A = CreateUSStockContractSmartRoute(tickerA);
                    B = CreateUSStockContractSmartRoute(tickerB);
                    addcontract(A);
                    addcontract(B);
                    break;
                case "HKSTK":
                    A = CreateHKStockContract(tickerA);
                    B = CreateHKStockContract(tickerB);
                    break;
                case "FX":
                    A = CreateFXContract(tickerA);
                    B = CreateFXContract(tickerB);
                    break;
                case "FUT":
                    int currentMonth = DateTime.Now.Month;
                    int currentYear = DateTime.Now.Year;
                    string EndTime = null;
                    if (currentMonth <= 3)
                        EndTime = new DateTime(currentYear, 3, 30).ToString("yyyyMM");
                    if (currentMonth > 3 && currentMonth <= 6)
                        EndTime = new DateTime(currentYear, 6, 30).ToString("yyyyMM");
                    if (currentMonth > 6 && currentMonth <= 9)
                        EndTime = new DateTime(currentYear, 9, 30).ToString("yyyyMM");
                    if (currentMonth > 9 && currentMonth <= 12)
                        EndTime = new DateTime(currentYear, 12, 30).ToString("yyyyMM");
                    createFutureContract(tickerA, EndTime);
                    createFutureContract(tickerB, EndTime);
                    break;
                default:
                    throw new Exception("未知的证券类型！");
            }

        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);

            barsList.Add(contract, new Dictionary<DateTime, bar>());
            closePrices.Add(contract, new List<double>());
            averagePrice.Add(contract, new double());
            closeStd.Add(contract, new double());

            String queryTime = USStockTime.AMESNow.AddMinutes(-20).ToString("yyyyMMdd HH:mm:ss");
            ibclient.ClientSocket.reqHistoricalData(requestId[contract], contract, queryTime, "1200 S", "1 min", "TRADES", 1, 1, null);
        }
        public override void HisticalData(int reqId, string date, double open, double high, double low, double close, int volume, int count, double WAP, bool hasGapsa)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;

            closePrices[contractIDrecord[reqId]].Add(close);
          
        }
        public override void HistoricalDataEnd(int reqId, string startDate, string endDate)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;

            averagePrice[contractIDrecord[reqId]] = closePrices[contractIDrecord[reqId]].Average();
            closeStd[contractIDrecord[reqId]] = CalculateStdDev(closePrices[contractIDrecord[reqId]]);

            Console.WriteLine(contractIDrecord[reqId] +"，开始时间："+ startDate+ " ，平均价格：" + averagePrice[contractIDrecord[reqId]]);
            Console.WriteLine(contractIDrecord[reqId] +"，结束时间："+endDate+ " ，标准差：" + closeStd[contractIDrecord[reqId]]);
            ibclient.ClientSocket.cancelHistoricalData(reqId);//得到数据后，取消请求
            ibclient.ClientSocket.reqMktData(reqId, contractIDrecord[reqId], "", false, new List<TagValue>());
        }

        public override void OnTrade(Contract contract)
        {
            double currentPrice = contractPrice[contract].Last();
            DateTime currentBarTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute - DateTime.Now.Minute % barsMinute, 0);

            if (!barsList[contract].ContainsKey(currentBarTime))//第一个价格，open
            {
                //不包含只有两种情况，此时这是第一个，或者是新bar的开端
                //如果不是第一个，预示着上一个bar的结束
                if (barsList[contract].Count > 0)
                {
                    this.onCandle(contract);
                }

                barsList[contract].Add(currentBarTime, new bar(currentPrice, currentPrice, currentPrice, currentPrice, currentPrice, currentPrice));

            }
            else if (barsList[contract].ContainsKey(currentBarTime))
            {
                barsList[contract][currentBarTime].high = Math.Max(barsList[contract][currentBarTime].high, currentPrice);
                barsList[contract][currentBarTime].low = Math.Min(currentPrice, barsList[contract][currentBarTime].low);
                barsList[contract][currentBarTime].close = currentPrice;
            }
        }

        public override void onCandle(Contract contract)
        {
            closePrices[contract].RemoveAt(0);
            closePrices[contract].Add(barsList[contract].Last().Value.close);

            averagePrice[contract] = closePrices[contract].Average();
            closeStd[contract] = CalculateStdDev(closePrices[contract]);

            volatility_ratio = closeStd[A] / closeStd[B];
            beta = averagePrice[A] / averagePrice[B];
            Console.WriteLine("volatility_ratio:" + volatility_ratio);
            Console.WriteLine("beta:" + beta);
        }
        public override void OnQuoteSize(Contract contract)
        {
            if (barsList[A].Count>0 && barsList[B].Count>0)
            {
                double currentPriceA = contractPrice[A].Last();
                double currentPriceB = contractPrice[B].Last();
                double expectedpriceA = currentPriceB * beta;

                if (currentPriceA>expectedpriceA && volatility_ratio>1)
                {//买A卖B
                    if (contractPosition[A].holding<=0 && !buyPlaced[A])
                    {
                        Order buy = new Order();
                        buy.Action = "BUY";
                        buy.TotalQuantity = Math.Round(usdAmount/currentPriceA,0);
                        buy.Transmit = true;
                        buy.OrderType = "MKT";
                        buy.Hidden = true;
                        buy.OutsideRth = true;
                      //  buy.Tif = "IOC";
                       // buy.LmtPrice = Math.Round(currentPriceA, 2);
                        PlaceOrder(A, buy);
                        buyPlaced[A] = true;
                    }
                    if (contractPosition[B].holding>=0 && !sellPlaced[B])
                    {
                        Order sellOrder = new Order();
                        sellOrder.Action = "SELL";
                        sellOrder.TotalQuantity =Math.Round(usdAmount / currentPriceB, 0);
                        sellOrder.OrderType = "MKT";
                        sellOrder.Transmit = true;
                      //  sellOrder.LmtPrice = Math.Round(currentPriceB, 2);
                        sellOrder.Hidden = true;
                        sellOrder.OutsideRth = true;
                       // sellOrder.Tif = "IOC";
                        PlaceOrder(B, sellOrder);
                        sellPlaced[B] = true;
                    }
                }
                else if (currentPriceA < expectedpriceA && volatility_ratio < 1)
                {// 买B卖A
                    if (contractPosition[B].holding <= 0 && !buyPlaced[B])
                    {
                        Order buy = new Order();
                        buy.Action = "BUY";
                        buy.TotalQuantity = Math.Round(usdAmount / currentPriceB, 0);
                        buy.Transmit = true;
                        buy.OrderType = "MKT";
                        buy.Hidden = true;
                        buy.OutsideRth = true;
                       // buy.Tif = "IOC";
                       // buy.LmtPrice = Math.Round(currentPriceB, 2);
                        PlaceOrder(B, buy);
                        buyPlaced[B] = true;
                    }
                    if (contractPosition[A].holding >= 0 && !sellPlaced[A])
                    {
                        Order sellOrder = new Order();
                        sellOrder.Action = "SELL";
                        sellOrder.TotalQuantity = Math.Round(usdAmount / currentPriceA, 0);
                        sellOrder.OrderType = "MKT";
                        sellOrder.Transmit = true;
                       // sellOrder.LmtPrice = Math.Round(currentPriceA, 2);
                        sellOrder.Hidden = true;
                        sellOrder.OutsideRth = true;
                       // sellOrder.Tif = "IOC";
                        PlaceOrder(A, sellOrder);
                        sellPlaced[A] = true;
                    }
                }
            }
        }

        private Contract createFutureContract(string ticker, string EndTime)
        {
            Contract fut = new Contract();
            fut.Symbol = ticker;
            fut.LastTradeDateOrContractMonth = EndTime;
            fut.SecType = "FUT";
            fut.Exchange = "GLOBEX";
            fut.Currency = "USD";
            return fut;
        }
        private double CalculateStdDev(IEnumerable<double> values)//计算标准差
        {
            double ret = 0;
            if (values.Count() > 0)
            {
                //Compute the Average      
                double avg = values.Average();
                //Perform the Sum of (value-avg)_2_2      
                double sum = values.Sum(d => Math.Pow(d - avg, 2));
                //Put it all together      
                ret = Math.Sqrt((sum) / (values.Count() - 1));
            }
            return ret;
        }
    }
}
