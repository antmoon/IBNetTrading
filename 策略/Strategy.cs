using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IBApi;

namespace IBNetTrading.Strategy
{
    /// <summary>
    /// 策略接口，由每个策略继承这个接口，然后实现接口的功能；策略接口使用IB接口功能
    /// </summary>
    interface Strategy
    {
        /// <summary>
        /// Trade事件时使用
        /// </summary>
        void OnTick(Contract contract);
        void OnQuote(Contract contract);
        void OnBar(Contract contract);
        void OnHistPrice(Contract contract);
        void OnFill(Contract contract);
        void OnPosition(Contract contract);
    }
}
