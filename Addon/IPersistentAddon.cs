﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SapphireBootWPF.Addon {
    interface IPersistentAddon : IAddon {
        void DoWork(object state);
    }
}