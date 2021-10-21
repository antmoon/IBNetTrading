using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using IBSampleApp;//直接拷贝了过来，引入时，需要using IBSampleAPP
using IBApi;
using UsStockTime;

namespace IBNetTrading
{
    public class Position
    {        
        public double holding;
        public double averageCost;
        public double unrealizedPnl;
        public double realizedPnl;
        public double marketPrice;
    }
    abstract class TradingInterface
    {

        int TickerID;//由IB进行初始化
        static int currentReqID;//记录当前ID，static便于所有类全集管理
        private static object reqIDojb = new object();//锁，当请求reqID时，使用这个锁，防止死锁
        private static object orderIDobj = new object();//订单ID锁

        protected Dictionary<Contract, bar> currentBar = new Dictionary<Contract, bar>();
        protected Dictionary<Contract, List<bar>> bars = new Dictionary<Contract, List<bar>>();
        // bar currentBar = null;

        //  List<bar> bars = new List<bar>();

        protected Dictionary<Contract, ContractDetails> contractDetails;

        protected int numberOfData;//bid,ask,trade最多存储的数目       

        protected Dictionary<int, Contract> contractIDrecord;//contractID压到这个字典中，每收到数据，先判断是否属于本策略要求的数据
        protected Dictionary<Contract, List<double>> contractPrice;
        protected Dictionary<Contract, List<double>> contractBid;
        protected Dictionary<Contract, List<double>> contractAsk;
        protected Dictionary<Contract, List<int>> contractSize;
        protected Dictionary<Contract, List<int>> contractBidSize;
        protected Dictionary<Contract, List<int>> contractAskSize;




        protected Dictionary<Contract, Position> contractPosition;//记录每个合同的仓位
        protected Dictionary<int, Contract> contractOrderID;//需要在placeorder函数中，将orderid和contract压入这个数据结构中,通过orderID找到对应的contract
        protected Dictionary<Contract, int> requestId;//根据contract去找对应的request id

        protected Dictionary<Contract, bool> contractPosEnd;
        protected Dictionary<Contract, bool> buyPlaced;//防止重复下单，下单后马上设为true；在单子成交后，由openorder中设为false，允许再次下单
        protected Dictionary<Contract, bool> sellPlaced;//ditto
        protected Dictionary<Contract, bool> signalCompleted;

        protected Dictionary<Contract, List<int>> OpenOrderID;//专门记录开放订单，将orderid压到这个结构中，如果订单成交，或者订单被取消，将orderid再remove掉，这样只记录open order
        protected Dictionary<Contract, List<Order>> FilledOrder;//专门记录contract每个成交的订单，这个和上面的OpenOrderID互补作用
        protected Dictionary<Contract, List<Order>> OpeningOrder;

        static protected Dictionary<int, Order> IDToOrder = new Dictionary<int, Order>();//上面的字典全部是通过contract找ID，或通过ID找contract，此处是通过ID找order

        protected List<Contract> contractInThisStrategy = new List<Contract>();

        protected IBClient ibclient;

        bool firstRun = true;

        protected delegate void contractDelegate(Contract contract);
        protected event contractDelegate addcontractEvent;
        protected event contractDelegate removecontractEvent;

        protected event contractDelegate histCompleteEvent;
        protected event contractDelegate tradeEvent;
        protected event contractDelegate bidEvent;
        protected event contractDelegate askEvent;
        protected event contractDelegate fillEvent;
        protected event contractDelegate FiveSecondsbarEvent;
        protected event contractDelegate ticksizeEvent;
        protected event contractDelegate quotesizeEvent;
        protected event contractDelegate positionEvent;
        protected event contractDelegate candleEvent;

        int barSize;
        public USStockTime usStockCloseTime = new USStockTime(15, 59, 0);

