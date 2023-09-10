using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyeshade.FuncModule
{
    public class EyeshadeCountdownProgressChangedArgs : EventArgs
    {
        public EyeshadeCountdownProgressChangedArgs(double progress)
        {
            Progress = progress;
        }

        public double Progress { get; private set; }
    }
}
