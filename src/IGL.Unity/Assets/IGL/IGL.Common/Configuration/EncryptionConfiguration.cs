using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IGL.Configuration
{
    public class EncryptionConfiguration
    {
        public bool IsEncryptionEnabled { get; set; }
        public string Salt { get; set; }
    }
}
