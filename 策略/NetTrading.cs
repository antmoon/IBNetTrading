using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;
using IBSampleApp;
using TicTacTec;//初始运行时，没有仓位，使用突破/超卖/超买开仓，保证有最大的赢率；后续在有仓位的情况下，进行网格交易，止盈点设为（average(最高）-average(最低)）/3，不设止损

namespace IBNetTrading.Strategy
{       
    class NetTrading:TradingInterface
    {
        Dictionary<Contract, double> profitPoint;
        Dictionary<Contract, double> netPoint;

        public NetTrading(IBClient ibclient, int numberOfdata) : base(ibclient, numberOfdata)
        {

        }
        protected override void OnAddcontract(Contract contract)
        {
            base.OnAddcontract(contract);
            ibclient.ClientSocket.reqHistoricalData(requestId[contract], contract, DateTime.Now.ToString("yyyyMMdd HH:mm:ss"), "20 D", "1 day", "MIDPOINT", 1, 1, new List<TagValue>());
            ibclient.ClientSocket.reqRealTimeBars(requestId[contract], contract, 5, "MIDPOINT", false, new List<TagValue>());
        }
        protected override void OnRemovecontract(Contract contract)
        {
            // base.OnRemovecontract(contract);
            ibclient.ClientSocket.cancelRealTimeBars(requestId[contract]);
        }




       
    }
}
