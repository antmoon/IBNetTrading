using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using IBNetTrading;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    /// <summary>
    /// 短期均线交易，短期70个ticks，长期110个ticks，短期越过长期，买入；短期死叉，卖出。同时，盈利每达到0.4%，加100仓位
    /// </summary>
    class ShortSMA: TradingInterface
    {
        bool fistRun = true;

        int ShortTicksDataNumber;
        int LongTicksDataNumber;

        Dictionary<Contract, List<double>> shortMaTicks;//存储短期的TICKS
        
        public ShortSMA(IBClient ibclient, int shortMAData=70, int LongMAData = 110):base(ibclient)
        {
            ShortTicksDataNumber = shortMAData;
            LongTicksDataNumber = LongMAData;
            shortMaTicks = new Dictionary<Contract, List<double>>();

            bidEvent += onBid;
            askEvent += onAsk;
            tradeEvent += onTick;
            fillEvent += onFill;
            FiveSecondsbarEvent += onBar;
            positionEvent += onPosition;
        }
        protected override void OnAddcontract(Contract contract)
        {
            shortMaTicks.Add(contract, new List<double>());

            base.OnAddcontract(contract);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, string.Empty, false, new List<TagValue>());

        }
        protected override void OnRemovecontract(Contract contract)
        {
             base.OnRemovecontract(contract);
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }
        public void onBid(Contract contract)
        {            

        }
        public void onAsk(Contract contract)
        {

        }
        public void onTick(Contract contract)
        {
            double currentTrade = contractPrice[contract][contractPrice[contract].Count - 1];
            shortMaTicks[contract].Add(currentTrade);
            if (shortMaTicks[contract].Count > ShortTicksDataNumber)
                shortMaTicks[contract].RemoveAt(0);
            if (contractPrice[contract].Count < LongTicksDataNumber)
                return;

            double longMA = contractPrice[contract].Average();
            double shortMA = shortMaTicks[contract].Average();

            if (contractPosition[contract].holding==(double)0)
            {
                if (!buyPlaced[contract] && shortMA>longMA)//买入
                {
                    Order longOrder = new Order();
                    longOrder.Action = "BUY";
                    longOrder.TotalQuantity = 200;
                    longOrder.OrderType="LMT";
                    longOrder.LmtPrice = contractBid[contract][contractBid[contract].Count - 1];
                    longOrder.OutsideRth = true;

                    PlaceOrder(contract, longOrder);
                    buyPlaced[contract] = true;
                }
                if (!sellPlaced[contract]&&shortMA<longMA)
                {
                    Order shortOrder = new Order();
                    shortOrder.Action = "SELL";
                    shortOrder.TotalQuantity = 200;
                    shortOrder.OrderType = "LMT";
                    shortOrder.LmtPrice = contractAsk[contract][contractAsk[contract].Count - 1];
                    shortOrder.OutsideRth = true;

                    PlaceOrder(contract, shortOrder);
                    sellPlaced[contract] = true;

                }
            }
            else if (contractPosition[contract].holding>0)
            {
                if (shortMA<longMA)
                {
                    Order flat = new Order();
                    flat.OrderType = "MKT";
                    flat.TotalQuantity = contractPosition[contract].holding;
                    flat.Action = "SELL";

                    PlaceOrder(contract, flat);
                }
            }
            else
            {
                if (shortMA > longMA)
                {
                    Order flat = new Order();
                    flat.OrderType = "MKT";
                    flat.TotalQuantity = Math.Abs(contractPosition[contract].holding);
                    flat.Action = "BUY";

                    PlaceOrder(contract, flat);
                }
            }
            

        }
        public void onFill(Contract contract)
        {
            foreach (int openOrderID in OpenOrderID[contract])
                CancelOrder(openOrderID);//有限价单成交，先取消掉所有没有成交的订单
            if (contractPosition[contract].holding==(double)0)
            {
                sellPlaced[contract] = false;
                buyPlaced[contract] = false;
            }
        }
        public void onBar(Contract contract)
        {            


        }
        public void onPosition(Contract contract)
        {
            if (contractPosition[contract].holding == (double)0)
            {
                sellPlaced[contract] = false;
                buyPlaced[contract] = false;
            }
        }

    }
}
