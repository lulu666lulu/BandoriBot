﻿using System;
using System.Collections.Generic;
using System.Text;

namespace BandoriBot.Config
{
    public enum CarType
    {
        Bandori,
        Sekai,
        None
    }

    public class CarTypeConfig : SerializableConfiguration<Dictionary<long, CarType>>
    {
        public override string Name => "cartype.json";

        public override void LoadDefault()
        {
            t = new Dictionary<long, CarType>();
        }

        public CarType this[long group]
        {
            get
            {
                lock (this)
                {
                    if (t.TryGetValue(group, out var type))
                        return type;
                    else
                        return CarType.Bandori;
                }
            }
            set
            {
                lock (this)
                {
                    t[group] = value;
                    Save();
                }
            }
        }
    }
}