        public TradingInterface(IBClient ibclient, int maxCount = 300)//防止程序将所有内存吃完，加一个数量限定，每个tick，bar，就保存这么多，超过的话，将第一个去掉。
        {
            numberOfData = maxCount;
            // TickerID = requestId;

            contractIDrecord = new Dictionary<int, Contract>();
            contractPrice = new Dictionary<Contract, List<double>>();
            contractBid = new Dictionary<Contract, List<double>>();
            contractAsk = new Dictionary<Contract, List<double>>();
            contractSize = new Dictionary<Contract, List<int>>();
            contractBidSize = new Dictionary<Contract, List<int>>();
            contractAskSize = new Dictionary<Contract, List<int>>();


            contractDetails = new Dictionary<Contract, ContractDetails>();

            contractPosition = new Dictionary<Contract, Position>();
            contractOrderID = new Dictionary<int, Contract>();
            OpenOrderID = new Dictionary<Contract, List<int>>();
            requestId = new Dictionary<Contract, int>();
            contractPosEnd = new Dictionary<Contract, bool>();
            buyPlaced = new Dictionary<Contract, bool>();
            sellPlaced = new Dictionary<Contract, bool>();
            signalCompleted = new Dictionary<Contract, bool>();
            FilledOrder = new Dictionary<Contract, List<Order>>();
            OpeningOrder = new Dictionary<Contract, List<Order>>();

            this.ibclient = ibclient;


            ibclient.Error += error;
            ibclient.ConnectionClosed += handleDisconnect;
            this.ibclient.TickPrice += tickPrice;//回调全在构造函数里实现
            this.ibclient.TickSize += tickSize;
            ibclient.TickGeneric += TickGeneric;
            ibclient.TickOptionCommunication += TickOptionComputation;
            this.ibclient.HistoricalData += HisticalData;
            ibclient.HistoricalDataEnd += HistoricalDataEnd;
            this.ibclient.Position += Position;
            ibclient.PositionEnd += PositionEnd;
            ibclient.OrderStatus += OrderStatus;
            ibclient.OpenOrder += OpenOrder;
            ibclient.RealtimeBar += RealtimeBar;
            ibclient.ContractDetails += ContractDetails;
            ibclient.ContractDetailsEnd += ContractDetailsEnd;
            ibclient.SecurityDefinitionOptionParameter += SecurityDefinitionOptionParameter;
            ibclient.SecurityDefinitionOptionParameterEnd += SecurityDefinitionOptionParameterEnd;
            ibclient.NextValidId += nextValidId;
            ibclient.ScannerData += scannerData;
            ibclient.UpdatePortfolio += updatePortfolio;

            ibclient.ClientSocket.reqOpenOrders();
            ibclient.ClientSocket.reqIds(-1);
            ibclient.ConnectionClosed += ConnectionClosed;

            //自定义事件,这些处理方法都是虚方法，由继承的具体策略来实现

            this.tradeEvent += OnTrade;
            this.ticksizeEvent += OnTradeSize;
            this.fillEvent += OnFill;
            this.histCompleteEvent += OnHistPrice;
            this.bidEvent += OnQuote;
            this.askEvent += OnQuote;
            this.quotesizeEvent += OnQuoteSize;
            this.FiveSecondsbarEvent += on5secondsBar;
            this.candleEvent += onCandle;


            addcontractEvent += OnAddcontract;//主动函数，一添加contract，则去注册这个contract所有的主动函数，req
            removecontractEvent += OnRemovecontract;

             TakeCareOfTime();


            /*
            ThreadStart threadStart = new ThreadStart(TakeCareOfTime);
            Thread watchTime = new Thread(threadStart);
            watchTime.Start();
            */

        }

        public virtual void SecurityDefinitionOptionParameterEnd(int reqId)
        {

        }

        public virtual void ContractDetailsEnd(int reqId)
        {

        }

        public void ConnectionClosed()
        {
            Console.WriteLine("连接已经断开！");
            //this.ibclient.ClientSocket.eConnect("127.0.0.1", 7497, 0);
        }

