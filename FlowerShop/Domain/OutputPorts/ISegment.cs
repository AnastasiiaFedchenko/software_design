using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.OutputPorts
{
    public interface ISegment
    {
        List<UserSegment> create();
    }
}
