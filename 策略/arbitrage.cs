using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;
/*
 * building automated trading systems中的案例，交易的是ES和NQ，不过是双线程的
 * Price Spread delta= -1*ES Bid + 2*NQ Ask;
 * z-transform method  norm=(delta-MA(delta)) / sigma(delta)
 * MA是30个价差改变的均值，不监听ticks的变化，只监听bid和ask
 * long position定义为-1*ES(卖空）和2*NQ（买涨2）；
 * short position定义为1*ES(买涨）和-2*NQ（卖空）
 * 策略是监控价差（spread price），根据价差计算norm，当norm超过2时，任务是看空信号，要做空一手，因为我们任务norm会回复到0；相反的是，当norm低于-2时，我们任务是看涨信号。
 * 看涨时，首先用限价单在ES的offer(ask)也就是卖价处卖出1*ES，如果订单后来成交，我们马上用市价单买入2*NQ；如果最终没有成交，也就是限价单没有被hit并且norm回复到了-2到2之间，我们就取消订单，然后等待下一次的机会
 * 止损价设为4个ticks；目标止盈价是当norm=0时，进行平仓。
        
*/

namespace IBNetTrading.Strategy
{
    class Arbitrage: TradingInterface
    {        
        Contract A;//假定为ES
        Contract B;//假定为NQ
        double miniTick_A;
        double miniTick_B;
        int MATickNumber = 30;
        double normUpband = 2D;//这些值只能在做ES和NQ的时候用，都能调整
        double normDownband = -2D;

        Dictionary<string, List<double>> delta=new Dictionary<string, List<double>>();//存储每一个delta，当数量超过30个后，再来新的数据后，首先去掉第一个，然后加入新的；string是TICKA+TICKB作为标识符



        public Arbitrage(string tickerA,string tickerB,string secType,IBClient ibclient) : base(ibclient)
        {
            
            histCompleteEvent += onHistPrice;
            bidEvent += onQuote;
            askEvent += onQuote;
            tradeEvent += onTick;
            fillEvent += onFill;
            FiveSecondsbarEvent += onBar;
            
            switch (secType)
            {
                case "STK":
                    A=CreateUSStockContractSmartRoute(tickerA);
                    B=CreateUSStockContractSmartRoute(tickerB);
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
                    if (currentMonth>3 && currentMonth<=6)
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
        public void StartArbitrage()
        {
            delta.Add(A.Symbol + B.Symbol, new List<double>());

            this.addcontract(A);
            this.addcontract(B);
            
            
            ibclient.ClientSocket.reqContractDetails(requestId[A], A);
            ibclient.ClientSocket.reqContractDetails(requestId[B], B);
        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
           
        }
        protected override void OnRemovecontract(Contract contract)
        {
            // base.OnRemovecontract(contract);
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
            
        }

        void onQuote(Contract contract)
        {
            int AbidIndex = contractBid[A].Count-1;
            int BofferIndex = contractAsk[B].Count - 1;
            if (AbidIndex < 1 || BofferIndex < 1)
                return;

            double deltaNow = contractBid[A][AbidIndex] * (-1) + 2 * contractAsk[B][BofferIndex];
            delta[A.Symbol + B.Symbol].Add(deltaNow);

            if (delta[A.Symbol + B.Symbol].Count < 30)
                return;

            double deltaMA = delta[A.Symbol + B.Symbol].Average();
            double deltaSigma = CalculateStdDev(delta[A.Symbol + B.Symbol]);
            double norm = (deltaNow - deltaMA) / deltaSigma;
            

            if (contractPosition[A].holding==0D && contractPosition[B].holding==0D)
            {
                if (norm>= normUpband && !buyPlaced[A])//看跌信号
                {
                    double mintickA = 0.01;
                    Order buyA = new Order();
                    buyA.Action = "BUY";
                    buyA.TotalQuantity = 100;//买入一个单位的A，需要在contractdetails获取这个单位
                    buyA.OrderType = "TRAIL LIMIT";
                    buyA.LmtPrice = contractBid[A][AbidIndex];
                    buyA.TrailStopPrice = buyA.LmtPrice- mintickA;//这个A的止损设在4个ticks下，ticks需要在contractdetails中得到
                    PlaceOrder(A, buyA);
                    buyPlaced[A] = true;
                }
                if (norm<=normDownband && !sellPlaced[A])
                {
                    double mintickB = 0.01;
                    Order sellA = new Order();
                    sellA.Action = "SELL";
                    sellA.TotalQuantity = 100;
                    sellA.OrderType = "TRAIL LIMIT";
                    sellA.LmtPrice = contractAsk[A][contractAsk[A].Count];
                    sellA.TrailStopPrice = sellA.LmtPrice+4* mintickB;
                    PlaceOrder(A, sellA);
                    sellPlaced[A] = true;
                }
                else if(norm>normDownband && norm<normUpband)//根据策略定义，当norm超过up和downband,我们下单，如果没有成交，而且norm回复了下来，则取消没有成交的订单，等待机会                    
                {
                    if (OpenOrderID[contract].Count>0)
                    {
                        foreach(int orderid in OpenOrderID[contract])
                            ibclient.ClientSocket.cancelOrder(orderid);
                        OpenOrderID[contract].Clear();

                    }
                }
            }

        }
        void onHistPrice(Contract contract)
        {

        }
        void onTick(Contract contract)
        {

        }
        void onFill(Contract contract)
        {
            if (contractPosition[A].holding>0 && contractPosition[B].holding==0 && !sellPlaced[B])
            {
                Order sellB = new Order();
                sellB.Action = "SELL";
                sellB.TotalQuantity = 2 * contractPosition[A].holding ;
                sellB.OrderType = "MKT";
                PlaceOrder(B, sellB);
                sellPlaced[B] = true;
            }
            if (contractPosition[A].holding < 0 && contractPosition[B].holding == 0 && !buyPlaced[B])
            {
                Order buyB = new Order();
                buyB.Action = "BUY";
                buyB.TotalQuantity = 2 * contractPosition[A].holding;
                buyB.OrderType = "MKT";
                PlaceOrder(B, buyB);
                buyPlaced[B] = true;
            }

        }
        void onBar(Contract contract)
        {

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
    }
}