        public virtual void OnHistPrice(Contract contract)
        {

        }
        public virtual void OnQuote(Contract contract)
        {

        }
        public virtual void OnQuoteSize(Contract contract)
        {

        }
        public virtual void OnFill(Contract contract)
        {

        }
        public virtual void OnTrade(Contract contract)
        {

        }
        public virtual void OnTradeSize(Contract contract)
        {

        }
        public virtual void on5secondsBar(Contract contract)
        {

        }

        public virtual void onCandle(Contract contract)
        {

        }

        void nextValidId(int validID)
        {
            TickerID = validID;
        }

        protected virtual void TakeCareOfTime()//一直睡眠到7：28分，然后苏醒，首先发出平仓信号，并且，开始检查仓位信息，仓位全部空的话，马上锁上下单锁
        {

            TimeSpan marketCloseTimeSpan = new TimeSpan(usStockCloseTime.CorrespondingChinaTime.Ticks);//
            Console.WriteLine("美股收盘时间是:" + usStockCloseTime.CorrespondingChinaTime);
            TimeSpan nowTicks = new TimeSpan(DateTime.Now.Ticks);
            if (marketCloseTimeSpan > nowTicks)
            {
                TimeSpan waitDuration = marketCloseTimeSpan.Subtract(nowTicks).Duration();
                Console.WriteLine("收盘需要等待 {0} 时间", waitDuration);
                // Thread.Sleep(waitDuration);
                var t = new Timer(closeAllPosition);                
                t.Change(waitDuration, Timeout.InfiniteTimeSpan);
            }
            else
            {
                Console.WriteLine("已经收盘");
            }

            // closeAllPosition();//到达收盘时间，关闭所有仓位
            //  Thread.Sleep(1000);
            // ibclient.ClientSocket.eDisconnect();

            //  Console.WriteLine("交易关闭，所有数据不再更新。交易结束时间：" + DateTime.Now);


        }

        void closeAllPosition(object state)
        {
            Console.WriteLine("清仓，开始卖掉所有仓位！");
            foreach (KeyValuePair<Contract, Position> ConPostion in contractPosition)
            {                
                if (ConPostion.Value.holding > 0)
                {
                    Order sellToCloseOrder = new Order();
                    sellToCloseOrder.Action = "SELL";
                    sellToCloseOrder.TotalQuantity = ConPostion.Value.holding;
                    sellToCloseOrder.OrderType = "MKT";
                    sellToCloseOrder.OutsideRth = true;
                    sellToCloseOrder.Transmit = true;
                    PlaceOrder(ConPostion.Key, sellToCloseOrder);
                    removecontractEvent(ConPostion.Key);//引发停止接收数据事件，不再接收数据
                }
                if (ConPostion.Value.holding < 0)
                {
                    Order buyToCover = new Order();
                    buyToCover.Action = "BUY";
                    buyToCover.TotalQuantity = System.Math.Abs(ConPostion.Value.holding);
                    buyToCover.OrderType = "MKT";
                    buyToCover.OutsideRth = true;
                    buyToCover.Transmit = true;
                    PlaceOrder(ConPostion.Key, buyToCover);
                    removecontractEvent(ConPostion.Key);

                }

            }

        }

        void handleDisconnect()
        {
            Console.WriteLine("Disconnected from IBTWS,Time:" + DateTime.Now);
        }


        void error(int a, int b, string m, Exception e)
        {
            Console.WriteLine("Exception: " + e + "---a:" + a + "---b:" + b + "---string m: " + m);
        }

