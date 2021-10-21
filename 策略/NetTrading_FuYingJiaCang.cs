using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    //这个策略和网格法不同的是，浮盈后加一个仓位，如果价格下跌，跌破了最后一个加的仓位的价格，则全部平仓，并顺手多卖一个仓位，反向开仓
    //网格法是越跌越加仓，适合震荡市场，可以从均值回复中赚钱；而本策略浮盈加仓更适合趋势市场
    class NetTrading_FuYingJiaCang:TradingInterface
    {
        Dictionary<Contract, double> profitPoint;
        Dictionary<Contract, double> netPoint;

        public NetTrading_FuYingJiaCang(IBClient ibclient, int numberOfdata) : base(ibclient, numberOfdata)
        {
           
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            ibclient.ClientSocket.reqHistoricalData(requestId[contract], contract, DateTime.Now.ToString("yyyyMMdd HH:mm:ss"), "20 D", "1 day", "MIDPOINT", 1, 1, new List<TagValue>());
            ibclient.ClientSocket.reqRealTimeBars(requestId[contract], contract, 5, "MIDPOINT", false, new List<TagValue>());
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
        }
        protected override void OnRemovecontract(Contract contract)
        {
            // base.OnRemovecontract(contract);
            ibclient.ClientSocket.cancelRealTimeBars(requestId[contract]);
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }

        
        void onQuote(Contract contract)
        {
            if (contractPosEnd[contract])//确保仓位已经更新，我们是根据仓位做决断的
            {
                int bidCount = contractBid[contract].Count;
                int askCount = contractAsk[contract].Count;
                if (bidCount <= 0 || askCount <= 0)
                    return;

                if (contractPosition[contract].holding>0)
                {
                    double nextSell = contractPosition[contract].averageCost + (contractPosition[contract].holding / 100 - 1) * netPoint[contract]/2;
                    double nextBuy = contractPosition[contract].averageCost + (contractPosition[contract].holding / 100 + 1) * netPoint[contract] / 2;
                    
                    if (contractBid[contract][bidCount-1]<=nextSell && !sellPlaced[contract])
                    {
                        Order sellOrder = new Order();
                        sellOrder.Action = "SELL";
                        sellOrder.TotalQuantity = contractPosition[contract].holding;
                        sellOrder.OrderType = "MKT";
                        PlaceOrder(contract, sellOrder);
                        sellPlaced[contract] = true;
                    }
                    if (contractAsk[contract][askCount-1]>=nextBuy && !buyPlaced[contract])
                    {
                        Order buyOrder = new Order();
                        buyOrder.Action = "BUY";
                        buyOrder.TotalQuantity = 200;
                        buyOrder.OrderType = "MKT";
                        PlaceOrder(contract, buyOrder);
                        buyPlaced[contract] = true;
                    }
                }
                if (contractPosition[contract].holding < 0)
                {
                    double nextBuy = contractPosition[contract].averageCost - (contractPosition[contract].holding / 100 - 1) * netPoint[contract] / 2;
                    double nextSell = nextBuy-netPoint[contract];
                    if (contractAsk[contract][askCount-1]>=nextBuy && !buyPlaced[contract])
                    {
                        Order buyCover = new Order();
                        buyCover.TotalQuantity = System.Math.Abs(contractPosition[contract].holding);
                        buyCover.OrderType = "MKT";
                        buyCover.Action = "BUY";
                        PlaceOrder(contract, buyCover);
                        buyPlaced[contract] = true;
                    }
                    if (contractBid[contract][bidCount-1]<=nextSell && !sellPlaced[contract])
                    {
                        Order sellAgain = new Order();
                        sellAgain.Action = "SELL";
                        sellAgain.TotalQuantity = 200;
                        sellAgain.OrderType = "MKT";
                        PlaceOrder(contract, sellAgain);
                        sellPlaced[contract] = true;
                    }

                }
            }
        }
        void onTick(Contract contract)
        {

        }
        void onFill(Contract contract)
        {

        }
       

    }
}
