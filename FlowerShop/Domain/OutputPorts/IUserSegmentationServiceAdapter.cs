﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain;
using Domain.OutputPorts;

namespace SegmentAnalysis
{
    public interface IUserSegmentationServiceAdapter
    {
        List<UserSegment> Create();
    }
}