        void tickPrice(int tickerId, int field, double price, int canAutoExecute)
        {
            //判断tickerid是否属于本策略，如果不属于，则不去处理   
            // Console.WriteLine("tickId:" + tickerId + ",Field:" + field + ",price" + price);
            if (!contractIDrecord.ContainsKey(tickerId))
                return;
            //更新相应的价格
            //debug

            Contract CurrentContract = contractIDrecord[tickerId];

            switch (field)
            {
                case 1:
                    contractBid[CurrentContract].Add(price);
                    // Console.WriteLine("Bid:" + price);
                    if (bidEvent != null)
                        bidEvent(CurrentContract);
                    break;
                case 2:
                    contractAsk[CurrentContract].Add(price);
                    if (askEvent != null)
                        askEvent(CurrentContract);
                    break;
                case 4:
                    contractPrice[CurrentContract].Add(price);
                    if (tradeEvent != null)
                        tradeEvent(CurrentContract);
                    break;
                // case 6:
                //     TodayHighest[CurrentContract] = price;
                //    break;
                // case 7:
                //      TodayLowest[CurrentContract] = price;
                //      break;
                case 14://Open Tick
                    break;
                case 15://Low 13 Weeks, stocks only.
                    break;
                case 16://high 13 weeks
                    break;
                case 17://Low 26 Weeks
                    break;
                case 18://High 26 Weeks
                    break;
                case 19://Low 52 Weeks
                    break;
                case 20://High 52 Weeks
                    break;
                default:
                    break;
            }
            if (contractAsk[CurrentContract].Count > numberOfData)
                contractAsk[CurrentContract].RemoveAt(0);
            if (contractBid[CurrentContract].Count > numberOfData)
            {
                contractBid[CurrentContract].RemoveAt(0);
            }
            if (contractPrice[CurrentContract].Count > numberOfData)
                contractPrice[CurrentContract].RemoveAt(0);

        }

        void tickSize(int tickerId, int field, int size)
        {
            //判断tickerid是否属于本策略，如果不属于，则不去处理
            if (!contractIDrecord.ContainsKey(tickerId))
                return;

            int requestID = tickerId;
            Contract curContract = contractIDrecord[requestID];
            switch (field)
            {
                case 5:
                    this.contractSize[curContract].Add(size);
                    if (ticksizeEvent != null)
                        ticksizeEvent(curContract);
                    break;
                case 0:
                    this.contractBidSize[curContract].Add(size);
                    if (quotesizeEvent != null)
                        quotesizeEvent(curContract);
                    break;
                case 3:
                    this.contractAskSize[curContract].Add(size);
                    if (quotesizeEvent != null)
                        quotesizeEvent(curContract);
                    break;
                case 21://Average Volume
                    break;
                case 27://Option Call Open Interest
                    break;
                case 28://Option Put Open Interest
                    break;
                case 29://Call option volume for the trading day.
                    break;
                case 30://	Put option volume for the trading day.
                    break;
                case 34://The number of shares that would trade if no new orders were received and the auction were held now.
                    break;
                case 36://The number of unmatched shares for the next auction; returns how many more shares are on one side of the auction than the other. Typically received after Auction Volume (tick type 34)
                    break;
                default:
                    break;

            }
            if (contractAskSize[curContract].Count > numberOfData)
                contractAskSize[curContract].RemoveAt(0);

            if (contractBidSize[curContract].Count > numberOfData)
            {
                contractBidSize[curContract].RemoveAt(0);
            }
            if (contractSize[curContract].Count > numberOfData)
                contractSize[curContract].RemoveAt(0);
        }

