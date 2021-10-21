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
    class ImpIntradayTrading : TradingInterface
    {
        Dictionary<Contract, double> ImpliedVolatility = new Dictionary<Contract, double>();        

        public ImpIntradayTrading(IBClient ibclient) : base(ibclient)
        {

        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
           
            ImpliedVolatility.Add(contract, 0);

            ibclient.ClientSocket.reqMarketDataType(2);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "106", false, new List<TagValue>());//获取隐含波动率

        }
        public override void TickGeneric(int tickerId, int field, double value)
        {
            if (!contractIDrecord.ContainsKey(tickerId))
                return;
            if (field == 24)
            {
                ImpliedVolatility[contractIDrecord[tickerId]] = value;
                Console.WriteLine(contractIDrecord[tickerId].Symbol + " ImpliedVolatility:" + ImpliedVolatility[contractIDrecord[tickerId]]);
            }
        }
        public override void OnTrade(Contract contract)
        {
            if (ImpliedVolatility.ContainsKey(contract) && ImpliedVolatility[contract]!=0)
            {
                TimeSpan timeSpan = usStockCloseTime.CorrespondingChinaTime.Subtract(DateTime.Now);
                double intradayRange = contractPrice[contract].Last() * ImpliedVolatility[contract] * Math.Sqrt(timeSpan.TotalDays/ 365);
                double buyLimit = Math.Round(contractPrice[contract].Last() - intradayRange, 2);
                double sellLimte = Math.Round(contractPrice[contract].Last() + intradayRange, 2);

                Order buyorder = new Order();
                buyorder.Action = "BUY";
                buyorder.TotalQuantity = 100;
                buyorder.Transmit = true;
                buyorder.OrderType = "LMT";
                buyorder.OutsideRth = true;
                buyorder.LmtPrice = buyLimit;
                buyorder.Hidden = true;
                if (!buyPlaced[contract])
                {
                    PlaceOrder(contract, buyorder);
                    buyPlaced[contract] = true;
                }

                Order sell = new Order();
                sell.Action = "SELL";
                sell.TotalQuantity = 100;
                sell.Transmit = true;
                sell.OrderType = "LMT";
                sell.OutsideRth = true;
                sell.LmtPrice = sellLimte;
                sell.Hidden = true;
                if (!sellPlaced[contract])
                {
                    PlaceOrder(contract, sell);
                    sellPlaced[contract] = true;
                }
                ibclient.ClientSocket.cancelMktData(requestId[contract]);//下完限价订单后，取消实时数据的接收，如果有成交，也会在收盘时平仓
            }            
        }
    }
}
