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
    /// <summary>
    /// 根据我的Multicharts Dualthrust回测写的日策略，非常简单，用本日的开盘价加减昨日最高减去最低的1/10，开仓和平仓
    /// </summary>
    class dualThrust:TradingInterface
    {
        double K = 0.15;      
        private Dictionary<Contract, double> buySellRange = new Dictionary<Contract, double>();
        private Dictionary<Contract, double> buyPrice = new Dictionary<Contract, double>();
        private Dictionary<Contract, double> sellPrice = new Dictionary<Contract, double>();

        Order buyOrder = null;
        Order sellOrder = null;

        Dictionary<Contract, List<double>> twentyDayCloses = new Dictionary<Contract, List<double>>();
        Dictionary<Contract, double> twentyDayAverage = new Dictionary<Contract, double>();

        public dualThrust(IBClient ibclient) : base(ibclient)
        {            
                      
        }

        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);           
            buySellRange.Add(contract, new double());
            buyPrice.Add(contract, new double());
            sellPrice.Add(contract, new double());
            twentyDayCloses.Add(contract, new List<double>());
            twentyDayAverage.Add(contract, new double());

            String queryTime = USStockTime.AMESNow.AddMinutes(-20).ToString("yyyyMMdd HH:mm:ss");
            Console.WriteLine("EndTime:" + queryTime);
            ibclient.ClientSocket.reqHistoricalData(requestId[contract], contract, queryTime, "20 D", "1 day", "TRADES", 1,1,null);

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
            buySellRange[contractIDrecord[reqId]] = Math.Round((high - low)*K,2);
            twentyDayCloses[contractIDrecord[reqId]].Add(close);
            
        }
        public override void HistoricalDataEnd(int reqId, string startDate, string endDate)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            twentyDayAverage[contractIDrecord[reqId]] = twentyDayCloses[contractIDrecord[reqId]].Average();
            ibclient.ClientSocket.reqMktData(reqId, contractIDrecord[reqId], "", false, new List<TagValue>());

            Console.WriteLine(contractIDrecord[reqId].Symbol + ",20 day moveing average:" + twentyDayAverage[contractIDrecord[reqId]] + ",buySellRange:" + buySellRange[contractIDrecord[reqId]]);
        }
        public override void OnTrade(Contract contract)
        {
            double currentPrice = contractPrice[contract].Last();
            buyPrice[contract] = currentPrice + buySellRange[contract];
            sellPrice[contract] = currentPrice - buySellRange[contract];
            Console.WriteLine(contract.Symbol + " current price:" + currentPrice + ",buyPrice:" + buyPrice[contract] + ",sellPrice:" + sellPrice[contract]);

            if (contractPosition[contract].holding==0)
            {
                if (currentPrice > twentyDayAverage[contract]&&!buyPlaced[contract])
                {
                    buyOrder = new Order();
                    buyOrder.Action = "BUY";
                    buyOrder.TotalQuantity = 200;
                    buyOrder.OrderType = "LMT";
                    buyOrder.Transmit = true;
                    buyOrder.LmtPrice =currentPrice;
                    buyOrder.Hidden = true;
                    buyOrder.Tif = "DAY";
                    buyOrder.OutsideRth = true;
                    PlaceOrder(contract, buyOrder);
                    buyPlaced[contract] = true;
                }                              
                else if (currentPrice < twentyDayAverage[contract]&&!sellPlaced[contract])
                {
                    sellOrder = new Order();
                    sellOrder.Action = "SELL";
                    sellOrder.TotalQuantity = 200;
                    sellOrder.OrderType = "LMT";
                    sellOrder.Transmit = true;
                    sellOrder.LmtPrice = currentPrice;
                    sellOrder.Tif = "DAY";
                    sellOrder.Hidden = true;
                    sellOrder.OutsideRth = true;
                    PlaceOrder(contract, sellOrder);
                    sellPlaced[contract] = true;
                }
            }
            else if (contractPosition[contract].holding>0)
            {
                if (!sellPlaced[contract])
                {
                    sellOrder = new Order();
                    sellOrder.Action = "SELL";
                    sellOrder.TotalQuantity =200;
                    sellOrder.OrderType = "STP";
                    sellOrder.Transmit = true;
                    sellOrder.AuxPrice = Math.Round(sellPrice[contract], 2);
                    sellOrder.Hidden = true;
                    sellOrder.OutsideRth = true;
                    PlaceOrder(contract, sellOrder);
                    sellPlaced[contract] = true;
                }
                
                if (!buyPlaced[contract]&&contractPosition[contract].holding<600)
                {
                    buyOrder = new Order();
                    buyOrder.Action = "BUY";
                    buyOrder.TotalQuantity = 200;
                    buyOrder.OrderType = "STP";
                    buyOrder.Transmit = true;
                    buyOrder.AuxPrice = Math.Round(buyPrice[contract], 2);
                    buyOrder.Hidden = true;
                    buyOrder.OutsideRth = true;
                    PlaceOrder(contract, buyOrder);
                    buyPlaced[contract] = true;
                }
               

            }
            else if (contractPosition[contract].holding < 0)
            {
                if (!buyPlaced[contract])
                {
                    buyOrder = new Order();
                    buyOrder.Action = "BUY";
                    buyOrder.TotalQuantity =200;
                    buyOrder.OrderType = "STP";//Market if touched
                    buyOrder.Transmit = true;
                    buyOrder.AuxPrice = Math.Round(buyPrice[contract], 2);
                    buyOrder.Hidden = true;
                    buyOrder.OutsideRth = true;
                    PlaceOrder(contract, buyOrder);
                    buyPlaced[contract] = true;
                }
                if (!sellPlaced[contract]&& contractPosition[contract].holding>-600)
                {
                    sellOrder = new Order();
                    sellOrder.Action = "SELL";
                    sellOrder.TotalQuantity = 200;
                    sellOrder.OrderType = "STP";
                    sellOrder.Transmit = true;
                    sellOrder.AuxPrice = Math.Round(sellPrice[contract], 2);
                    sellOrder.Hidden = true;
                    sellOrder.OutsideRth = true;
                    PlaceOrder(contract, sellOrder);
                    sellPlaced[contract] = true;
                }

            }

            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }

        protected override void TakeCareOfTime()
        {
            //什么也不做，收盘后不平仓。父类默认是平仓的。
        }
    }
}