        public virtual void TickGeneric(int tickerId, int field, double value)
        {
            if (!contractIDrecord.ContainsKey(tickerId))
                return;
            // Console.WriteLine("TickGeneric"+tickerId + "field：" + field + ",值：" + value);
            switch (field)
            {
                case 23://The 30-day historical volatility (currently for stocks).30天的历史波动率,reqMktdata,104
                    Console.WriteLine(tickerId + " 30 day history volatility:" + value);
                    break;
                case 24://Option Implied Volatility A prediction of how volatile an underlying will be in the future. The IB 30-day volatility is the at-market volatility estimated for a maturity thirty calendar days forward of the current trading day, and is based on option prices from two consecutive expiration months.
                    Console.WriteLine(tickerId + " IB预测波动率Option Implied Volatility:" + value);//预测波动率
                    break;
                case 31://The number of points that the index is over the cash index.
                    break;
                case 46://Describes the level of difficulty with which the contract can be sold short. See Shortable
                    break;
                case 49://Indicates if a contract is halted. See Halted
                    if (removecontractEvent != null)
                        removecontractEvent(contractIDrecord[tickerId]);
                    break;
                case 54://Trade count for the day.目前为止当天的成交次数
                    break;
                case 55://Trade count per minute.每分钟成交次数
                    break;
                case 56://	Volume per minute.每分钟成交量
                    break;
                case 58://	30-day real time historical volatility.30天真实的历史波动率。
                    break;
                default:
                    break;
            }
        }

        public virtual void TickOptionComputation(int tickerId, int field, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
        {
            Console.WriteLine("TickOptionComputation. TickerId: " + tickerId + ", field: " + field + ", ImpliedVolatility: " + impliedVolatility + ", Delta: " + delta
                + ", OptionPrice: " + optPrice + ", pvDividend: " + pvDividend + ", Gamma: " + gamma + ", Vega: " + vega + ", Theta: " + theta + ", UnderlyingPrice: " + undPrice);
            if (!contractIDrecord.ContainsKey(tickerId))
                return;
            Contract currentContract = contractIDrecord[tickerId];

            switch (field)
            {
                //Bid Option Computation 根据标的价和期权报买价计算的希腊值
                case 10://Computed Greeks and implied volatility based on the underlying stock price and the option bid price
                    break;
                //Ask Option Computation 根据标的价和期权报卖价计算的希腊值
                case 11://Computed Greeks and implied volatility based on the underlying stock price and the option ask price
                    break;
                //Computed Greeks and implied volatility based on the underlying stock price and the option last traded price. See Option Greeks
                case 12://Last Option Computation根据标的价和期权最新成交价计算的希腊值
                    break;
                //Model Option Computation 根据标的价格和期权模型计算的希腊值。
                case 13://Computed Greeks and implied volatility based on the underlying stock price and the option model price. Correspond to greeks shown in TWS
                    break;
            }

        }

        public virtual void SecurityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
        }


        public virtual void HisticalData(int reqId, string date, double open, double high, double low, double close, int volume, int count, double WAP, bool hasGapsa)
        {
            //判断tickerid是否属于本策略，如果不属于，则不去处理
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            Console.WriteLine("数据属于本策略，分别是：Close" + close + ":Count=" + count);
            Contract ticker = contractIDrecord[reqId];//根据reqID找到对应的contract

        }

        public virtual void HistoricalDataEnd(int reqId, string startDate, string endDate)
        {
            if (contractIDrecord.ContainsKey(reqId))
            {
                if (histCompleteEvent != null)
                    histCompleteEvent(contractIDrecord[reqId]);//历史数据下载完毕，通知OnBar
            }
        }

        public virtual void RealtimeBar(int reqId, long time, double open, double high, double low, double close, long volume, double WAP, int count)
        {
            //目前还不确定是把这个加入到哪个数据结构中，个人感觉应该是构造bar使用的。暂时先显示在屏幕上吧
            //  Console.WriteLine("reqID:" + reqId + ",time" + time + ",open:" + open + ",high:" + high + ",low:" + low + ",close:" + close + ",volume:" + volume + ",WAP:" + WAP);
            //判断tickerid是否属于本策略，如果不属于，则不去处理

            //如果是外汇类的，在这里组成外汇的bar，因为外汇类的没有成交信息，req时需要改为middle

            if (!contractIDrecord.ContainsKey(reqId))
                return;
            Contract ticker = contractIDrecord[reqId];//根据reqID找到对应的contract

            bar curBar = new bar(high, low, open, close, volume, WAP);

            bars[ticker].Add(curBar);

            if (FiveSecondsbarEvent != null)
            {
                FiveSecondsbarEvent(contractIDrecord[reqId]);//引发bar事件
            }

        }


