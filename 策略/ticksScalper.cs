using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class ticksScalper : TradingInterface
    {//高频scalper，只要当前价格大于均值，就入市；只要价格从高点下跌超过1%，就平仓

        Dictionary<Contract, double> highestSincePosition = new Dictionary<Contract, double>();
        public ticksScalper(IBClient ibclient) : base(ibclient)
        {          
           
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            highestSincePosition.Add(contract, new double());
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
            
        }
        protected override void OnRemovecontract(Contract contract)
        {
            ibclient.ClientSocket.cancelMktData(requestId[contract]);            
        }

        public override void OnTradeSize(Contract contract)
        {
            if (contractPrice[contract].Count < this.numberOfData)
                return;

            double curPrice = contractPrice[contract].Last();
            Console.WriteLine("In tickScalper,Symbol:" + contract.Symbol + ",tradePrice:" + curPrice);
            if (contractPosition[contract].holding<=0)
            {
                if (highestSincePosition[contract] != 0)
                    highestSincePosition[contract] = 0;

                if (curPrice> contractPrice[contract].Average()&& contractPrice[contract].Average()> contractPrice[contract].First())
                {
                    Order buy = new Order();
                    buy.Action = "BUY";
                    buy.TotalQuantity = 200;
                    buy.Transmit = true;
                    buy.OrderType = "LMT";
                    buy.Hidden = true;
                    buy.Tif = "IOC";
                    buy.LmtPrice = Math.Round(curPrice, 2);
                    PlaceOrder(contract, buy);
                    Console.WriteLine("Scalping RealTime Bar下单，" + contract.Symbol + ",价格是：" + buy.LmtPrice);                    
                }
            }
            else if (contractPosition[contract].holding > 0)
            {
                if (curPrice > highestSincePosition[contract])
                    highestSincePosition[contract] = curPrice;

                if (highestSincePosition[contract] - curPrice > highestSincePosition[contract] * 0.01|| (curPrice < contractPrice[contract].Average() && contractPrice[contract].Average() < contractPrice[contract].First()))//跟踪止损，1%
                {
                    Order sellOrder = new Order();
                    sellOrder.Action = "SELL";
                    sellOrder.TotalQuantity = contractPosition[contract].holding;
                    sellOrder.OrderType = "LMT";
                    sellOrder.Transmit = true;
                    sellOrder.LmtPrice = Math.Round(curPrice, 2);
                    sellOrder.Hidden = true;
                    sellOrder.Tif = "IOC";
                    PlaceOrder(contract, sellOrder);
                    Console.WriteLine(contract.Symbol + DateTime.Now + ",sellAt:" + sellOrder.LmtPrice);
                }
            }
        }

    }
}

