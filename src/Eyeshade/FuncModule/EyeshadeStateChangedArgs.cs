using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyeshade.FuncModule
{
    public class EyeshadeStateChangedArgs : EventArgs
    {
        public EyeshadeStateChangedArgs(EyeshadeStates state)
        {
            State = state;
        }

        public EyeshadeStates State { get; private set; }
    }
}
