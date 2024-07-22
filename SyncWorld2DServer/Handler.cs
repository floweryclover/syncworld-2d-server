using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncWorld2DServer
{
    internal class Handler : SyncWorld2DProtocol.Stc.IStcHandler
    {
        public bool OnHelloClient(string message)
        {
            Console.WriteLine(message);
            return true;
        }
    }
}
