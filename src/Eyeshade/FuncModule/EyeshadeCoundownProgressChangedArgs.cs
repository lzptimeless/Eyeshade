using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyeshade.FuncModule
{
    public class EyeshadeCoundownProgressChangedArgs : EventArgs
    {
        public EyeshadeCoundownProgressChangedArgs(double progress)
        {
            Progress = progress;
        }

        public double Progress { get; private set; }
    }
}
