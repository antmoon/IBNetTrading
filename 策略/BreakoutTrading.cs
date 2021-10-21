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
    class BreakoutTrading : TradingInterface
    {//只要当前价是最高价，就下一个买单，IOC，实际运行，发现赔钱

        Dictionary<Contract, bool> barReady = new Dictionary<Contract, bool>();
       
        double K = 0.1;
        private Dictionary<Contract, double> buySellRange = new Dictionary<Contract, double>();
        private Dictionary<Contract, double> buyBreak = new Dictionary<Contract, double>();
        private Dictionary<Contract, double> sellBreak = new Dictionary<Contract, double>();
        private Dictionary<Contract, double> Highest = new Dictionary<Contract, double>();
        private Dictionary<Contract, double> Lowest = new Dictionary<Contract, double>();

        Order buyOrder = null;
        Order sellOrder = null;


        public BreakoutTrading(IBClient ibclient) : base(ibclient)
        {         
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            buySellRange.Add(contract, new double());
            buyBreak.Add(contract, new double());
            sellBreak.Add(contract, new double());
            Highest.Add(contract, 0);
            Lowest.Add(contract, 0);

            String queryTime = USStockTime.AMESNow.AddMinutes(-20).ToString("yyyyMMdd HH:mm:ss");
            Console.WriteLine("EndTime:" + queryTime);
            ibclient.ClientSocket.reqHistoricalData(requestId[contract], contract, queryTime, "20 D", "1 day", "TRADES", 1, 1, null);

            //ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
        }




        protected override void OnRemovecontract(Contract contract)
        {
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }

        public override void HisticalData(int reqId, string date, double open, double high, double low, double close, int volume, int count, double WAP, bool hasGapsa)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            buySellRange[contractIDrecord[reqId]] = Math.Round((high - low) * K, 2);
          

        }
        public override void HistoricalDataEnd(int reqId, string startDate, string endDate)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            
            ibclient.ClientSocket.reqMktData(reqId, contractIDrecord[reqId], "", false, new List<TagValue>());
         
        }
        public override void OnTrade(Contract contract)
        {
            double currentPrice = contractPrice[contract].Last();
            if (buyBreak[contract]==0)
                buyBreak[contract] = currentPrice + buySellRange[contract];
            if (sellBreak[contract]==0)
                sellBreak[contract] = currentPrice - buySellRange[contract];

            Highest[contract] = Math.Max(Highest[contract], currentPrice);
            if (Lowest[contract] == 0)
                Lowest[contract] = currentPrice;
            else
                Lowest[contract] = Math.Min(Lowest[contract], currentPrice);

            if (contractPosition[contract].holding == 0)
            {
                if (currentPrice > buyBreak[contract] && Lowest[contract]<sellBreak[contract])
                {
                    buyOrder = new Order();
                    buyOrder.Action = "BUY";
                    buyOrder.TotalQuantity = 200;
                    buyOrder.OrderType = "LMT";
                    buyOrder.Transmit = true;
                    buyOrder.LmtPrice = currentPrice;
                    buyOrder.Hidden = true;
                    buyOrder.Tif = "IOC";
                    PlaceOrder(contract, buyOrder);
                }
                else if (currentPrice<sellBreak[contract] && Highest[contract]>buyBreak[contract])
                {
                    sellOrder = new Order();
                    sellOrder.Action = "SELL";
                    sellOrder.TotalQuantity = 200;
                    sellOrder.OrderType = "LMT";
                    sellOrder.Tif = "IOC";
                    sellOrder.Transmit = true;
                    sellOrder.LmtPrice = currentPrice;
                    sellOrder.Hidden = true;
                    PlaceOrder(contract, sellOrder);
                }
            }
           
        }

        protected override void TakeCareOfTime()
        {
            //什么也不做，收盘后不平仓。父类默认是平仓的。
        }




    }
}