        void Position(string account, Contract contract, double pos, double avgCost)
        {
            if (firstRun)
            {
                firstRun = false;
                return;
            }
            Dictionary<Contract, Position>.KeyCollection ConPos = contractPosition.Keys;
            foreach (Contract key in ConPos)
            {
                if (key.Symbol == contract.Symbol && key.SecType == contract.SecType && key.Strike == contract.Strike && key.Right == contract.Right)//这玩意的坑在于，不能去比较key和contract，即使他们事实上是同一个，symbol完全相同，但是结构可能不一样，有的带其他数据
                {
                    contractPosition[key].averageCost = avgCost;
                    contractPosition[key].holding = pos;
                    contractPosEnd[key] = true;
                    Console.WriteLine(contract.Symbol + "属于本策略，持仓数量是：" + contractPosition[key].holding + ",持仓成本是：" + contractPosition[key].averageCost);
                    if (positionEvent != null)
                        positionEvent(key);
                }
            }

        }

        void PositionEnd()
        {

        }
        void updatePortfolio(Contract contract,double position,double marketPrice,double marketValue,double averageCost,double unrealizedPNL,double realizedPNL,string accountName)
        {
            if (!contractPosition.ContainsKey(contract))
                return;
            contractPosition[contract].marketPrice = marketPrice;
            contractPosition[contract].realizedPnl = realizedPNL;
            contractPosition[contract].unrealizedPnl = unrealizedPNL;
            
        }

        //根据orderId也能推断出来是个买单还是卖单
        void OrderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld)
        {//只有一个成交价格相对于openorder函数有价值
            //仍然先判断order属于本策略中的            
            if (!contractOrderID.ContainsKey(orderId))
                return;
            //如果status == "Filled"说明全部完成了  
            Contract currentContract = contractOrderID[orderId];//很多事件要用到contract，所以我们要根据orderid找到对应的contract

            if (status == "Filled" && fillEvent != null)
            {
                FilledOrder[currentContract].Add(IDToOrder[orderId]);//使用orderId 找到order，这个是成交的订单，存到成交订单list里面
                fillEvent(currentContract);
                util.WriteMsg(currentContract.Symbol + "成交数量:" + filled, "，成交价格:" + avgFillPrice, DateTime.Now.ToString("yyyy-MM-dd"));
                Console.WriteLine("OrderStatus中有成交，orderid=" + orderId + ",目前contractPos状态是：" + contractPosEnd[contractOrderID[orderId]] + ",whyheld:" + whyHeld + "，成交方向：" + IDToOrder[orderId].Action);

            }

        }

        //上面的order status中有通过IDTOORDER字典找到order后，就能辨别方向了，下面的openorder函数实际不需要了
        void OpenOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {
            if (!contractOrderID.ContainsKey(orderId))
                return;

            Contract currentContract = contractOrderID[orderId];//很多事件要用到contract，所以我们要根据orderid找到对应的contract

            if (orderState.Status == "Filled" || orderState.Status == "Cancelled")
            {
                if (OpeningOrder.ContainsKey(contract))
                {
                    foreach (Order waitingorder in OpeningOrder[contract])
                    {
                        if (waitingorder == order)
                            OpeningOrder[contract].Remove(order);
                    }
                }
                Console.WriteLine("OpenOrder中有事件发生，合同是：" + contract.Symbol + ",事件是：" + orderState.Status);
                util.WriteMsg(contractOrderID[orderId].Symbol, "，成交方向:" + order.Action, DateTime.Now.ToString("yyyy-MM-dd"));

                switch (order.Action)
                {
                    case "BUY":
                        buyPlaced[currentContract] = false;//买单已成交或者取消，释放这个锁，允许再次下买单。不加标志的话，TWS总是一次下三四个单，重复下单，本来只要买200，他会给买600
                        break;
                    case "SELL":
                        sellPlaced[currentContract] = false;
                        break;
                    default:
                        break;
                }
                //  contractOrderID.Remove(orderId);//将orderid从追踪的数据结构中移除，防止消息重复，因为IB可能会发送相同的消息。
                if (OpenOrderID[currentContract].Contains(orderId))
                    OpenOrderID[currentContract].Remove(orderId);
            }
            else if (orderState.Status== "Submitted")
            {
                if (OpeningOrder.ContainsKey(contract))
                {
                    if (!OpeningOrder[contract].Contains(order))
                        OpeningOrder[contract].Add(order);
                }
            }
        }

