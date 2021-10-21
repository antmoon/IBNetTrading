using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBNetTrading;
using IBApi;
using IBSampleApp;
using Python.Runtime;

namespace IBNetTrading.Strategy
{    
    class HmmTrading:TradingInterface
    {
        dynamic pickle;
        dynamic hmm;
        dynamic np;
        PyObject py;
        dynamic hmmmodel;

        public HmmTrading(IBClient ibclient) :base(ibclient)
        {            
            tradeEvent += onTick;
            fillEvent += onFill;
            FiveSecondsbarEvent += onMinuteBar;

            using (Py.GIL())
            {
                pickle = Py.Import("pickle");
                hmm = Py.Import("hmmlearn");
                np = Py.Import("numpy");

                py = PythonEngine.Eval("open('TVIXIntraDay1Mbar.pkl', 'rb')");//执行原生函数
            }
        }

        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);  
            if (contract.Symbol=="TVIX")
            {
                using (Py.GIL())
                {
                    hmmmodel = pickle.load(py);//获得模型
                }
                    ibclient.ClientSocket.reqMktData(requestId[contract], contract, string.Empty, false, new List<TagValue>());
            }
            

        }
        protected override void OnRemovecontract(Contract contract)
        {
          //  base.OnRemovecontract(contract);
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }

        void onFill(Contract contract)//update position
        {
            Console.WriteLine("有新成交，成交合同是：" + contract.Symbol);

        }
        void onTick(Contract contract)//price,size
        {
            
        }
        void onMinuteBar(Contract contract)//price,size
        {
            Console.WriteLine(contract.Symbol + " Close:" + bars[contract][bars[contract].Count - 1].close);
            if (bars[contract].Count < 5)
                return;

            int index = bars[contract].Count - 1;

            double closeMA = (bars[contract][index].close +
                            bars[contract][index - 1].close +
                            bars[contract][index - 2].close +
                            bars[contract][index - 3].close +
                            bars[contract][index - 4].close) / 5;
            double volueMA = (bars[contract][index].volume +
                            bars[contract][index - 1].volume +
                            bars[contract][index - 2].volume +
                            bars[contract][index - 3].volume +
                            bars[contract][index - 4].volume) / 5;

            double logRet_1 = Math.Log(bars[contract][index].close) - Math.Log(bars[contract][index-1].close);
            double logRet_5 = Math.Log(bars[contract][index].close) - Math.Log(bars[contract][index-4].close);
            double logRet_5_Ma = Math.Log(bars[contract][index].close) - Math.Log(closeMA);
            double logDel = Math.Log(bars[contract][index].high) - Math.Log(bars[contract][index].low);
            double LogVol_5 = Math.Log(bars[contract][index].volume) - Math.Log(bars[contract][index-4].volume);
            double LogVol_5_Ma = Math.Log(bars[contract][index].volume) - Math.Log(volueMA);

            Console.WriteLine(logRet_5_Ma);

            using (Py.GIL())
            {
                dynamic obs = np.array(new List<double> { logRet_1,  logDel, logRet_5,
         9.13209065e-01,1,1 });

                dynamic state = hmmmodel.decode(obs.reshape(1, 6));

                PyObject st = state[1];

                Console.WriteLine(DateTime.Now + ":" + st.ToString());

                if (st.ToString() == "[7]")
                {
                    double averagePrice = Math.Round((contractAsk[contract][contractAsk[contract].Count-1] * contractAskSize[contract][contractAskSize[contract].Count-1] +
                        contractBid[contract][contractBid[contract].Count - 1] * contractBidSize[contract][contractBidSize[contract].Count - 1]) / (contractAskSize[contract][contractAskSize[contract].Count - 1] + 
                        contractBidSize[contract][contractBidSize[contract].Count - 1]), 2);
                    double enterPrice = averagePrice < bars[contract][index].close ? averagePrice : bars[contract][index].close;
                    Order buylimit = new Order();
                    buylimit.Action = "BUY";
                    buylimit.TotalQuantity = 200;
                    buylimit.OrderType = "LMT";
                    buylimit.OutsideRth = true;
                    buylimit.LmtPrice = enterPrice;
                    // buyStop.AuxPrice = stopPrice;
                    PlaceOrder(contract, buylimit);
                    buyPlaced[contract] = true;
                    Console.WriteLine("Send buy order");
                }

                else if (st.ToString() == "[0]" || st.ToString() == "[2]")
                {
                    double averagePrice = Math.Round((contractAsk[contract][contractAsk[contract].Count - 1] * contractAskSize[contract][contractAskSize[contract].Count - 1] +
                        contractBid[contract][contractBid[contract].Count - 1] * contractBidSize[contract][contractBidSize[contract].Count - 1]) / (contractAskSize[contract][contractAskSize[contract].Count - 1] +
                        contractBidSize[contract][contractBidSize[contract].Count - 1]), 2);
                    double enterPrice = averagePrice > bars[contract][index].close ? averagePrice : bars[contract][index].close;
                    Order sellorder = new Order();
                    sellorder.Action = "SELL";
                    sellorder.TotalQuantity = 200;
                    sellorder.OrderType = "LMT";
                    sellorder.OutsideRth = true;
                    sellorder.LmtPrice = enterPrice;
                    PlaceOrder(contract, sellorder);
                    sellPlaced[contract] = true;
                    Console.WriteLine("Send sell order");
                }

            }

        }
    }
}
