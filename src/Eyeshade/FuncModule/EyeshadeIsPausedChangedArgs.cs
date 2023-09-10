using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyeshade.FuncModule
{
    public class EyeshadeIsPausedChangedArgs : EventArgs
    {
        public EyeshadeIsPausedChangedArgs(bool isPause)
        {
            IsPause = isPause;
        }

        public bool IsPause { get; private set; }
    }
}
