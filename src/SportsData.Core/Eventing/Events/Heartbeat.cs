﻿using System;

namespace SportsData.Core.Eventing.Events
{
    public class Heartbeat
    {
        public string Producer { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}