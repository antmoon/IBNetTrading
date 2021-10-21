using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;

namespace IBNetTrading.Strategy
{
    class FXHighLowBreakout:TradingInterface
    {
        /// <summary>
        /// 同我的股票high low breakout，只是只适用于外汇市场，外汇只有bid和ask，没有trade。因此空仓或short时，在ask中进行买操作；空仓或long时，在bid中进行卖操作
        /// </summary>
        public int NumberOfTicks;
        public FXHighLowBreakout(IBClient ibclient, int numberOfdata = 101): base(ibclient, numberOfdata)
        {
            NumberOfTicks = numberOfdata;
            bidEvent += OnBid;
            fillEvent += OnFill;
            askEvent += OnAsk;
        }
        protected override void OnAddcontract(Contract contract)
        {
            if (contract.SecType == "CASH")
            {
                Console.WriteLine(contract.Symbol + " is Ok, proceeding...");
                base.OnAddcontract(contract);
                ibclient.ClientSocket.reqMktData(requestId[contract], contract, "", false, new List<TagValue>());
                ibclient.ClientSocket.reqContractDetails(requestId[contract], contract);//为了得到mintick
            }
            else
            {
                Console.WriteLine(contract.Symbol + "不是外汇，不符合本策略");
            }
            
        }
        protected override void OnRemovecontract(Contract contract)
        {
            ibclient.ClientSocket.cancelMktData(requestId[contract]);
        }
        /// <summary>
        /// 空仓时，根据信号进行卖操作；多仓时，根据信号进行平仓止损或者跟踪止损操作
        /// </summary>
        /// <param name="contract"></param>
        void OnBid(Contract contract)
        {           

        }
        /// <summary>
        /// 空仓时，根据信号进行买操作；short时，根据信号进行平仓止损或者跟踪止损操作
        /// </summary>
        /// <param name="contract"></param>
        void OnAsk(Contract contract)
        {           

        }
        void OnFill(Contract contract)
        {
            if (contractPosition[contract].holding == (double)0)
            {
                buyPlaced[contract] = false;
                sellPlaced[contract] = false;
            }
        }
    }
}
