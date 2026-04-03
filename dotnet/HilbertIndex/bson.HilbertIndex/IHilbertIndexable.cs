using System;
using System.Collections.Generic;
using System.Linq;

namespace bson.HilbertIndex
{
    public interface IHilbertIndexable
    {
        ulong Hid { get; }

        double X { get; }

        double Y { get; }
    }
}