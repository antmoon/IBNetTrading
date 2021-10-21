using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;
using MathNet.Numerics.LinearRegression;

namespace IBNetTrading.Strategy
{    
    class LinearRegression : TradingInterface
    {
        
        Dictionary<Contract, List<double>> TradePrices = new Dictionary<Contract, List<double>>();
        Dictionary<Contract, List<double>> TradeTimestamp = new Dictionary<Contract, List<double>>();

        public LinearRegression(IBClient ibclient) : base(ibclient) 
        {
            numberOfData = 500;
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            TradePrices.Add(contract, new List<double>());
            TradeTimestamp.Add(contract, new List<double>());

            ibclient.ClientSocket.reqMarketDataType(2);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());//获取隐含波动率
        }
        public override void OnTradeSize(Contract contract)
        {
            TradePrices[contract].Add(contractPrice[contract].Last());
            double timeStamp = DateTime.Now.Ticks;
            TradeTimestamp[contract].Add(timeStamp);
            Console.WriteLine(contract.Symbol + " last price:" + contractPrice[contract].Last()+ ",trade size:" + contractSize[contract].Last());
            
            if (TradePrices[contract].Count > numberOfData && TradeTimestamp[contract].Count > numberOfData)
            {
                TradePrices[contract].RemoveAt(0);
                TradeTimestamp[contract].RemoveAt(0);

                double[] price = TradePrices[contract].ToArray<double>();
                double[] time = TradeTimestamp[contract].ToArray<double>();

                Tuple<double, double> p = SimpleRegression.Fit(price, time);

                Console.WriteLine(contract.Symbol + " Slope:" + p.Item2);
                if (p.Item2 > 0)
                {
                    if (contractPosition[contract].holding <= 0)
                    {
                        Order buy = new Order();
                        buy.Action = "BUY";
                        buy.TotalQuantity = 200;
                        buy.Transmit = true;
                        buy.OrderType = "LMT";
                        buy.Hidden = true;
                        buy.Tif = "IOC";
                        buy.LmtPrice = contractPrice[contract].Last();
                        PlaceOrder(contract, buy);
                        
                    }
                }
                else if (p.Item2 < 0)
                {
                    if (contractPosition[contract].holding >= 0)
                    {
                        Order sellOrder = new Order();
                        sellOrder.Action = "SELL";
                        sellOrder.TotalQuantity = 200;
                        sellOrder.OrderType = "LMT";
                        sellOrder.Transmit = true;
                        sellOrder.LmtPrice = contractPrice[contract].Last();
                        sellOrder.Hidden = true;
                        sellOrder.Tif = "IOC";
                        PlaceOrder(contract, sellOrder);
                        
                    }

                }

            }
        }
        public override void OnTrade(Contract contract)
        {
            
        }
        protected override void TakeCareOfTime()
        {
            //什么也不做，收盘后不平仓。父类默认是平仓的。
        }
    }
}
