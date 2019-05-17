using System;
using System.Collections.Generic;
using System.Text;

namespace QueueManager
{
    public enum Operation : byte
    {
        Enqueue,
        Dequeue,
        Removal
    }
}
