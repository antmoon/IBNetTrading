using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    /// <summary>
    /// 交易思想是，取最近100个tick的最高和最低，没有仓位时在最高处下stpbuy,最低处下stpsell；有仓位且为long时，每来一个tick，取消上一个stpsell，根据根据目前的100个tick最低价下stpsell；仓位为short时，根据目前的
    /// 100个tick最高价，先取消上一个是stpbuy，再下新的stpbuy;当有仓位且为long时，如果tick价格-仓位成本价大于等于6，且最低价小于仓位成本价，且此时bid大于
    /// </summary>
    class HighLowBreakOut :TradingInterface
    {
        public int NumberOfTicks;
        private Dictionary<Contract, double> offset = new Dictionary<Contract, double>();//追踪止损绝对值，就像MC中的dollartrailing
        private Dictionary<Contract, List<double>> TicksSinceFill=new Dictionary<Contract, List<double>>();//从成交开始，记录每一个ticks，这样如果仓位是long，TicksSinceFill中最高价-当前成交价格>=offset时，马上止损
        private Dictionary<Contract, List<double>> BiggestProfitReached = new Dictionary<Contract, List<double>>();//统计用，记录每个contract平仓前曾经达到的最大利润，最终需要算出这个结构中的数据的平均值，最大值，最小值，中值，方差，标准差等数据

        public HighLowBreakOut(IBClient ibclient, int numberOfdata=99):base(ibclient,numberOfdata)
        {
            NumberOfTicks = numberOfdata;
            tradeEvent += OnTick;
            fillEvent += OnFill;

            bidEvent += OnBid;
            askEvent += OnAsk;
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            offset.Add(contract, new double());
            TicksSinceFill.Add(contract, new List<double>());
            BiggestProfitReached.Add(contract, new List<double>());


            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
            ibclient.ClientSocket.reqContractDetails(requestId[contract], contract);//为了得到mintick
        }
        protected override void OnRemovecontract(Contract contract)
        {
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }
         void OnTick(Contract contract)
        {
            if (contractPrice[contract].Count < NumberOfTicks)
                return;                        

            double CurrentTradePrice = contractPrice[contract][NumberOfTicks];

            if (contractPosition[contract].holding == (double)0)//空仓
            {
                double Highest = contractPrice[contract].Max();
                double Lowest = contractPrice[contract].Min();

                if (!buyPlaced[contract] && CurrentTradePrice>=Highest)
                {
                    Order buyStop = new Order();
                    buyStop.Action = "BUY";
                    buyStop.TotalQuantity = 200;
                    buyStop.OrderType = "MKT";
                    buyStop.OutsideRth = true;
                  //  buyStop.AuxPrice = Highest;
                    PlaceOrder(contract,buyStop);
                    buyPlaced[contract] = true;
                    
                }
                if (!sellPlaced[contract] && CurrentTradePrice <= Lowest)
                {
                    Order sellStop = new Order();
                    sellStop.Action = "SELL";
                    sellStop.TotalQuantity = 200;
                    sellStop.OrderType = "MKT";
                    sellStop.OutsideRth = true;
                   // sellStop.AuxPrice = Lowest;
                    PlaceOrder(contract, sellStop);
                    sellPlaced[contract] = true;
                    
                }
                offset[contract] = Highest - Lowest;
                Console.WriteLine(contract.Symbol + ",offset:" + offset[contract]);

            }
            else if (contractPosition[contract].holding > (double)0)//目前是多仓
            {
                TicksSinceFill[contract].Add(CurrentTradePrice);
                //跟踪止损方式
                if (TicksSinceFill[contract].Max()- CurrentTradePrice>offset[contract])
                {
                    //要平仓了，先把曾经达到的最大利润记录下来，最大利润就是tickssincefill.max减去仓位成本
                    util.WriteMsg(contract.Symbol, DateTime.Now.ToString() + " " + (TicksSinceFill[contract].Max()- contractPosition[contract].averageCost), contract.Symbol);

                    Order sellStop = new Order();
                    sellStop.Action = "SELL";
                    sellStop.TotalQuantity = contractPosition[contract].holding;
                    sellStop.OrderType = "MKT";
                    sellStop.OutsideRth = true;
                    //sellStop.AuxPrice = stopPrice;
                    PlaceOrder(contract, sellStop);
                }

                //多仓进行的是跟踪止损，每次都先取消先前的止损单，然后重新下止损单
                /*
                 * *这种方法废弃了，因为会频繁的取消订单，下订单，超过IB每秒50条消息的限制，导致了自动断开连接，所以废弃，改成下面的方法，由程序自身跟踪止损，合适时下单。
                foreach (int orderID in OpenOrderID[contract])
                {
                    CancelOrder(orderID);//这种情况下才重新下单                 
                    
                }
                    

                double stopPrice;
                double MinTick = getMinTick(contract);
                
                double CurrentBidPrice = contractBid[contract][contractBid[contract].Count - 1];
                if (CurrentBidPrice > contractPosition[contract].averageCost && CurrentTradePrice - contractPosition[contract].averageCost > 6* MinTick && Lowest <= contractPosition[contract].averageCost)
                    stopPrice = contractPosition[contract].averageCost + MinTick;
                else
                    stopPrice = Lowest;

                Order sellStop = new Order();
                sellStop.Action = "SELL";
                sellStop.TotalQuantity = contractPosition[contract].holding;
                sellStop.OrderType = "STP";
                sellStop.AuxPrice = stopPrice;
                PlaceOrder(contract, sellStop);  
                */
                //不用管sellplaced，因为不管这个是true还是false，都要重新下单                
            }
            else //short的情况下
            {
                TicksSinceFill[contract].Add(CurrentTradePrice);
                if (CurrentTradePrice- TicksSinceFill[contract].Min()>offset[contract])
                {
                    util.WriteMsg(contract.Symbol, DateTime.Now.ToString() + " " + (contractPosition[contract].averageCost-TicksSinceFill[contract].Min()), contract.Symbol);

                    Order buyStop = new Order();
                    buyStop.Action = "BUY";
                    buyStop.TotalQuantity = Math.Abs(contractPosition[contract].holding);
                    buyStop.OrderType = "MKT";
                    buyStop.OutsideRth = true;
                    // buyStop.AuxPrice = stopPrice;
                    PlaceOrder(contract, buyStop);
                }
                /*
                 * 废弃
                foreach (int orderID in OpenOrderID[contract])
                    CancelOrder(orderID);
                double MinTick = getMinTick(contract);
                double stopPrice;
                
                double CurrentAskPrice = contractAsk[contract][contractAsk[contract].Count - 1];
                if (CurrentAskPrice < contractPosition[contract].averageCost && contractPosition[contract].averageCost - CurrentTradePrice > 6* MinTick && Highest >= contractPosition[contract].averageCost)
                    stopPrice = contractPosition[contract].averageCost - MinTick;
                else
                    stopPrice = Highest;

                Order buyStop = new Order();
                buyStop.Action = "BUY";
                buyStop.TotalQuantity = Math.Abs(contractPosition[contract].holding);
                buyStop.OrderType = "STP";
                buyStop.AuxPrice = stopPrice;
                PlaceOrder(contract, buyStop);              
                    */
            }

        }

        void OnBid(Contract contract)
        {
            if (contractPosition[contract].holding>0)
            {
                double currentBid = contractBid[contract][contractBid[contract].Count - 1];
                double profitPoint = contractPosition[contract].averageCost * 5 / 1000;
                if (offset[contract] > profitPoint)
                    profitPoint = offset[contract];
                if (currentBid-contractPosition[contract].averageCost> profitPoint)
                {
                    //止盈
                    Order profitOrder = new Order();
                    profitOrder.Action = "SELL";
                    profitOrder.TotalQuantity = contractPosition[contract].holding;
                    profitOrder.OrderType = "MKT";
                    profitOrder.OutsideRth = true;
                    PlaceOrder(contract, profitOrder);
                }
            }
        }

        void OnAsk(Contract contract)
        {
            if (contractPosition[contract].holding < 0)
            {
                double currentAsk = contractAsk[contract][contractAsk[contract].Count - 1];
                double profitPoint = contractPosition[contract].averageCost*4/1000;
                if (offset[contract] > profitPoint)
                    profitPoint = offset[contract];
                if (contractPosition[contract].averageCost-currentAsk> profitPoint)
                {
                    //止盈
                    Order profitOrder = new Order();
                    profitOrder.Action = "BUY";
                    profitOrder.TotalQuantity =Math.Abs(contractPosition[contract].holding);
                    profitOrder.OrderType = "MKT";
                    profitOrder.OutsideRth = true;
                    PlaceOrder(contract, profitOrder);
                }
            }
        }

        void OnFill(Contract contract)
        {
           if (contractPosition[contract].holding==(double)0)
            {
                buyPlaced[contract] = false;
                sellPlaced[contract] = false;

                TicksSinceFill[contract].Clear();//重新开始
            }
        }
    }
}