        public virtual void ContractDetails(int reqId, ContractDetails contractDetails)
        {
            if (!contractIDrecord.ContainsKey(reqId))
                return;
            Contract currentContract = contractIDrecord[reqId];
            if (currentContract.Symbol == contractDetails.Summary.Symbol)
            {
                this.contractDetails[currentContract] = contractDetails;
            }
        }

        public virtual void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr)
        {
            /*
            Console.WriteLine("ScannerData. " + reqId + " - Rank: " + rank + ", Symbol: " + contractDetails.Summary.Symbol + ", SecType: " + contractDetails.Summary.SecType + ", Exchange: " + contractDetails.Summary.Exchange + ", Currency: " + contractDetails.Summary.Currency
                + ", Distance: " + distance + ", Benchmark: " + benchmark + ", Projection: " + projection + ", Legs String: " + legsStr);
            Console.WriteLine("Next Option Date:" + contractDetails.NextOptionDate);
            */
        }

        public void addcontract(Contract contract)
        {
            if (addcontractEvent != null)
                addcontractEvent(contract);
        }
        public void removecontract(Contract contract)
        {
            if (removecontractEvent != null)
                removecontractEvent(contract);
        }
        public void addUSStocksSmart(string[] args)
        {
            foreach (string symbol in args)
            {
                addUSStockSmart(symbol);
            }
        }
        public void addUSStockSmart(string symbol)
        {
            Contract contract = CreateUSStockContractSmartRoute(symbol);
            addcontract(contract);
        }
        protected virtual void OnAddcontract(Contract contract)
        {
            if (!contractInThisStrategy.Contains(contract))
            {
                currentBar[contract] = null;
                //为历史数据建造数据结构

                List<double> Trade = new List<double>();
                List<double> Bid = new List<double>();
                List<double> Ask = new List<double>();
                List<int> TradeSize = new List<int>();
                List<int> BidSize = new List<int>();
                List<int> AskSize = new List<int>();

                List<int> OpenOrderid = new List<int>();
                List<Order> orderList = new List<Order>();

                Position position = new Position();
                position.averageCost = 0D;
                position.holding = 0;

                bool buySent = false;
                bool sellSent = false;
                bool signalComplete = false;

                int requestID = creatReqId();

                Console.WriteLine(contract.Symbol + "生成tickerid成功，id是：" + requestID);

                this.requestId.Add(contract, requestID);//这个通过contract找ID
                this.contractIDrecord.Add(requestID, contract);//这个通过ID找contract
                this.contractPrice.Add(contract, Trade);
                this.contractBid.Add(contract, Bid);
                this.contractAsk.Add(contract, Ask);
                this.contractSize.Add(contract, TradeSize);
                this.contractAskSize.Add(contract, AskSize);
                this.contractBidSize.Add(contract, BidSize);
                this.contractPosition.Add(contract, position);
                this.contractPosEnd.Add(contract, true);//初始为真，初始就能下单
                this.bars.Add(contract, new List<bar>());


                this.OpenOrderID.Add(contract, OpenOrderid);

                this.FilledOrder.Add(contract, orderList);
                this.OpeningOrder.Add(contract, new List<Order>());

                this.buyPlaced.Add(contract, buySent);
                this.sellPlaced.Add(contract, sellSent);
                this.signalCompleted.Add(contract, signalComplete);
                this.contractDetails.Add(contract, new IBApi.ContractDetails());

                contractInThisStrategy.Add(contract);
            }                

        }

        protected virtual void OnRemovecontract(Contract contract)
        {

            int reqID = 0;
            Dictionary<int, Contract>.KeyCollection allcontractKeys = contractIDrecord.Keys;
            foreach (int key in allcontractKeys)
            {
                if (contractIDrecord[key] == contract)
                {
                    this.ibclient.ClientSocket.cancelHistoricalData(key);
                    this.ibclient.ClientSocket.cancelMktData(key);
                    reqID = key;
                }
            }
            if (reqID != 0)
                contractIDrecord.Remove(reqID);

        }

        public Contract CreateHKStockContract(string symbol)
        {
            Contract hkStock = new Contract();
            hkStock.Currency = "HKD";
            hkStock.Exchange = "SEHK";
            hkStock.Symbol = symbol;
            hkStock.SecType = "STK";
            return hkStock;
        }

        public Contract CreateUSStockContractSmartRoute(string symbol)
        {
            Contract Stock = new Contract();
            Stock.Currency = "USD";
            Stock.Exchange = "SMART";
            Stock.Symbol = symbol;
            Stock.SecType = "STK";

            return Stock;
        }
        public Contract CreateFXContract(string symbol)
        {
            Contract fx = new Contract();
            fx.Symbol = symbol;
            fx.Exchange = "IDEALPRO";
            fx.SecType = "CASH";
            fx.Currency = "USD";
            return fx;
        }

        public Contract CreateOptionContract(string symbol,double strike,string right="C")
        {
            Contract contract = new Contract();
            contract.Symbol = symbol;
            contract.SecType = "OPT";
            contract.Exchange = "SMART";
            contract.Currency = "USD";            
            DateTime OneMonthLater = DateTime.Now.AddMonths(1);
            contract.LastTradeDateOrContractMonth = OneMonthLater.ToString("yyyyMMdd");
            contract.Strike = strike;
            contract.Right = right;
            contract.Multiplier = "100";
            Console.WriteLine("成功创建期权，" + symbol + ":日期：" + contract.LastTradeDateOrContractMonth);
            return contract;
        }

        public virtual void PlaceOrder(Contract contract,Order order)//下单，修改订单使用
        {
            int orderID = createOrderId();
            this.contractOrderID.Add(orderID, contract);//通过orderID去找对应的contract
            IDToOrder.Add(orderID, order);//通过orderstatus中的orderID推断order的buy还是sell，这样就不需要使用下面的openorder中的action了；成交的订单不移除掉
            OpenOrderID[contract].Add(orderID);//要取消open order用，取消掉没有成交的订单用，因为成交后，自动会把里面的orderid移除掉；还有一个结构，filledorder，专门记录成交的订单
            this.ibclient.ClientSocket.placeOrder(orderID, contract, order);                        
                
            contractPosEnd[contract] = false;
            
        }
        public void CancelOrder(int orderId)//取消开放订单用
        {
            this.ibclient.ClientSocket.cancelOrder(orderId);
            if (contractOrderID.ContainsKey(orderId))
            {
                contractOrderID.Remove(orderId);
            }
            if (IDToOrder.ContainsKey(orderId))
            {
                IDToOrder.Remove(orderId);
            }
            //OpenOrderID由回调函数来处理，当有订单成交或取消时，引发openorder,orderstatus事件，其中去处理OpenOrderID

        }

        int creatReqId()
        {
            if (currentReqID < TickerID)
                currentReqID = TickerID;            
            return currentReqID++;
        }
        public int createOrderId()
        {
            int tem;
            lock(orderIDobj)
            {
                tem = this.ibclient.NextOrderId++;
            }
            return tem;
        }
        
        
    }
}
