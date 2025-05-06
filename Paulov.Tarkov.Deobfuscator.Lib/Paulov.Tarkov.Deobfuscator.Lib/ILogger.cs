using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paulov.Tarkov.Deobfuscator.Lib
{
    /// <summary>
    /// A simple Logging interface. Inherit this into your Window/UserControl/Test class to enable logging from the Deobfuscator
    /// </summary>
    public interface ILogger
    {
        public void Log(string message);    
    }
}
