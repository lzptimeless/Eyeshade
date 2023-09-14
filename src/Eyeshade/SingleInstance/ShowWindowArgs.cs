using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eyeshade.SingleInstance
{
    public class ShowWindowArgs : EventArgs
    {
        public ShowWindowArgs(string? args)
        {
            Args = args;
        }

        public string? Args { get; }
    }
}
